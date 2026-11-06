"""Compare only fresh, in-stock offers of the same structural variant/warranty."""
from collections import defaultdict
from datetime import datetime, timezone
from sqlalchemy import select
from .db import AppSetting, Product, LatestOffer, Seller, Brand, get_settings, now
from .urls import product_url

STATUSES = {'not_best', 'best', 'tied', 'no_competitor', 'missing_own', 'own_unavailable', 'needs_review', 'not_monitored'}
STATUS_LABELS = {'not_best': 'قیمت ما بهترین نیست', 'best': 'قیمت ما پایین‌تر است', 'tied': 'هم‌قیمت ارزان‌ترین رقیب', 'no_competitor': 'رقیب قابل مقایسه یافت نشد', 'missing_own': 'فروشگاه ما در محصول یافت نشد', 'own_unavailable': 'پیشنهاد ما موجود یا جاری نیست', 'needs_review': 'نیازمند دریافت یا بررسی اطلاعات', 'not_monitored': 'پایش غیرفعال'}


def own_store(db):
    def value(key, default=''):
        record = db.get(AppSetting, key)
        return record.value if record else default
    sid = value('own_seller_id')
    seller = db.get(Seller, sid) if sid else None
    return {'seller_id': sid, 'seller_name': seller.name if seller else value('own_seller_name'), 'undercut_toman': int(value('undercut_toman', '1')), 'matched': seller is not None}


def compare_prices(db, brand_id=None, q='', status='not_best', page=1, page_size=20):
    config = own_store(db)
    summary = {s: 0 for s in STATUSES}
    if not config['seller_id']:
        return {'configured': False, 'store': config, 'items': [], 'total': 0, 'summary': summary, 'products': 0, 'not_best_products': 0, 'page': page, 'generated_at': now()}
    stmt = select(Product, Brand.name).join(Brand).where(Product.deleted_at == None)
    if brand_id: stmt = stmt.where(Product.brand_id == brand_id)
    if q: stmt = stmt.where(Product.title.contains(q, autoescape=True))
    products = db.execute(stmt.order_by(Product.id)).all()
    offers = defaultdict(list)
    ids = [p.id for p, _ in products]
    # Chunk IDs to stay within SQLite's parameter limit for larger catalogs.
    for start in range(0, len(ids), 500):
        for o, name in db.execute(select(LatestOffer, Seller.name).join(Seller).where(LatestOffer.product_id.in_(ids[start:start + 500]))):
            offers[o.product_id].append((o, name))
    tolerance = get_settings(db)['interval_minutes'] * 60 * 1.5
    moment = datetime.now(timezone.utc)
    result = []
    for product, brand in products:
        base = {'product_id': product.id, 'product_title': product.title, 'brand_id': product.brand_id, 'brand_name': brand, 'url': product_url(product.source_id) if product.source_id.isdigit() else product.url, 'last_success': product.last_success, 'error': product.error, 'variant': '—', 'own_price': None, 'competitor_price': None, 'competitor_name': None, 'competitor_id': None, 'difference': None, 'target_price': None, 'reduction_needed': None, 'own_observed_at': None, 'competitor_observed_at': None}
        def add(state, **values):
            result.append({**base, **values, 'status': state, 'status_label': STATUS_LABELS[state]})
        if not product.tracked:
            add('not_monitored')
            continue
        if product.error or not product.last_success or (moment - datetime.fromisoformat(product.last_success)).total_seconds() > tolerance:
            add('needs_review')
            continue
        product_offers = offers[product.id]
        ours = [(o, n) for o, n in product_offers if o.seller_id == config['seller_id']]
        if not ours:
            add('missing_own')
            continue
        for own, _ in ours:
            details = {'variant': own.variant, 'own_price': own.price, 'own_observed_at': own.observed_at}
            if own.missing or own.observed_at != product.last_success or own.available is False or not own.price or own.price <= 0:
                add('own_unavailable', **details)
                continue
            if not own.comparison_key or own.available is None:
                add('needs_review', **details)
                continue
            peers = [(o, n) for o, n in product_offers if o.seller_id != config['seller_id'] and not o.missing and o.observed_at == product.last_success]
            # A legacy/unknown key cannot prove which variant the rival belongs
            # to. Avoid claiming a best price from such a mixed snapshot.
            if any(not o.comparison_key for o, _ in peers):
                add('needs_review', **details)
                continue
            comparable = [(o, n) for o, n in peers if o.comparison_key == own.comparison_key]
            if any(o.available is None for o, _ in comparable):
                add('needs_review', **details)
                continue
            rivals = [(o, n) for o, n in comparable if o.available is True and o.price is not None and o.price > 0]
            if not rivals:
                add('no_competitor', **details)
                continue
            cheapest, name = min(rivals, key=lambda pair: (pair[0].price, pair[0].seller_id))
            difference = own.price - cheapest.price
            state = 'not_best' if difference > 0 else 'tied' if difference == 0 else 'best'
            target = cheapest.price - config['undercut_toman']
            target = target if state == 'not_best' and target > 0 else None
            add(state, **details, competitor_id=cheapest.seller_id, competitor_name=name, competitor_price=cheapest.price, competitor_observed_at=cheapest.observed_at, difference=difference, target_price=target, reduction_needed=own.price - target if target is not None else None)
    for item in result:
        summary[item['status']] += 1
    not_best_products = len({r['product_id'] for r in result if r['status'] == 'not_best'})
    if status != 'all': result = [r for r in result if r['status'] == status]
    result.sort(key=lambda r: (r['status'] != 'not_best', -(r['difference'] or 0), r['product_id'], r['variant']))
    total = len(result)
    return {'configured': True, 'store': config, 'items': result[(page - 1) * page_size:page * page_size], 'total': total, 'summary': summary, 'products': len(products), 'not_best_products': not_best_products, 'page': page, 'generated_at': now()}
