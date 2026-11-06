from datetime import datetime, timedelta, timezone
from io import BytesIO
import pytest
from sqlalchemy import select, func, text
from openpyxl import load_workbook
from fastapi.testclient import TestClient
from backend.collector import Offer
from backend.db import AppSetting, Product, LatestOffer, Observation, engine, init_db
from backend.main import app
from backend.monitor import apply_observations
from backend.competition import compare_prices


def configure(db, seller_id='ours', step=1000):
    for key, value in [('own_seller_id', seller_id), ('own_seller_name', 'فروشگاه ما'), ('undercut_toman', str(step))]:
        db.merge(AppSetting(key=key, value=value))
    db.commit()


def offer(seller='ours', price=120000, group='same', available=True, variant='پیش‌فرض'):
    return Offer(seller, 'فروشگاه ما' if seller == 'ours' else seller, seller + '-' + str(group), variant, price, price, available, group)


def snapshot(db, product, *offers):
    apply_observations(db, product, list(offers))
    db.commit()


def test_no_default_store_is_not_silently_guessed(client):
    result = client.get('/api/price-position').json()
    assert result['configured'] is False
    assert result['items'] == []
    assert client.get('/api/price-position/excel').status_code == 422


def test_not_best_price_excludes_our_own_seller_and_calculates_target(db, product):
    configure(db)
    snapshot(db, product, offer(), offer('cheap', 100000), offer('other', 110000))
    result = compare_prices(db)
    assert result['not_best_products'] == 1
    item = result['items'][0]
    assert item['competitor_id'] == 'cheap'
    assert item['difference'] == 20000
    assert item['target_price'] == 99000
    assert item['reduction_needed'] == 21000
    assert item['own_observed_at'] == item['competitor_observed_at']


@pytest.mark.parametrize('own_price,status', [(100000, 'tied'), (90000, 'best'), (110000, 'not_best')])
def test_equal_price_is_joint_best_and_lower_price_is_best(db, product, own_price, status):
    configure(db)
    snapshot(db, product, offer(price=own_price), offer('other', 100000))
    result = compare_prices(db, status='all')
    assert result['items'][0]['status'] == status
    if status != 'not_best': assert result['items'][0]['target_price'] is None


def test_other_variants_and_warranties_do_not_compete(db, product):
    configure(db)
    snapshot(db, product, offer(group='small-warranty-a'), offer('other', 5000, group='large-warranty-a'), offer('another', 8000, group='small-warranty-b'))
    result = compare_prices(db, status='all')
    assert result['items'][0]['status'] == 'no_competitor'
    assert result['not_best_products'] == 0


def test_unavailable_and_missing_rivals_are_excluded(db, product):
    configure(db)
    snapshot(db, product, offer(), offer('sold-out', 1000, available=False), offer('available', 130000))
    result = compare_prices(db, status='all')
    assert result['items'][0]['status'] == 'best'
    assert result['items'][0]['competitor_id'] == 'available'
    db.scalar(select(LatestOffer).where(LatestOffer.seller_id == 'available')).missing = True
    db.commit()
    assert compare_prices(db, status='all')['items'][0]['status'] == 'no_competitor'


@pytest.mark.parametrize('issue,expected', [('error', 'needs_review'), ('stale', 'needs_review'), ('stopped', 'not_monitored'), ('own-out', 'own_unavailable'), ('own-missing', 'own_unavailable'), ('unknown-stock', 'needs_review'), ('unknown-identity', 'needs_review')])
def test_uncertain_data_never_claims_best_or_actionable_price(db, product, issue, expected):
    configure(db)
    snapshot(db, product, offer(), offer('other', 100000))
    own = db.scalar(select(LatestOffer).where(LatestOffer.seller_id == 'ours'))
    rival = db.scalar(select(LatestOffer).where(LatestOffer.seller_id == 'other'))
    if issue == 'error': product.error = 'Source unavailable'
    if issue == 'stale': product.last_success = (datetime.now(timezone.utc) - timedelta(hours=3)).isoformat(timespec='microseconds')
    if issue == 'stopped': product.tracked = False
    if issue == 'own-out': own.available = False
    if issue == 'own-missing': own.missing = True
    if issue == 'unknown-stock': rival.available = None
    if issue == 'unknown-identity': rival.comparison_key = None
    db.commit()
    item = compare_prices(db, status='all')['items'][0]
    assert item['status'] == expected
    assert item['target_price'] is None
    assert compare_prices(db)['total'] == 0


def test_missing_our_store_is_not_best(db, product):
    configure(db)
    snapshot(db, product, offer('other', 100000))
    assert compare_prices(db, status='all')['items'][0]['status'] == 'missing_own'


def test_negative_or_zero_suggested_prices_are_never_returned(db, product):
    configure(db, step=1000)
    snapshot(db, product, offer(price=1000), offer('other', 500))
    item = compare_prices(db)['items'][0]
    assert item['status'] == 'not_best'
    assert item['target_price'] is None and item['reduction_needed'] is None


def test_multiple_variants_show_each_offer_but_product_count_is_unique(db, product):
    configure(db)
    snapshot(db, product, offer(group='a'), offer('other', 100000, group='a'), offer(group='b'), offer('other', 90000, group='b'))
    result = compare_prices(db)
    assert result['total'] == 2 and result['not_best_products'] == 1
    assert result['items'][0]['difference'] == 30000


def test_store_settings_persist_validate_clear_and_respect_viewer(client, db, product):
    snapshot(db, product, offer())
    result = client.put('/api/settings/own-store', json={'seller_id': 'ours', 'seller_name': 'ignored name', 'undercut_toman': 1000})
    assert result.status_code == 200
    assert result.json()['seller_name'] == 'فروشگاه ما'
    assert client.get('/api/settings/own-store').json()['seller_id'] == 'ours'
    assert client.put('/api/settings/own-store', json={'seller_id': 'unknown'}).status_code == 422
    assert client.put('/api/settings/own-store', json={'seller_id': 'ours', 'undercut_toman': 0}).status_code == 422
    with TestClient(app) as viewer:
        viewer.post('/api/auth/login', json={'username': 'viewer', 'password': 'test-viewer-password'})
        assert viewer.get('/api/price-position').status_code == 200
        assert viewer.put('/api/settings/own-store', json={'seller_id': ''}).status_code == 403
    assert client.put('/api/settings/own-store', json={'seller_id': ''}).status_code == 200
    assert client.get('/api/price-position').json()['configured'] is False


def test_unknown_store_id_can_be_saved_with_an_explicit_name(client):
    result = client.put('/api/settings/own-store', json={'seller_id': 'new-store', 'seller_name': 'فروشگاه تازه', 'undercut_toman': 1})
    assert result.status_code == 200
    assert result.json()['matched'] is False
    assert client.get('/api/price-position').json()['store']['seller_name'] == 'فروشگاه تازه'


def test_comparison_filters_pagination_and_excel_consistency(client, db, product):
    configure(db)
    snapshot(db, product, offer(), offer('other', 100000))
    for brand in ('eudoramax', 'elvina'):
        p = Product(brand_id=brand, source_id='12345' + brand, title='محصول دوم', url='', tracked=True)
        db.add(p); db.flush()
        snapshot(db, p, offer(), offer('other', 100000))
    assert client.get('/api/price-position', params={'page_size': 1, 'page': 2}).json()['items'][0]['product_id'] != product.id
    filtered = client.get('/api/price-position', params={'brand_id': 'vitalayer'}).json()
    assert filtered['total'] == 1 and filtered['products'] == 1
    assert client.get('/api/price-position', params={'q': 'سرم'}).json()['total'] == 1
    assert client.get('/api/price-position', params={'status': 'bogus'}).status_code == 422
    response = client.get('/api/price-position/excel', params={'brand_id': 'vitalayer'})
    assert response.status_code == 200
    wb = load_workbook(BytesIO(response.content))
    ws = wb.worksheets[0]
    assert ws.max_row == 5 and ws.sheet_view.rightToLeft
    assert ws['E5'].value == 120000 and ws['G5'].value == 100000
    assert ws['I5'].value == 99000 and ws['J5'].value == 21000


def test_additive_upgrade_preserves_prices_history_and_queues_refresh(db, product):
    snapshot(db, product, offer())
    with engine.begin() as connection:
        connection.execute(text('ALTER TABLE latest_offers DROP COLUMN comparison_key'))
    init_db()
    db.expire_all()
    assert db.scalar(select(LatestOffer)).price == 120000
    assert db.scalar(select(func.count()).select_from(Observation)) == 1
    assert db.get(Product, product.id).next_check is not None
    assert db.scalar(select(LatestOffer)).comparison_key is None
