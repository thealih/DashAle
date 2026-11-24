"""Runtime XLSX export, using a streaming writer for large monitoring datasets."""
import os
import tempfile
from datetime import datetime
from zoneinfo import ZoneInfo
from openpyxl import Workbook
from openpyxl.cell import WriteOnlyCell
from openpyxl.styles import Font, PatternFill, Alignment
from openpyxl.worksheet.table import Table
from sqlalchemy import select, func
from fastapi import HTTPException
from .config import settings
from .db import Product, Brand, Seller, LatestOffer, Observation, Notification, now
from .urls import product_url

TEHRAN = ZoneInfo('Asia/Tehran')


def local_time(value):
    if not value: return ''
    return datetime.fromisoformat(value).astimezone(TEHRAN).strftime('%Y-%m-%d %H:%M:%S')


def source_link(product):
    return product_url(product.source_id) if product.source_id.isdigit() else product.url


def sheet(wb, name, headers, widths):
    ws = wb.create_sheet(name)
    ws.sheet_view.rightToLeft = True
    ws.freeze_panes = 'C5'
    ws.sheet_properties.pageSetUpPr.fitToPage = True
    ws.page_setup.orientation = 'landscape'
    ws.page_setup.paperSize = '8'  # ECMA-376 A3; write-only sheets omit constants.
    ws.page_setup.fitToWidth = 1
    ws.page_setup.fitToHeight = 0
    ws.print_title_rows = '1:4'
    ws.sheet_view.showGridLines = False
    from openpyxl.utils import get_column_letter
    for i, width in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = width
    ws.append([f'پارسا آل شریف | {name} | قیمت‌ها به تومان'])
    ws.append([f'تهیه: {local_time(now())} (زمان تهران) · {"داده‌های ساختگی نمایشی" if settings.app_mode == "demo" else "داده‌های ثبت‌شده از اسنپ‌شاپ"}'])
    ws.append(['زمان‌ها زمان مشاهده هستند؛ دادهٔ قدیمی با زمان آخرین مشاهده مشخص می‌شود.'])
    cells = []
    for title in headers:
        c = WriteOnlyCell(ws, title)
        c.font = Font(name='Tahoma', bold=True, color='FFFFFF', size=11)
        c.fill = PatternFill('solid', fgColor='183C36')
        c.alignment = Alignment(horizontal='right', vertical='center')
        cells.append(c)
    ws.append(cells)
    return ws


def append(ws, values):
    cells = []
    for value in values:
        c = WriteOnlyCell(ws, value)
        # Explicit string typing prevents formula injection through source titles.
        if isinstance(value, str):
            c.data_type = 's'
        c.font = Font(name='Tahoma', size=10)
        c.alignment = Alignment(horizontal='right', vertical='center')
        if isinstance(value, (float, int)):
            c.number_format = '#,##0.##;[Red](#,##0.##)'
        cells.append(c)
    ws.append(cells)


def create_position_report(result):
    wb = Workbook(write_only=True)
    ws = sheet(wb, 'مقایسه فروشگاه ما', ['برند', 'محصول', 'گونه / گارانتی', 'فروشگاه ما', 'قیمت ما (تومان)', 'ارزان‌ترین رقیب', 'قیمت رقیب (تومان)', 'اختلاف با رقیب (تومان)', 'قیمت پیشنهادی (تومان)', 'کاهش لازم (تومان)', 'وضعیت', 'مشاهده قیمت ما (تهران)', 'مشاهده قیمت رقیب (تهران)', 'لینک محصول'], [20, 65, 45, 30, 23, 32, 23, 24, 25, 23, 42, 28, 28, 65])
    count = 4
    for row in result['items']:
        append(ws, [row['brand_name'], row['product_title'], row['variant'], result['store']['seller_name'], row['own_price'], row['competitor_name'], row['competitor_price'], row['difference'], row['target_price'], row['reduction_needed'], row['status_label'], local_time(row['own_observed_at']), local_time(row['competitor_observed_at']), row['url']])
        count += 1
    ws.auto_filter.ref = f'A4:N{count}'
    fd, path = tempfile.mkstemp(suffix='.xlsx')
    os.close(fd)
    try:
        wb.save(path)
    except Exception:
        os.unlink(path)
        raise
    return path


def create_report(db, brand_id=None, product_id=None, seller_id=None, start=None, end=None, include_history=True):
    conditions = [Product.deleted_at == None]
    if brand_id: conditions.append(Product.brand_id == brand_id)
    if product_id: conditions.append(Product.id == product_id)
    latest = select(LatestOffer, Product, Brand.name, Seller.name).join(Product, LatestOffer.product_id == Product.id).join(Brand, Product.brand_id == Brand.id).join(Seller, LatestOffer.seller_id == Seller.id).where(*conditions)
    history = select(Observation, Product, Brand.name, Seller.name).join(Product, Observation.product_id == Product.id).join(Brand, Product.brand_id == Brand.id).join(Seller, Observation.seller_id == Seller.id).where(*conditions)
    changes = select(Notification, Product, Brand.name, Seller.name).join(Product, Notification.product_id == Product.id).join(Brand, Product.brand_id == Brand.id).outerjoin(Seller, Notification.seller_id == Seller.id).where(*conditions, Notification.kind.in_(['price_up', 'price_down']))
    if seller_id:
        latest = latest.where(LatestOffer.seller_id == seller_id)
        history = history.where(Observation.seller_id == seller_id)
        changes = changes.where(Notification.seller_id == seller_id)
    for column, time_value, op in [(Observation.observed_at, start, 'start'), (Observation.observed_at, end, 'end')]:
        if time_value: history = history.where(column >= time_value if op == 'start' else column < time_value)
    if start: changes = changes.where(Notification.created_at >= start)
    if end: changes = changes.where(Notification.created_at < end)
    # The latest sheet always represents now; the selected range applies to history/events.
    counts = [db.scalar(select(func.count()).select_from(q.subquery())) for q in ([latest, changes, history] if include_history else [latest, changes])]
    if sum(counts) > settings.max_report_rows:
        raise HTTPException(413, f'گزارش بیش از {settings.max_report_rows:,} ردیف است. بازه یا فیلتر را محدود کنید؛ هیچ ردیفی حذف نشده است.')
    wb = Workbook(write_only=True)
    summary = sheet(wb, 'خلاصه مدیریتی', ['شاخص', 'مقدار'], [55, 55])
    append(summary, ['برند', brand_id or 'همه برندها'])
    append(summary, ['بازهٔ تاریخچه از (زمان تهران)', local_time(start) or 'ابتدای سابقه'])
    append(summary, ['بازهٔ تاریخچه تا (انحصاری)', local_time(end) or 'اکنون'])
    append(summary, ['ردیف آخرین پیشنهادهای فروش', counts[0]])
    append(summary, ['تغییرات قیمت در بازه', counts[1]])
    append(summary, ['مشاهدات در بازه', counts[2] if include_history else 'تاریخچه درخواست نشده'])
    append(summary, ['مبنای آخرین قیمت‌ها', 'آخرین مشاهدهٔ ثبت‌شده، مستقل از بازهٔ تاریخچه'])
    append(summary, ['محدودهٔ قیمت‌های منبع', 'تومان؛ مقصد تهران؛ هزینهٔ ارسال و کوپن شخصی لحاظ نشده'])
    append(summary, ['نوع داده', 'ساختگی — فقط برای نمایش' if settings.app_mode == 'demo' else 'واقعی؛ آخرین زمان مشاهده را بررسی کنید'])
    headers = ['برند', 'محصول', 'شناسهٔ منبع', 'فروشنده', 'شناسهٔ فروشنده', 'گونه / گارانتی', 'قیمت فروش (تومان)', 'قیمت پایه (تومان)', 'وضعیت موجودی', 'زمان مشاهده (تهران)', 'وضعیت دریافت', 'لینک منبع']
    ws = sheet(wb, 'آخرین قیمت‌ها', headers, [18, 65, 20, 30, 22, 45, 24, 24, 22, 28, 30, 60])
    count = 4
    for o, p, brand, seller in db.execute(latest.order_by(Product.title, Seller.name).execution_options(yield_per=1000)):
        append(ws, [brand, p.title, p.source_id, seller, o.seller_id, o.variant, o.price, o.list_price, 'موجود' if o.available else ('ناموجود' if o.available is False else 'نامشخص'), local_time(o.observed_at), 'در آخرین دریافت دیده نشد' if o.missing else ('خطای آخرین پایش' if p.error else 'ثبت موفق'), source_link(p)])
        count += 1
    ws.auto_filter.ref = f'A4:L{count}'
    ws = sheet(wb, 'تغییرات قیمت', ['برند', 'محصول', 'فروشنده', 'شرح / گونه', 'قیمت قبلی (تومان)', 'قیمت جدید (تومان)', 'اختلاف (تومان)', 'درصد تغییر', 'مشاهده قبلی (تهران)', 'تشخیص تغییر (تهران)'], [18, 65, 30, 70, 24, 24, 24, 18, 28, 28])
    count = 4
    for n, p, brand, seller in db.execute(changes.order_by(Notification.created_at.desc()).execution_options(yield_per=1000)):
        append(ws, [brand, p.title, seller, n.title, n.old_price, n.new_price, n.new_price - n.old_price, round(n.percent, 2), local_time(n.previous_observed_at), local_time(n.created_at)])
        count += 1
    ws.auto_filter.ref = f'A4:J{count}'
    if include_history:
        ws = sheet(wb, 'تاریخچه', headers[:-2] + ['لینک منبع'], [18, 65, 20, 30, 22, 45, 24, 24, 22, 28, 60])
        count = 4
        for o, p, brand, seller in db.execute(history.order_by(Observation.observed_at.desc()).execution_options(yield_per=1000)):
            append(ws, [brand, p.title, p.source_id, seller, o.seller_id, o.variant, o.price, o.list_price, 'موجود' if o.available else ('ناموجود' if o.available is False else 'نامشخص'), local_time(o.observed_at), source_link(p)])
            count += 1
        ws.auto_filter.ref = f'A4:K{count}'
    fd, path = tempfile.mkstemp(suffix='.xlsx')
    os.close(fd)
    try:
        wb.save(path)
    except Exception:
        os.unlink(path)
        raise
    return path
