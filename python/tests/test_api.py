from io import BytesIO
from fastapi.testclient import TestClient
from openpyxl import load_workbook
from sqlalchemy import select, func
from backend.main import app
from backend.db import LatestOffer, Observation, Product, Notification
from backend.monitor import apply_observations
from test_monitor import offer


def test_auth_and_viewer_cannot_edit(client, product):
    with TestClient(app) as anonymous:
        assert anonymous.get('/api/products').status_code == 401
        assert anonymous.post('/api/auth/login', json={'username': 'viewer', 'password': 'test-viewer-password'}).status_code == 200
        assert anonymous.get('/api/products').status_code == 200
        assert anonymous.post('/api/products/tracking', json={'ids': [product.id], 'tracked': False}).status_code == 403
    assert client.post('/api/auth/logout').status_code == 200
    assert client.get('/api/me').status_code == 401


def test_csrf_and_bad_login(client):
    assert client.post('/api/auth/login', headers={'Origin': 'https://evil.test'}, json={'username': 'admin', 'password': 'test-admin-password'}).status_code == 403
    assert client.post('/api/auth/login', json={'username': 'admin', 'password': 'wrong'}).status_code == 401


def test_tracking_stop_keeps_history_and_resume_queues(client, db, product):
    apply_observations(db, product, [offer()])
    db.commit()
    response = client.post('/api/products/tracking', json={'ids': [product.id], 'tracked': False})
    assert response.status_code == 200
    assert client.get(f'/api/products/{product.id}').json()['tracked'] is False
    assert client.get(f'/api/products/{product.id}/history').json()['total'] == 1
    assert client.post('/api/products/tracking', json={'ids': [product.id], 'tracked': True}).status_code == 200
    assert client.get(f'/api/products/{product.id}').json()['next_check'] is not None


def test_filters_history_timeline_and_notifications(client, db, product):
    apply_observations(db, product, [offer()], '2026-11-06T09:30:00.000000+00:00')
    db.commit()
    apply_observations(db, product, [offer(120000), offer(150000, 'two')], '2026-11-06T10:30:00.000000+00:00')
    db.commit()
    assert client.get('/api/products', params={'brand_id': 'vitalayer', 'q': 'سرم'}).json()['total'] == 1
    assert client.get('/api/products', params={'brand_id': 'isol'}).json()['total'] == 0
    assert client.get(f'/api/products/{product.id}/history', params={'seller_id': 'two'}).json()['total'] == 1
    assert client.get(f'/api/products/{product.id}/timeline').json()[1]['price'] == 120000
    assert client.get('/api/notifications', params={'kind': 'price_up'}).json()['total'] == 1
    assert client.post('/api/notifications/read', json={}).status_code == 200
    assert client.get('/api/notifications', params={'unread': True}).json()['total'] == 0


def test_settings_validation_and_time_validation(client):
    assert client.put('/api/settings', json={'interval_minutes': 1, 'threshold_percent': 0, 'retention_days': 30, 'discovery_hours': 24}).status_code == 422
    assert client.get('/api/reports/excel', params={'start': '2026-11-06'}).status_code == 422
    assert client.get('/api/reports/excel', params={'start': '2026-11-06T13:00:00+03:30', 'end': '2026-11-05T13:00:00+03:30'}).status_code == 422


def test_export_numeric_prices_rtl_filters_time_and_formula_safety(client, db, product):
    product.title = '=HYPERLINK("https://evil.test")'
    apply_observations(db, product, [offer()], '2026-11-06T09:30:00.000000+00:00')
    db.commit()
    apply_observations(db, product, [offer(120000)], '2026-11-06T10:30:00.000000+00:00')
    db.commit()
    response = client.get('/api/reports/excel', params={'product_id': product.id, 'start': '2026-11-06T10:00:00+00:00'})
    assert response.status_code == 200
    wb = load_workbook(BytesIO(response.content))
    assert wb.sheetnames == ['خلاصه مدیریتی', 'آخرین قیمت‌ها', 'تغییرات قیمت', 'تاریخچه']
    ws = wb['آخرین قیمت‌ها']
    assert ws.sheet_view.rightToLeft is True and ws.freeze_panes == 'C5'
    assert ws['B5'].data_type == 's' and ws['B5'].value.startswith('=HYPERLINK')
    assert ws['G5'].value == 120000 and ws['G5'].data_type == 'n'
    assert ws['J5'].value == '2026-11-06 14:00:00'
    assert ws.auto_filter.ref == 'A4:L5'
    assert ws['L5'].value == 'https://snappshop.ir/product/snp-123456'
    assert wb['تاریخچه'].max_row == 5
    assert wb['تغییرات قیمت']['G5'].value == 20000


def test_report_limit_refuses_instead_of_truncating(client, db, product, monkeypatch):
    apply_observations(db, product, [offer()])
    db.commit()
    monkeypatch.setattr('backend.reports.settings.max_report_rows', 1)
    assert client.get('/api/reports/excel').status_code == 413


def test_manual_url_rejects_arbitrary_network_targets(client):
    for url in ['http://127.0.0.1:8000/secret', 'https://snappshop.ir.evil.test/product/12345', 'https://snappshop.ir@evil.test/product/12345']:
        assert client.post('/api/products', json={'brand_id': 'vitalayer', 'url': url}).status_code == 422


def test_discovery_queue_is_idempotent(client):
    a = client.post('/api/brands/vitalayer/discover').json()
    b = client.post('/api/brands/vitalayer/discover').json()
    assert a['job_id'] == b['job_id']
    assert client.get('/api/brands').json()[0]['job']['status'] == 'queued'


def test_canonical_links_include_snp_for_existing_and_new_records(client, db, product):
    assert client.get(f'/api/products/{product.id}').json()['url'] == 'https://snappshop.ir/product/snp-123456'
    assert client.get('/api/products').json()['items'][0]['url'] == 'https://snappshop.ir/product/snp-123456'
    from backend.db import init_db
    init_db()
    db.expire_all()
    assert db.get(Product, product.id).url == 'https://snappshop.ir/product/snp-123456'


def test_unchecked_seller_count_is_unknown_not_zero(client, product):
    assert client.get('/api/products').json()['items'][0]['seller_count'] is None


def test_out_of_stock_sellers_are_still_counted(client, db, product):
    apply_observations(db, product, [offer(available=False)])
    db.commit()
    result = client.get('/api/products').json()['items'][0]
    assert result['seller_count'] == 1
    assert result['min_price'] is None


def test_preview_reads_current_sellers_without_tracking_or_history(client, db, product, monkeypatch):
    product.tracked = False
    db.commit()
    monkeypatch.setattr('backend.main.source.product', lambda _: ({}, [offer()]))
    response = client.get(f'/api/products/{product.id}/preview')
    assert response.status_code == 200
    assert response.json()['seller_count'] == 1
    assert response.json()['offers'][0]['price'] == 100000
    db.expire_all()
    assert db.get(Product, product.id).tracked is False
    assert db.get(Product, product.id).last_success is None
    assert db.scalar(select(func.count()).select_from(Observation)) == 0
    assert db.scalar(select(func.count()).select_from(LatestOffer)) == 0


def test_preview_distinguishes_empty_source_from_source_failure(client, product, monkeypatch):
    from backend.collector import SourceError
    monkeypatch.setattr('backend.main.source.product', lambda _: ({'page': {'extra_meta': [{'property': 'product:availability', 'content': 'out of stock'}]}}, []))
    result = client.get(f'/api/products/{product.id}/preview').json()
    assert result['seller_count'] == 0 and result['availability'] == 'out_of_stock'
    def fail(_): raise SourceError('Source unavailable')
    monkeypatch.setattr('backend.main.source.product', fail)
    assert client.get(f'/api/products/{product.id}/preview').status_code == 502


def test_manual_existing_product_resumes_tracking_without_duplicate(client, db, product, monkeypatch):
    product.tracked = False
    db.commit()
    def should_not_fetch(_): raise AssertionError('Existing product needs no source lookup')
    monkeypatch.setattr('backend.main.source.product', should_not_fetch)
    result = client.post('/api/products', json={'brand_id': 'vitalayer', 'url': 'https://snappshop.ir/product/snp-123456'}).json()
    assert result['already_exists'] and result['tracked']
    assert result['next_check'] is not None
    assert db.scalar(select(func.count()).select_from(Product)) == 1


def test_manual_product_moves_brand_and_preserves_history(client, db, product):
    apply_observations(db, product, [offer()])
    db.commit()
    result = client.post('/api/products', json={'brand_id': 'eudoramax', 'url': 'https://snappshop.ir/product/123456'}).json()
    assert result['brand_changed'] and result['brand_id'] == 'eudoramax'
    assert result['id'] == product.id
    assert client.get(f'/api/products/{product.id}/history').json()['total'] == 1


def test_manual_new_product_is_tracked_and_sellers_saved_atomically(client, db, monkeypatch):
    monkeypatch.setattr('backend.main.source.product', lambda _: ({'content': {'title_fa': 'محصول ویتالیر'}, 'images': []}, [offer()]))
    response = client.post('/api/products', json={'brand_id': 'vitalayer', 'url': 'https://snappshop.ir/product/snp-123456'})
    result = response.json()
    assert response.status_code == 201
    assert not result['already_exists'] and result['tracked']
    assert result['url'] == 'https://snappshop.ir/product/snp-123456'
    assert result['last_success'] is not None
    assert db.scalar(select(func.count()).select_from(Observation)) == 1
    assert db.scalar(select(func.count()).select_from(LatestOffer)) == 1
    # Repeat submission reuses the same record and never doubles observations.
    assert client.post('/api/products', json={'brand_id': 'vitalayer', 'url': result['url']}).json()['already_exists']
    assert db.scalar(select(func.count()).select_from(Product)) == 1
    assert db.scalar(select(func.count()).select_from(Observation)) == 1
