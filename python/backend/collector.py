"""Read-only Snapp! Shop adapter. No seller cap; unknown/partial schemas fail closed.

Protocol research: https://github.com/sepehr071/snappshop-mcp (MIT).
This implementation does not use the MCP tool's 25-offer output limit.
"""
import hashlib
import json
import re
import threading
import time
import uuid
from dataclasses import dataclass
from curl_cffi import requests
from .config import settings
from .urls import product_url


class SourceError(Exception):
    pass


@dataclass
class Offer:
    seller_id: str
    seller_name: str
    key: str
    variant: str
    price: int | None
    list_price: int | None
    available: bool | None
    comparison_key: str | None = None


def normalized(s):
    return re.sub(r'[\s\u200c\u200fـ_-]+', '', str(s).lower().replace('ي', 'ی').replace('ك', 'ک').replace('آ', 'ا'))


def parse_product(payload):
    d = payload.get('data')
    if not isinstance(d, dict) or not isinstance(d.get('variants'), list) or not isinstance(d.get('vendors'), list):
        raise SourceError('ساختار پاسخ محصول تغییر کرده است؛ قیمت‌ها به‌روزرسانی نشدند.')
    if not (d.get('content') or {}).get('title_fa'):
        raise SourceError('عنوان معتبر محصول در پاسخ دریافت نشد.')
    if d.get('next') or (d.get('pagination') or {}).get('next'):
        raise SourceError('پاسخ فروشندگان صفحه‌بندی دارد؛ ثبت ناقص متوقف شد.')
    sellers = {str(v['id']): v.get('title') for v in d['vendors'] if v.get('id') is not None}
    if not d['variants'] and sellers:
        raise SourceError('نام فروشندگان دریافت شد اما جزئیات پیشنهادهای فروش ناقص است؛ بررسی مجدد لازم است.')
    attributes = {str(a['value']['id']): f"{a.get('title', '')}: {a['value'].get('title', '')}" for a in d.get('configurable_attribute') or [] if a.get('value') and a['value'].get('id') is not None}
    warranties = {str(w['id']): w.get('name', '') for w in d.get('warranties') or [] if w.get('id') is not None}
    offers = []
    keys = set()
    for var in d['variants']:
        if not isinstance(var.get('vendor'), list):
            raise SourceError('فهرست فروشندگان یکی از گونه‌های محصول ناقص است.')
        expected = var.get('active_vendors')
        if isinstance(expected, int) and expected > len({v.get('vendor_id') for v in var['vendor']}):
            raise SourceError(f'پاسخ فروشندگان کامل نیست ({len(var["vendor"])} پیشنهاد در برابر {expected} فروشندهٔ اعلام‌شده). ثبت قیمت ناقص انجام نشد.')
        attr_ids = sorted(str(a['attribute_value_id']) for a in var.get('attribute_ids') or [] if 'attribute_value_id' in a)
        variant_name = '، '.join(attributes.get(a, a) for a in attr_ids) or 'پیش‌فرض'
        for v in var['vendor']:
            sid = str(v.get('vendor_id', ''))
            if sid not in sellers or not sellers[sid]:
                raise SourceError('شناسه یا نام یکی از فروشندگان مشخص نیست؛ ثبت ناقص متوقف شد.')
            price = v.get('special_price') or v.get('price')
            stock = v.get('stock')
            if stock is not None and (isinstance(stock, bool) or not isinstance(stock, (int, float)) or stock < 0):
                raise SourceError('ساختار موجودی فروشنده تغییر کرده است.')
            if price is not None and (isinstance(price, bool) or not isinstance(price, (int, float)) or price <= 0 or int(price) != price):
                raise SourceError('قیمت نامعتبر از منبع دریافت شد.')
            if price is None and stock != 0:
                raise SourceError('پیشنهاد فروش بدون قیمت معتبر دریافت شد.')
            list_price = v.get('price')
            if list_price is not None and (isinstance(list_price, bool) or not isinstance(list_price, (int, float)) or list_price < 0):
                raise SourceError('قیمت پایه نامعتبر است.')
            identity = [sid, attr_ids or str(var.get('variation_id', var.get('id', 'default'))), str(v.get('warranty_id', ''))]
            key = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()
            if key in keys:
                raise SourceError('پیشنهادهای تکراری با مشخصات یکسان دریافت شد؛ نیاز به بررسی ساختار منبع است.')
            keys.add(key)
            warranty = warranties.get(str(v.get('warranty_id', '')), '')
            comparison_key = hashlib.sha256(json.dumps(identity[1:], sort_keys=True).encode()).hexdigest()
            offers.append(Offer(sid, sellers[sid], key, variant_name + (f' · {warranty}' if warranty else ''), int(price) if price else None, int(list_price) if list_price else None, stock > 0 if stock is not None else None, comparison_key))
    return d, offers


class Snappshop:
    def __init__(self):
        self._session = requests.Session(impersonate='chrome', trust_env=False)
        self._headers = {'Accept': '*/*', 'Accept-Language': 'fa-IR,fa;q=0.9', 'Origin': 'https://snappshop.ir', 'Referer': 'https://snappshop.ir/', 's-device': 'DESKTOP', 's-device-source': 'shop', 's-area-ids': '29,197', 'x-origin': 'https://snappshop.ir', 's-session-id': str(uuid.uuid4()), 'uuid': str(uuid.uuid4())}
        self._token = None
        self._lock = threading.Lock()
        self._last = 0.0

    def _request(self, path, body=None, token=True):
        url = 'https://apix.snappshop.ir/' + path
        for attempt in range(3):
            wait = settings.request_delay_seconds - (time.monotonic() - self._last)
            if wait > 0:
                time.sleep(wait)
            self._last = time.monotonic()
            try:
                r = self._session.request('GET' if body is None else 'POST', url, params={'lat': '35.77331', 'lng': '51.418591'}, json=body, headers={**self._headers, **({'Authorization': f'Bearer {self._token}'} if token else {})}, proxy=settings.snappshop_proxy or None, timeout=settings.request_timeout_seconds, allow_redirects=False)
            except requests.RequestsError:
                if attempt < 2:
                    time.sleep(2 ** attempt)
                    continue
                raise SourceError('اتصال به اسنپ‌شاپ برقرار نشد. اینترنت سرور و دسترسی با IP ایران را بررسی کنید.') from None
            if r.status_code == 403:
                raise SourceError('اسنپ‌شاپ دسترسی این شبکه را رد کرد (۴۰۳). سرور باید با IP ایران و بدون VPN خارجی اجرا شود.')
            if r.status_code == 401 and token and attempt < 2:
                self._token = self._request('guest/v2/token', token=False).get('data', {}).get('token')
                continue
            if r.status_code == 429 or r.status_code >= 500:
                if attempt < 2:
                    try:
                        retry = float(r.headers.get('Retry-After', 2 ** (attempt + 1)))
                    except ValueError:
                        retry = 4
                    time.sleep(min(max(retry, 1), 30))
                    continue
            if r.status_code != 200:
                raise SourceError(f'دریافت اطلاعات از اسنپ‌شاپ ناموفق بود (HTTP {r.status_code}).')
            if len(r.content) > 12_000_000:
                raise SourceError('حجم پاسخ منبع بیش از حد مجاز است.')
            try:
                data = r.json()
            except ValueError:
                raise SourceError('پاسخ اسنپ‌شاپ JSON معتبر نیست.') from None
            if not isinstance(data, dict):
                raise SourceError('ساختار پاسخ منبع معتبر نیست.')
            return data
        raise SourceError('دریافت اطلاعات پس از چند تلاش ناموفق بود.')

    def fetch(self, path, body=None):
        with self._lock:
            if not self._token:
                self._token = self._request('guest/v2/token', token=False).get('data', {}).get('token')
                if not self._token:
                    raise SourceError('توکن بازدیدکننده از اسنپ‌شاپ دریافت نشد.')
            return self._request(path, body)

    def product(self, source_id):
        if not re.fullmatch(r'\d{3,30}', source_id):
            raise SourceError('شناسهٔ محصول اسنپ‌شاپ معتبر نیست.')
        return parse_product(self.fetch(f'products/v2/{source_id}'))

    def discover(self, aliases, on_page=None):
        results = {}
        for query in aliases.split(','):
            seen_pages = set()
            for page in range(1, settings.discovery_max_pages + 1):
                response = self.fetch('search/v1', {'render': 4, 'query': query.strip(), **({'skip': page - 1} if page > 1 else {})})
                section = next((s for s in (response.get('data') or {}).get('structure', []) if s.get('section_type') == 'plp'), None)
                if section is None or not isinstance(section.get('items'), list):
                    raise SourceError('ساختار نتایج جست‌وجوی اسنپ‌شاپ قابل تشخیص نیست.')
                items = section['items']
                signature = tuple(i.get('href', '') for i in items)
                if signature in seen_pages and items:
                    raise SourceError('صفحه‌بندی جست‌وجو تکراری است؛ جست‌وجوی کامل تأیید نشد.')
                seen_pages.add(signature)
                for item in items:
                    # The source falls back to unrelated products when no match exists.
                    if not any(normalized(a) in normalized(item.get('title', '')) for a in aliases.split(',')):
                        continue
                    match = re.search(r'/product/(?:snp-)?(\d+)', item.get('href', ''))
                    if match:
                        image = item.get('image') or item.get('images')
                        if isinstance(image, dict): image = image.get('src') or image.get('url')
                        if isinstance(image, list): image = image[0] if image else None
                        if isinstance(image, dict): image = image.get('src')
                        results[match[1]] = {'source_id': match[1], 'title': item['title'], 'url': product_url(match[1]), 'image': image if isinstance(image, str) and image.startswith('https://') else None}
                if on_page:
                    on_page(list(results.values()))
                pages = (section.get('pagination') or {}).get('total_pages')
                if not items or (isinstance(pages, int) and page >= pages):
                    break
                if pages is None:
                    raise SourceError('اطلاعات صفحه‌بندی جست‌وجو دریافت نشد.')
            else:
                raise SourceError('سقف صفحات جست‌وجو رسید؛ فهرست کامل تأیید نشد.')
        return list(results.values())


source = Snappshop()
