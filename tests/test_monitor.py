import copy
import json
from pathlib import Path
import pytest
from sqlalchemy import select, func
from backend.collector import Offer, SourceError, parse_product, Snappshop
from backend.db import LatestOffer, Observation, Notification, Product, AppSetting, SessionLocal
from backend.monitor import apply_observations, poll_product, tick


def offer(price=100000, seller='one', key=None, available=True):
    return Offer(seller, 'فروشندهٔ ' + seller, key or seller, '۳۰ میلی‌لیتر', price, 150000, available)


def test_initial_then_change_and_unchanged_do_not_duplicate_alerts(db, product):
    apply_observations(db, product, [offer()], '2026-11-06T09:30:00.000000+00:00')
    db.commit()
    assert db.scalar(select(func.count()).select_from(Notification)) == 0
    apply_observations(db, product, [offer(110000)], '2026-11-06T10:30:00.000000+00:00')
    db.commit()
    event = db.scalar(select(Notification))
    assert (event.old_price, event.new_price, event.percent) == (100000, 110000, 10)
    assert event.previous_observed_at == '2026-11-06T09:30:00.000000+00:00'
    apply_observations(db, product, [offer(110000)])
    db.commit()
    assert db.scalar(select(func.count()).select_from(Notification)) == 1
    assert db.scalar(select(func.count()).select_from(Observation)) == 3


def test_threshold_keeps_history_and_event(db, product):
    db.get(AppSetting, 'threshold_percent').value = '20'
    apply_observations(db, product, [offer()])
    db.commit()
    apply_observations(db, product, [offer(110000)])
    db.commit()
    assert db.scalar(select(Notification)).alerts is False
    assert db.scalar(select(func.count()).select_from(Observation)) == 2


def test_missing_is_not_stock_change_and_reappearance_preserves_seller(db, product):
    apply_observations(db, product, [offer()])
    db.commit()
    previous_time = db.scalar(select(LatestOffer)).observed_at
    apply_observations(db, product, [])
    db.commit()
    latest = db.scalar(select(LatestOffer))
    assert latest.missing and latest.available is True
    assert latest.price == 100000 and latest.observed_at == previous_time
    assert not db.scalar(select(Notification))
    apply_observations(db, product, [offer()])
    db.commit()
    assert latest.missing is False
    assert not db.scalar(select(Notification))


def test_seller_added_once_for_multiple_variants_and_stock_change(db, product):
    apply_observations(db, product, [offer()])
    db.commit()
    apply_observations(db, product, [offer(available=False), offer(seller='two', key='two-small'), offer(200000, seller='two', key='two-large')])
    db.commit()
    kinds = [n.kind for n in db.scalars(select(Notification))]
    assert kinds.count('seller_added') == 1
    assert kinds.count('stock') == 1
    assert 'price_up' not in kinds


def test_failure_preserves_prices_and_success_timestamp(db, product, monkeypatch):
    apply_observations(db, product, [offer()])
    db.commit()
    timestamp = product.last_success
    def fail(_): raise SourceError('403 denied')
    monkeypatch.setattr('backend.monitor.source.product', fail)
    poll_product(product.id)
    db.expire_all()
    assert db.get(Product, product.id).last_success == timestamp
    assert db.get(Product, product.id).error == '403 denied'
    assert db.scalar(select(LatestOffer)).price == 100000
    poll_product(product.id)
    assert db.scalar(select(func.count()).select_from(Notification).where(Notification.kind == 'error')) == 1


def fixture_product():
    payload = json.loads((Path(__file__).parent / 'fixtures' / 'product.json').read_text(encoding='utf-8'))
    # The upstream fixture is intentionally abbreviated (3 of 43 vendors).
    # Normalize the count only in synthetic full-response tests below.
    for variant in payload['data']['variants']:
        variant['active_vendors'] = len(variant['vendor'])
    return payload


def test_recorded_source_response_is_supported():
    _, offers = parse_product(fixture_product())
    assert len(offers) == 3
    assert all(o.price > 0 and o.seller_name for o in offers)


def test_abbreviated_recorded_response_is_detected_as_partial():
    raw = json.loads((Path(__file__).parent / 'fixtures' / 'product.json').read_text(encoding='utf-8'))
    with pytest.raises(SourceError, match='کامل نیست'):
        parse_product(raw)


def test_parser_collects_all_200_sellers_without_cap():
    payload = fixture_product()
    data = payload['data']
    template = data['variants'][0]
    vendor = template['vendor'][0]
    data['variants'] = [copy.deepcopy(template)]
    data['variants'][0]['vendor'] = [{**vendor, 'vendor_id': str(i)} for i in range(200)]
    data['variants'][0]['active_vendors'] = 200
    data['vendors'] = [{'id': str(i), 'title': f'Seller {i}'} for i in range(200)]
    _, offers = parse_product(payload)
    assert len(offers) == 200
    assert len({o.comparison_key for o in offers}) == 1
    assert offers[0].comparison_key is not None


@pytest.mark.parametrize('mutation', ['missing_vendors', 'bad_price', 'unknown_seller', 'duplicate', 'pagination'])
def test_malformed_source_fails_without_partial_data(mutation):
    payload = fixture_product()
    data = payload['data']
    if mutation == 'missing_vendors': del data['vendors']
    if mutation == 'bad_price': data['variants'][0]['vendor'][0].update(price=-3, special_price=-3)
    if mutation == 'unknown_seller': data['variants'][0]['vendor'][0]['vendor_id'] = 'unknown'
    if mutation == 'duplicate': data['variants'][0]['vendor'].append(copy.deepcopy(data['variants'][0]['vendor'][0]))
    if mutation == 'pagination': data['pagination'] = {'next': 2}
    with pytest.raises(SourceError): parse_product(payload)


def test_search_pagination_and_unrelated_fallback(monkeypatch):
    source = Snappshop()
    calls = []
    def fetch(path, body):
        calls.append(body)
        items = [{'href': '/product/1111', 'title': 'سرم ویتالیر', 'image': {'src': 'https://cdn.snappshop.ir/a.jpg'}}, {'href': '/product/2222', 'title': 'محصول نامرتبط'}] if 'skip' not in body else [{'href': '/product/3333', 'title': 'کرم ویتالیر'}]
        return {'data': {'structure': [{'section_type': 'plp', 'items': items, 'pagination': {'total_pages': 2}}]}}
    monkeypatch.setattr(source, 'fetch', fetch)
    products = source.discover('ویتالیر')
    assert [p['source_id'] for p in products] == ['1111', '3333']
    assert products[0]['url'] == 'https://snappshop.ir/product/snp-1111'
    assert calls[1]['skip'] == 1


def test_tick_does_not_repoll_a_leased_product(db, product, monkeypatch):
    calls = []
    monkeypatch.setattr('backend.monitor.source.product', lambda sid: (None, calls.append(sid) or [offer()]))
    tick()
    tick()
    assert calls == [product.source_id]


def test_duplicates_roll_back_whole_snapshot(db, product):
    with pytest.raises(ValueError):
        apply_observations(db, product, [offer(), offer(200000)])
    db.rollback()
    assert db.scalar(select(func.count()).select_from(Observation)) == 0
    assert db.scalar(select(func.count()).select_from(LatestOffer)) == 0


def test_named_sellers_without_variants_are_not_treated_as_no_sellers():
    payload = fixture_product()
    payload['data']['variants'] = []
    with pytest.raises(SourceError, match='ناقص'):
        parse_product(payload)
