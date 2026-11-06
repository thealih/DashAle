import logging
import os
import threading
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from urllib.parse import urlparse
from fastapi import FastAPI, Request, Depends, HTTPException, Query
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from starlette.background import BackgroundTask
from pydantic import BaseModel, Field
from sqlalchemy import select, func, distinct, or_, update
from .config import settings, ROOT
from .db import init_db, SessionLocal, Brand, Product, Seller, LatestOffer, Observation, Notification, AppSetting, Job, get_settings, now
from .auth import current_user, admin, login, make_token
from .monitor import embedded_loop, enqueue_discovery
from .collector import source, SourceError, normalized
from .reports import create_report
from .urls import product_url
from .competition import own_store, compare_prices, STATUSES

logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(app):
    init_db()
    if settings.app_mode == 'demo':
        from .seed import seed_demo
        seed_demo()
    stop = threading.Event()
    threads = []
    if settings.embedded_scheduler:
        for discovery in (False, True):
            thread = threading.Thread(target=embedded_loop, args=(stop, discovery), daemon=True, name='brand-discovery' if discovery else 'price-monitor')
            thread.start()
            threads.append(thread)
    yield
    stop.set()
    for thread in threads:
        thread.join(timeout=2)


app = FastAPI(title='پارسا آل شریف | Price Monitor', version='1.3.0', lifespan=lifespan)


@app.middleware('http')
async def security(request, call_next):
    if request.method not in ('GET', 'HEAD', 'OPTIONS'):
        origin = request.headers.get('origin')
        if origin and urlparse(origin).netloc != request.headers.get('host'):
            return JSONResponse({'detail': 'درخواست از مبدأ غیرمجاز است.'}, status_code=403)
    response = await call_next(request)
    response.headers['X-Content-Type-Options'] = 'nosniff'
    response.headers['X-Frame-Options'] = 'DENY'
    response.headers['Referrer-Policy'] = 'strict-origin-when-cross-origin'
    if request.url.path.startswith('/api/'):
        response.headers['Cache-Control'] = 'no-store'
    return response


def database():
    with SessionLocal() as db:
        yield db


def row(obj):
    return {column.name: getattr(obj, column.name) for column in obj.__table__.columns}


def visible_notification():
    return or_(Notification.product_id == None, Notification.product_id.in_(select(Product.id).where(Product.deleted_at == None)))


def require_active_product(db, product_id):
    p = db.get(Product, product_id)
    if not p or p.deleted_at:
        raise HTTPException(404, 'محصول پیدا نشد یا حذف شده است.')
    return p


def timestamp(value):
    if value is None: return None
    try:
        d = datetime.fromisoformat(value.replace('Z', '+00:00'))
        if not d.tzinfo: raise ValueError()
        return d.astimezone(timezone.utc).isoformat(timespec='microseconds')
    except ValueError:
        raise HTTPException(422, 'زمان باید ISO 8601 و دارای منطقهٔ زمانی باشد.') from None


@app.get('/api/health')
def health(db=Depends(database)):
    db.execute(select(1))
    return {'status': 'ok', 'mode': settings.app_mode}


class Credentials(BaseModel):
    username: str = Field(max_length=100)
    password: str = Field(max_length=500)


@app.post('/api/auth/login')
def sign_in(body: Credentials, request: Request):
    user = login(body.username, body.password, request.client.host if request.client else 'unknown')
    response = JSONResponse(user)
    response.set_cookie('didban_session', make_token(**user), httponly=True, secure=settings.secure_cookies, samesite='strict', max_age=43200)
    return response


@app.post('/api/auth/logout')
def sign_out():
    response = JSONResponse({'ok': True})
    response.delete_cookie('didban_session')
    return response


@app.get('/api/me')
def me(user=Depends(current_user)):
    return {**user, 'mode': settings.app_mode}


@app.get('/api/dashboard')
def dashboard(user=Depends(current_user), db=Depends(database)):
    from zoneinfo import ZoneInfo
    day = datetime.now(ZoneInfo('Asia/Tehran')).replace(hour=0, minute=0, second=0, microsecond=0).astimezone(timezone.utc).isoformat(timespec='microseconds')
    scalar = lambda stmt: db.scalar(stmt) or 0
    heartbeat = db.get(AppSetting, 'worker_heartbeat')
    return {
        'products': scalar(select(func.count()).select_from(Product).where(Product.deleted_at == None)),
        'tracked': scalar(select(func.count()).select_from(Product).where(Product.tracked == True, Product.deleted_at == None)),
        'sellers': scalar(select(func.count(distinct(LatestOffer.seller_id))).join(Product, LatestOffer.product_id == Product.id).where(Product.tracked == True, Product.deleted_at == None, LatestOffer.missing == False)),
        'changes_today': scalar(select(func.count()).select_from(Notification).where(Notification.kind.in_(['price_up', 'price_down']), Notification.created_at >= day, visible_notification())),
        'unread': scalar(select(func.count()).select_from(Notification).where(Notification.read == False, Notification.alerts == True, visible_notification())),
        'errors': scalar(select(func.count()).select_from(Product).where(Product.tracked == True, Product.deleted_at == None, Product.error != None)),
        'last_success': db.scalar(select(func.max(Product.last_success)).where(Product.deleted_at == None)),
        'heartbeat': heartbeat.value if heartbeat else None,
        'settings': get_settings(db), 'mode': settings.app_mode,
    }


@app.get('/api/brands')
def brands(user=Depends(current_user), db=Depends(database)):
    output = []
    for b in db.scalars(select(Brand)):
        data = row(b)
        data['total'] = db.scalar(select(func.count()).select_from(Product).where(Product.brand_id == b.id, Product.deleted_at == None))
        data['tracked'] = db.scalar(select(func.count()).select_from(Product).where(Product.brand_id == b.id, Product.tracked == True, Product.deleted_at == None))
        data['changes'] = db.scalar(select(func.count()).select_from(Notification).where(Notification.brand_id == b.id, Notification.kind.in_(['price_up', 'price_down']), visible_notification()))
        job = db.scalar(select(Job).where(Job.brand_id == b.id).order_by(Job.created_at.desc()).limit(1))
        data['job'] = row(job) if job else None
        output.append(data)
    return output


@app.post('/api/brands/{brand_id}/discover', status_code=202)
def discover(brand_id: str, user=Depends(admin), db=Depends(database)):
    if not db.get(Brand, brand_id): raise HTTPException(404, 'برند پیدا نشد.')
    job_id = enqueue_discovery(db, brand_id)
    db.commit()
    return {'job_id': job_id, 'message': 'جست‌وجو در صف قرار گرفت.'}


@app.get('/api/products')
def products(brand_id: str | None = None, q: str = '', tracked: bool | None = None, deleted: bool = False, page: int = Query(1, ge=1), page_size: int = Query(12, ge=1, le=100), user=Depends(current_user), db=Depends(database)):
    if deleted and user['role'] != 'admin': raise HTTPException(403, 'مشاهدهٔ حذف‌شده‌ها به دسترسی مدیر نیاز دارد.')
    stmt = select(Product).where(Product.deleted_at != None if deleted else Product.deleted_at == None)
    if brand_id: stmt = stmt.where(Product.brand_id == brand_id)
    if tracked is not None: stmt = stmt.where(Product.tracked == tracked)
    if q: stmt = stmt.where(Product.title.contains(q, autoescape=True))
    total = db.scalar(select(func.count()).select_from(stmt.subquery()))
    result = []
    for p in db.scalars(stmt.order_by(Product.tracked.desc(), Product.created_at.desc(), Product.id).offset((page - 1) * page_size).limit(page_size)):
        # Count sellers independently of stock; a named out-of-stock seller is
        # still a seller. Never represent an uninspected catalog entry as zero.
        min_price = db.scalar(select(func.min(LatestOffer.price)).where(LatestOffer.product_id == p.id, LatestOffer.missing == False, or_(LatestOffer.available == True, LatestOffer.available == None)))
        seller_count = db.scalar(select(func.count(distinct(LatestOffer.seller_id))).where(LatestOffer.product_id == p.id, LatestOffer.missing == False))
        data = row(p)
        if p.source_id.isdigit(): data['url'] = product_url(p.source_id)
        data.update(min_price=min_price, seller_count=seller_count if p.last_success or seller_count else None)
        result.append(data)
    return {'items': result, 'total': total, 'page': page, 'page_size': page_size}


class Tracking(BaseModel):
    ids: list[int] = Field(min_length=1, max_length=1000)
    tracked: bool


@app.post('/api/products/tracking')
def track(body: Tracking, user=Depends(admin), db=Depends(database)):
    values = {'tracked': body.tracked}
    if body.tracked: values['next_check'] = now()
    count = db.execute(update(Product).where(Product.id.in_(body.ids), Product.deleted_at == None).values(**values)).rowcount
    db.commit()
    return {'updated': count}


class ProductSelection(BaseModel):
    ids: list[int] = Field(min_length=1, max_length=1000)


@app.post('/api/products/delete')
def delete_products(body: ProductSelection, user=Depends(admin), db=Depends(database)):
    count = db.execute(update(Product).where(Product.id.in_(body.ids), Product.deleted_at == None).values(deleted_at=now(), tracked=False, next_check=None)).rowcount
    db.commit()
    return {'updated': count}


@app.post('/api/products/restore')
def restore_products(body: ProductSelection, user=Depends(admin), db=Depends(database)):
    count = db.execute(update(Product).where(Product.id.in_(body.ids), Product.deleted_at != None).values(deleted_at=None, tracked=False, next_check=None)).rowcount
    db.commit()
    return {'updated': count}


class ManualProduct(BaseModel):
    brand_id: str
    url: str = Field(max_length=2000)


@app.post('/api/products', status_code=201)
def manual(body: ManualProduct, user=Depends(admin), db=Depends(database)):
    import re
    parsed = urlparse(body.url)
    match = re.match(r'^/product/(?:snp-)?(\d{3,30})(?:/|$)', parsed.path)
    if parsed.scheme != 'https' or parsed.hostname not in ('snappshop.ir', 'www.snappshop.ir') or not match:
        raise HTTPException(422, 'لینک معتبر صفحهٔ محصول اسنپ‌شاپ را وارد کنید.')
    b = db.get(Brand, body.brand_id)
    if not b: raise HTTPException(404, 'برند پیدا نشد.')
    if settings.app_mode == 'demo': raise HTTPException(422, 'افزودن لینک واقعی در نسخهٔ نمایشی فعال نیست.')
    existing = db.scalar(select(Product).where(Product.source_id == match[1]))
    if existing:
        restored = existing.deleted_at is not None
        existing.deleted_at = None
        brand_changed = existing.brand_id != b.id
        existing.brand_id = b.id
        existing.url = product_url(existing.source_id)
        existing.tracked, existing.next_check = True, now()
        db.commit()
        return {**row(existing), 'already_exists': True, 'brand_changed': brand_changed, 'restored': restored, 'message': 'محصول حذف‌شده بازیابی شد و پایش آن فعال شد.' if restored else 'این محصول قبلاً ثبت شده بود؛ پایش آن برای برند انتخاب‌شده فعال شد.'}
    try:
        data, offers = source.product(match[1])
    except SourceError as exc:
        raise HTTPException(502, str(exc)) from None
    title = data['content']['title_fa']
    brand = data.get('brand') or {}
    brand_matches = any(normalized(a) in normalized(title + ' ' + str(brand.get('title_fa', '')) + ' ' + str(brand.get('title_en', ''))) for a in b.aliases.split(','))
    images = data.get('images') or []
    p = Product(brand_id=b.id, source_id=match[1], title=title, url=product_url(match[1]), image=images[0].get('src') if images else None, tracked=True, next_check=now())
    db.add(p)
    db.flush()
    from .monitor import apply_observations, notify
    apply_observations(db, p, offers)
    notify(db, 'product_found', f'محصول با لینک به برند {b.name} اضافه و پایش شد: {p.title}', p)
    db.commit()
    return {**row(p), 'already_exists': False, 'brand_changed': False, 'brand_warning': None if brand_matches else 'برند انتخاب‌شده توسط شما اعمال شد؛ نام برند در مشخصات منبع تطبیق خودکار نداشت.', 'message': 'محصول جدید به برند انتخاب‌شده اضافه شد و پایش آن فعال است.'}


@app.get('/api/products/{product_id}')
def detail(product_id: int, user=Depends(current_user), db=Depends(database)):
    p = db.get(Product, product_id)
    if not p or p.deleted_at: raise HTTPException(404, 'محصول پیدا نشد یا حذف شده است.')
    offers = [{**row(o), 'seller_name': name} for o, name in db.execute(select(LatestOffer, Seller.name).join(Seller).where(LatestOffer.product_id == p.id).order_by(LatestOffer.missing, LatestOffer.price))]
    data = row(p)
    if p.source_id.isdigit(): data['url'] = product_url(p.source_id)
    return {**data, 'offers': offers}


@app.get('/api/products/{product_id}/preview')
def preview(product_id: int, user=Depends(current_user), db=Depends(database)):
    """Read current sellers without enrolling or fabricating monitored history."""
    p = db.get(Product, product_id)
    if not p or p.deleted_at: raise HTTPException(404, 'محصول پیدا نشد یا حذف شده است.')
    if settings.app_mode == 'demo':
        offers = [{**row(o), 'seller_name': name} for o, name in db.execute(select(LatestOffer, Seller.name).join(Seller).where(LatestOffer.product_id == p.id, LatestOffer.missing == False).order_by(LatestOffer.price))]
        return {'product_id': p.id, 'offers': offers, 'observed_at': now(), 'seller_count': len({o['seller_id'] for o in offers}), 'mode': 'demo', 'availability': 'unknown'}
    try:
        payload, fresh = source.product(p.source_id)
    except SourceError as exc:
        raise HTTPException(502, str(exc)) from None
    moment = now()
    offers = [{'id': -(i + 1), 'seller_id': o.seller_id, 'seller_name': o.seller_name, 'offer_key': o.key, 'variant': o.variant, 'price': o.price, 'list_price': o.list_price, 'available': o.available, 'observed_at': moment, 'missing': False} for i, o in enumerate(fresh)]
    offers.sort(key=lambda o: (o['price'] is None, o['price'] or 0))
    meta = {m.get('property'): m.get('content') for m in (payload.get('page') or {}).get('extra_meta') or [] if isinstance(m, dict)}
    availability = 'out_of_stock' if not offers and meta.get('product:availability') == 'out of stock' else 'unknown'
    return {'product_id': p.id, 'offers': offers, 'observed_at': moment, 'seller_count': len({o.seller_id for o in fresh}), 'mode': 'live', 'availability': availability}


@app.post('/api/products/{product_id}/check', status_code=202)
def check(product_id: int, user=Depends(admin), db=Depends(database)):
    p = db.get(Product, product_id)
    if not p or p.deleted_at: raise HTTPException(404, 'محصول پیدا نشد یا حذف شده است.')
    if not p.tracked: raise HTTPException(422, 'ابتدا پایش این محصول را فعال کنید.')
    p.next_check = now()
    db.commit()
    return {'message': 'بررسی در صف قرار گرفت.'}


@app.get('/api/products/{product_id}/history')
def history(product_id: int, seller_id: str | None = None, start: str | None = None, end: str | None = None, page: int = Query(1, ge=1), page_size: int = Query(100, ge=1, le=500), user=Depends(current_user), db=Depends(database)):
    require_active_product(db, product_id)
    stmt = select(Observation, Seller.name).join(Seller).where(Observation.product_id == product_id)
    if seller_id: stmt = stmt.where(Observation.seller_id == seller_id)
    if start: stmt = stmt.where(Observation.observed_at >= timestamp(start))
    if end: stmt = stmt.where(Observation.observed_at < timestamp(end))
    total = db.scalar(select(func.count()).select_from(stmt.subquery()))
    items = [{**row(o), 'seller_name': name} for o, name in db.execute(stmt.order_by(Observation.observed_at.desc(), Observation.id.desc()).offset((page - 1) * page_size).limit(page_size))]
    return {'items': items, 'total': total, 'page': page}


@app.get('/api/products/{product_id}/timeline')
def timeline(product_id: int, seller_id: str | None = None, user=Depends(current_user), db=Depends(database)):
    require_active_product(db, product_id)
    stmt = select(Observation.observed_at.label('time'), func.min(Observation.price).label('price')).where(Observation.product_id == product_id, Observation.price != None, or_(Observation.available == True, Observation.available == None))
    if seller_id: stmt = stmt.where(Observation.seller_id == seller_id)
    records = db.execute(stmt.group_by(Observation.observed_at).order_by(Observation.observed_at.desc()).limit(168)).mappings().all()
    return list(reversed([dict(r) for r in records]))


@app.get('/api/notifications')
def notifications(brand_id: str | None = None, product_id: int | None = None, seller_id: str | None = None, kind: str | None = None, unread: bool = False, alerts_only: bool = False, start: str | None = None, end: str | None = None, page: int = Query(1, ge=1), page_size: int = Query(20, ge=1, le=100), user=Depends(current_user), db=Depends(database)):
    stmt = select(Notification).where(visible_notification())
    if brand_id: stmt = stmt.where(Notification.brand_id == brand_id)
    if product_id: stmt = stmt.where(Notification.product_id == product_id)
    if seller_id: stmt = stmt.where(Notification.seller_id == seller_id)
    if kind: stmt = stmt.where(Notification.kind == kind)
    if unread: stmt = stmt.where(Notification.read == False)
    if alerts_only: stmt = stmt.where(Notification.alerts == True)
    if start: stmt = stmt.where(Notification.created_at >= timestamp(start))
    if end: stmt = stmt.where(Notification.created_at < timestamp(end))
    total = db.scalar(select(func.count()).select_from(stmt.subquery()))
    return {'items': [row(n) for n in db.scalars(stmt.order_by(Notification.created_at.desc(), Notification.id.desc()).offset((page - 1) * page_size).limit(page_size))], 'total': total}


class ReadNotifications(BaseModel):
    ids: list[int] | None = Field(default=None, max_length=1000)


@app.post('/api/notifications/read')
def mark_read(body: ReadNotifications, user=Depends(current_user), db=Depends(database)):
    stmt = update(Notification).where(Notification.read == False)
    if body.ids is not None: stmt = stmt.where(Notification.id.in_(body.ids))
    db.execute(stmt.values(read=True))
    db.commit()
    return {'ok': True}


class Configuration(BaseModel):
    interval_minutes: int = Field(ge=5, le=1440)
    threshold_percent: float = Field(ge=0, le=1000)
    retention_days: int = Field(ge=1, le=365)
    discovery_hours: int = Field(ge=1, le=168)


@app.get('/api/settings')
def read_settings(user=Depends(current_user), db=Depends(database)):
    return get_settings(db)


@app.put('/api/settings')
def save_settings(body: Configuration, user=Depends(admin), db=Depends(database)):
    for key, value in body.model_dump().items(): db.merge(AppSetting(key=key, value=str(value)))
    # Recalculate next checks after an interval change, without fabricating a scan.
    from datetime import timedelta
    for p in db.scalars(select(Product).where(Product.tracked == True)):
        if p.last_success and not p.error:
            p.next_check = (datetime.fromisoformat(p.last_success) + timedelta(minutes=body.interval_minutes)).isoformat(timespec='microseconds')
    db.commit()
    return body


@app.get('/api/sellers')
def sellers(user=Depends(current_user), db=Depends(database)):
    return [row(s) for s in db.scalars(select(Seller).order_by(Seller.name))]


class OwnStoreConfiguration(BaseModel):
    seller_id: str = Field(default='', max_length=100, pattern=r'^[A-Za-z0-9_-]*$')
    seller_name: str = Field(default='', max_length=300)
    undercut_toman: int = Field(default=1, ge=1, le=1000000000)


@app.get('/api/settings/own-store')
def read_own_store(user=Depends(current_user), db=Depends(database)):
    return own_store(db)


@app.put('/api/settings/own-store')
def save_own_store(body: OwnStoreConfiguration, user=Depends(admin), db=Depends(database)):
    seller = db.get(Seller, body.seller_id) if body.seller_id else None
    name = seller.name if seller else body.seller_name.strip()
    if body.seller_id and not name:
        raise HTTPException(422, 'برای شناسه‌ای که هنوز شناسایی نشده، نام فروشگاه را هم وارد کنید.')
    for key, value in [('own_seller_id', body.seller_id), ('own_seller_name', name if body.seller_id else ''), ('undercut_toman', str(body.undercut_toman))]:
        db.merge(AppSetting(key=key, value=value))
    db.commit()
    return own_store(db)


@app.get('/api/price-position')
def price_position(brand_id: str | None = None, q: str = Query('', max_length=300), status: str = 'not_best', page: int = Query(1, ge=1), page_size: int = Query(20, ge=1, le=100), user=Depends(current_user), db=Depends(database)):
    if status not in STATUSES | {'all'}: raise HTTPException(422, 'وضعیت مقایسه معتبر نیست.')
    return compare_prices(db, brand_id, q, status, page, page_size)


@app.get('/api/price-position/excel')
def price_position_excel(brand_id: str | None = None, q: str = Query('', max_length=300), status: str = 'not_best', user=Depends(current_user), db=Depends(database)):
    if status not in STATUSES | {'all'}: raise HTTPException(422, 'وضعیت مقایسه معتبر نیست.')
    result = compare_prices(db, brand_id, q, status, page_size=settings.max_report_rows)
    if not result['configured']: raise HTTPException(422, 'ابتدا فروشگاه پیش‌فرض را در تنظیمات انتخاب کنید.')
    if result['total'] > settings.max_report_rows: raise HTTPException(413, 'تعداد ردیف‌ها زیاد است؛ برند یا عبارت جست‌وجو را محدود کنید.')
    from .reports import create_position_report
    path = create_position_report(result)
    return FileResponse(path, media_type='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', filename=f'price-position-{settings.app_mode}.xlsx', background=BackgroundTask(os.unlink, path))


@app.get('/api/jobs')
def jobs(user=Depends(current_user), db=Depends(database)):
    return [row(j) for j in db.scalars(select(Job).order_by(Job.created_at.desc()).limit(30))]


@app.get('/api/reports/excel')
def export(brand_id: str | None = None, product_id: int | None = None, seller_id: str | None = None, start: str | None = None, end: str | None = None, include_history: bool = True, user=Depends(current_user), db=Depends(database)):
    start, end = timestamp(start), timestamp(end)
    if start and end and start >= end: raise HTTPException(422, 'پایان بازه باید بعد از شروع باشد.')
    path = create_report(db, brand_id, product_id, seller_id, start, end, include_history)
    return FileResponse(path, media_type='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', filename=f'didban-{settings.app_mode}-{datetime.now().strftime("%Y%m%d-%H%M")}.xlsx', background=BackgroundTask(os.unlink, path))


out = ROOT / 'frontend' / 'out'
if out.exists():
    app.mount('/', StaticFiles(directory=out, html=True), name='panel')
