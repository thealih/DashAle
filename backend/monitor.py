import logging
import random
import threading
import uuid
from datetime import datetime, timedelta, timezone
from sqlalchemy import select, update, or_, delete, func
from .config import settings
from .db import SessionLocal, Product, Seller, LatestOffer, Observation, Notification, Brand, Job, AppSetting, get_settings, now
from .collector import source, SourceError, Offer

log = logging.getLogger(__name__)
_tick_lock = threading.Lock()


def later(minutes=0, seconds=0):
    return (datetime.now(timezone.utc) + timedelta(minutes=minutes, seconds=seconds)).isoformat(timespec='microseconds')


def notify(db, kind, title, product=None, **kwargs):
    db.add(Notification(kind=kind, title=title, product_id=product.id if product else None, brand_id=product.brand_id if product else kwargs.pop('brand_id', None), **kwargs))


def apply_observations(db, product, offers, timestamp=None):
    """One transaction for a complete response: snapshot, latest and events are atomic."""
    timestamp = timestamp or now()
    old = {o.offer_key: o for o in db.scalars(select(LatestOffer).where(LatestOffer.product_id == product.id))}
    known_sellers = {o.seller_id for o in old.values()}
    new_sellers = set()
    threshold = get_settings(db)['threshold_percent']
    observed_keys = set()
    for o in offers:
        if o.key in observed_keys:
            raise ValueError('Duplicate offer key')
        observed_keys.add(o.key)
        seller = db.get(Seller, o.seller_id)
        if seller:
            seller.name = o.seller_name
        else:
            db.add(Seller(id=o.seller_id, name=o.seller_name))
            db.flush()
        prior = old.get(o.key)
        if not prior:
            prior = LatestOffer(product_id=product.id, seller_id=o.seller_id, offer_key=o.key, variant=o.variant, observed_at=timestamp)
            db.add(prior)
            if product.last_success and o.seller_id not in known_sellers and o.seller_id not in new_sellers:
                notify(db, 'seller_added', f'فروشندهٔ «{o.seller_name}» به {product.title} اضافه شد.', product, seller_id=o.seller_id, created_at=timestamp)
                new_sellers.add(o.seller_id)
        else:
            if prior.price is not None and o.price is not None and prior.price != o.price:
                percent = (o.price - prior.price) / prior.price * 100
                notify(db, 'price_up' if percent > 0 else 'price_down', f'تغییر قیمت «{product.title}» توسط {o.seller_name} · {o.variant}', product, seller_id=o.seller_id, old_price=prior.price, new_price=o.price, percent=percent, previous_observed_at=prior.observed_at, created_at=timestamp, alerts=abs(percent) >= threshold)
            if o.available is not None and prior.available is not None and o.available != prior.available:
                notify(db, 'stock', f'{product.title} نزد {o.seller_name} {"موجود" if o.available else "ناموجود"} شد · {o.variant}', product, seller_id=o.seller_id, created_at=timestamp)
        prior.price, prior.list_price, prior.available = o.price, o.list_price, o.available
        prior.observed_at, prior.missing, prior.variant = timestamp, False, o.variant
        prior.comparison_key = o.comparison_key
        db.add(Observation(product_id=product.id, seller_id=o.seller_id, offer_key=o.key, variant=o.variant, price=o.price, list_price=o.list_price, available=o.available, observed_at=timestamp))
    for key, previous in old.items():
        if key not in observed_keys:
            # Absence is not proof of out-of-stock. Preserve its last price and time.
            previous.missing = True
    product.last_success, product.error, product.failure_count = timestamp, None, 0
    product.next_check = later(minutes=get_settings(db)['interval_minutes'])
    product.lease_until = None


def poll_product(product_id):
    with SessionLocal() as db:
        p = db.get(Product, product_id)
        if not p or p.deleted_at or not p.tracked:
            return
        sid = p.source_id
    try:
        if settings.app_mode == 'demo':
            with SessionLocal() as db:
                rows = db.scalars(select(LatestOffer).where(LatestOffer.product_id == product_id, LatestOffer.missing == False)).all()
                offers = [Offer(o.seller_id, db.get(Seller, o.seller_id).name, o.offer_key, o.variant, max(1000, int(o.price * random.choice([1, 1, 0.98, 1.03]))) if o.price else None, o.list_price, o.available, o.comparison_key or 'demo-default') for o in rows]
        else:
            _, offers = source.product(sid)
        with SessionLocal.begin() as db:
            p = db.get(Product, product_id)
            if p and not p.deleted_at and p.tracked:
                apply_observations(db, p, offers)
            elif p:
                p.lease_until = None
    except Exception as exc:
        message = str(exc) if isinstance(exc, SourceError) else 'خطای پردازش اطلاعات؛ جزئیات در لاگ سرور ثبت شد.'
        if not isinstance(exc, SourceError):
            log.exception('Product poll failed: %s', product_id)
        with SessionLocal.begin() as db:
            p = db.get(Product, product_id)
            if p and not p.deleted_at:
                if not p.error:
                    notify(db, 'error', f'پایش {p.title} متوقف شد: {message}', p)
                p.error = message
                p.failure_count += 1
                p.next_check = later(minutes=min(360, 5 * 2 ** min(p.failure_count - 1, 6)))
                p.lease_until = None


def save_discovered(brand_id, items):
    with SessionLocal.begin() as db:
        for item in items:
            p = db.scalar(select(Product).where(Product.source_id == item['source_id']))
            if p and p.deleted_at:
                continue  # User exclusion survives future automatic discovery.
            if not p:
                p = Product(brand_id=brand_id, **item)
                db.add(p)
                db.flush()
                notify(db, 'product_found', f'محصول جدید برای انتخاب: {p.title}', p)
            elif p.brand_id == brand_id:
                p.title, p.image, p.url = item['title'], item['image'] or p.image, item['url']


def run_discovery(job_id):
    with SessionLocal() as db:
        job = db.get(Job, job_id)
        b = db.get(Brand, job.brand_id)
        aliases, brand_id = b.aliases, b.id
    try:
        def progress(items):
            save_discovered(brand_id, items)
            with SessionLocal.begin() as db:
                j = db.get(Job, job_id)
                j.lease_until = later(minutes=10)
                j.message = f'{len(items)} محصول شناسایی شد؛ جست‌وجو ادامه دارد.'
                db.merge(AppSetting(key='discovery_heartbeat', value=now()))
        if settings.app_mode == 'demo':
            items = []
            message = 'نسخهٔ نمایشی: محصولات نمونه آماده‌اند؛ جست‌وجوی واقعی انجام نشد.'
        else:
            items = source.discover(aliases, progress)
            save_discovered(brand_id, items)
            message = f'جست‌وجو کامل شد؛ {len(items)} محصول مرتبط یافت شد.'
        with SessionLocal.begin() as db:
            b = db.get(Brand, brand_id)
            b.last_discovered, b.discovery_error = now(), None
            j = db.get(Job, job_id)
            j.status, j.finished_at, j.message = 'done', now(), message
    except Exception as exc:
        message = str(exc) if isinstance(exc, SourceError) else 'جست‌وجو ناموفق بود؛ لاگ سرور بررسی شود.'
        log.warning('Discovery %s failed: %s', brand_id, type(exc).__name__)
        with SessionLocal.begin() as db:
            db.get(Brand, brand_id).discovery_error = message
            j = db.get(Job, job_id)
            j.status, j.finished_at, j.message = 'failed', now(), message
            notify(db, 'error', f'جست‌وجوی برند ناموفق بود: {message}', brand_id=brand_id)


def enqueue_discovery(db, brand_id):
    existing = db.scalar(select(Job).where(Job.brand_id == brand_id, Job.status.in_(['queued', 'running'])))
    if existing:
        return existing.id
    job = Job(id=str(uuid.uuid4()), kind='discovery', brand_id=brand_id)
    db.add(job)
    db.flush()
    return job.id


def tick():
    # Celery and embedded mode must never be enabled together. Atomic leases also
    # protect against duplicate beat messages and allow recovery after a crash.
    if not _tick_lock.acquire(blocking=False):
        return
    try:
        moment = now()
        with SessionLocal.begin() as db:
            db.merge(AppSetting(key='worker_heartbeat', value=moment))
            due = list(db.scalars(select(Product.id).where(Product.deleted_at == None, Product.tracked == True, or_(Product.next_check == None, Product.next_check <= moment), or_(Product.lease_until == None, Product.lease_until < moment)).limit(10)))
        for pid in due:
            with SessionLocal.begin() as db:
                claimed = db.execute(update(Product).where(Product.id == pid, Product.deleted_at == None, Product.tracked == True, or_(Product.lease_until == None, Product.lease_until < now())).values(lease_until=later(minutes=5), last_attempt=now())).rowcount
            if claimed:
                poll_product(pid)
        maintenance()
    finally:
        _tick_lock.release()


def discovery_tick():
    with SessionLocal.begin() as db:
        job_id = db.scalar(select(Job.id).where(or_(Job.status == 'queued', (Job.status == 'running') & (Job.lease_until < now()))).order_by(Job.created_at).limit(1))
        claimed = 0
        if job_id:
            claimed = db.execute(update(Job).where(Job.id == job_id, or_(Job.status == 'queued', (Job.status == 'running') & (Job.lease_until < now()))).values(status='running', lease_until=later(minutes=10))).rowcount
    if job_id and claimed:
        run_discovery(job_id)


def maintenance():
    with SessionLocal.begin() as db:
        config = get_settings(db)
        cutoff = later(minutes=-config['retention_days'] * 1440)
        # Delete in bounded batches so a large retention cleanup cannot hold the
        # database for an entire monitoring cycle.
        ids = select(Observation.id).where(Observation.observed_at < cutoff).limit(10000)
        db.execute(delete(Observation).where(Observation.id.in_(ids)))
        discovery_cutoff = later(minutes=-config['discovery_hours'] * 60)
        for b in db.scalars(select(Brand).where(Brand.last_discovered != None, Brand.last_discovered < discovery_cutoff)):
            last_job = db.scalar(select(Job.created_at).where(Job.brand_id == b.id).order_by(Job.created_at.desc()).limit(1))
            if not last_job or last_job < discovery_cutoff:
                enqueue_discovery(db, b.id)


def embedded_loop(stop, discovery=False):
    while not stop.is_set():
        try:
            discovery_tick() if discovery else tick()
        except Exception:
            log.exception('Scheduler tick failed')
        stop.wait(10)
