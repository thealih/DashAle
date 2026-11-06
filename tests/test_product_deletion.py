from io import BytesIO
from fastapi.testclient import TestClient
from openpyxl import load_workbook
from sqlalchemy import select, func, text
from backend.db import Product, LatestOffer, Observation, SessionLocal, engine, init_db
from backend.main import app
from backend.monitor import apply_observations, poll_product, save_discovered
from test_monitor import offer


def test_delete_stops_tracking_hides_catalog_and_preserves_history(client, db, product):
    apply_observations(db, product, [offer()]); db.commit()
    apply_observations(db, product, [offer(110000)]); db.commit()
    result = client.post('/api/products/delete', json={'ids': [product.id]})
    assert result.status_code == 200 and result.json()['updated'] == 1
    assert client.get('/api/products').json()['total'] == 0
    assert client.get('/api/dashboard').json()['products'] == 0
    assert client.get('/api/dashboard').json()['tracked'] == 0
    assert client.get('/api/dashboard').json()['unread'] == 0
    assert client.get('/api/brands').json()[0]['total'] == 0
    assert client.get('/api/notifications').json()['total'] == 0
    db.expire_all()
    p = db.get(Product, product.id)
    assert p.deleted_at and not p.tracked and p.next_check is None
    assert db.scalar(select(func.count()).select_from(Observation)) == 2
    assert db.scalar(select(LatestOffer)).price == 110000
    assert client.get(f'/api/products/{product.id}').status_code == 404
    assert client.get(f'/api/products/{product.id}/preview').status_code == 404
    assert client.get(f'/api/products/{product.id}/history').status_code == 404
    assert client.get(f'/api/products/{product.id}/timeline').status_code == 404
    assert client.post(f'/api/products/{product.id}/check').status_code == 404
    wb = load_workbook(BytesIO(client.get('/api/reports/excel').content))
    assert wb['آخرین قیمت‌ها'].max_row == 4
    assert wb['تاریخچه'].max_row == 4


def test_restoration_preserves_identity_and_keeps_tracking_off(client, db, product):
    apply_observations(db, product, [offer()]); db.commit()
    client.post('/api/products/delete', json={'ids': [product.id]})
    trash = client.get('/api/products', params={'deleted': True}).json()
    assert trash['total'] == 1 and trash['items'][0]['id'] == product.id
    assert client.post('/api/products/restore', json={'ids': [product.id]}).json()['updated'] == 1
    result = client.get(f'/api/products/{product.id}').json()
    assert result['deleted_at'] is None and not result['tracked']
    assert result['offers'][0]['price'] == 100000
    assert client.get(f'/api/products/{product.id}/history').json()['total'] == 1
    assert client.get('/api/products', params={'deleted': True}).json()['total'] == 0


def test_discovery_cannot_reintroduce_excluded_product(client, db, product):
    original_title = product.title
    client.post('/api/products/delete', json={'ids': [product.id]})
    save_discovered(product.brand_id, [{'source_id': product.source_id, 'title': 'New source title', 'url': product.url, 'image': None}])
    assert client.get('/api/products').json()['total'] == 0
    assert db.scalar(select(func.count()).select_from(Product)) == 1
    db.expire_all()
    assert db.get(Product, product.id).title == original_title


def test_deleted_product_cannot_be_resumed_through_tracking_endpoint(client, product):
    client.post('/api/products/delete', json={'ids': [product.id]})
    assert client.post('/api/products/tracking', json={'ids': [product.id], 'tracked': True}).json()['updated'] == 0
    assert client.get('/api/products', params={'deleted': True}).json()['items'][0]['tracked'] is False


def test_explicit_manual_link_restores_and_resumes_instead_of_duplicating(client, product):
    client.post('/api/products/delete', json={'ids': [product.id]})
    response = client.post('/api/products', json={'brand_id': product.brand_id, 'url': 'https://snappshop.ir/product/snp-123456'})
    assert response.status_code == 201
    item = response.json()
    assert item['restored'] and item['tracked']
    assert item['id'] == product.id and item['deleted_at'] is None
    assert client.get('/api/products').json()['total'] == 1


def test_deletion_and_restoration_are_admin_only(client, product):
    with TestClient(app) as viewer:
        viewer.post('/api/auth/login', json={'username': 'viewer', 'password': 'test-viewer-password'})
        for endpoint in ('delete', 'restore'):
            assert viewer.post('/api/products/' + endpoint, json={'ids': [product.id]}).status_code == 403
        assert viewer.get('/api/products', params={'deleted': True}).status_code == 403


def test_bulk_delete_is_idempotent_and_scoped_to_selected_ids(client, db, product):
    other = Product(brand_id='isol', source_id='654321', title='Unselected product', url='', tracked=True)
    db.add(other); db.commit()
    for expected in (1, 0):
        assert client.post('/api/products/delete', json={'ids': [product.id, product.id, 999999]}).json()['updated'] == expected
    assert client.get('/api/products').json()['items'][0]['id'] == other.id
    assert client.post('/api/products/delete', json={'ids': []}).status_code == 422
    assert client.post('/api/products/restore', json={'ids': [product.id]}).json()['updated'] == 1
    assert client.post('/api/products/restore', json={'ids': [product.id]}).json()['updated'] == 0


def test_inflight_response_cannot_save_after_product_is_deleted(client, db, product, monkeypatch):
    def fetch(_):
        result = client.post('/api/products/delete', json={'ids': [product.id]})
        assert result.status_code == 200
        return {}, [offer()]
    monkeypatch.setattr('backend.monitor.source.product', fetch)
    poll_product(product.id)
    assert db.scalar(select(func.count()).select_from(Observation)) == 0
    assert db.scalar(select(func.count()).select_from(LatestOffer)) == 0


def test_deleted_products_are_excluded_from_competitive_prices(client, db, product):
    from test_competition import configure, offer as competitive_offer
    configure(db)
    apply_observations(db, product, [competitive_offer(), competitive_offer('rival', 90000)]); db.commit()
    assert client.get('/api/price-position').json()['total'] == 1
    client.post('/api/products/delete', json={'ids': [product.id]})
    result = client.get('/api/price-position', params={'status': 'all'}).json()
    assert result['total'] == 0 and result['products'] == 0


def test_deletion_column_upgrade_preserves_existing_products(db, product):
    with engine.begin() as connection:
        connection.execute(text('ALTER TABLE products DROP COLUMN deleted_at'))
    init_db()
    db.expire_all()
    assert db.get(Product, product.id).deleted_at is None
    assert db.get(Product, product.id).tracked is True
