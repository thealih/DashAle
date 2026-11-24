"""Synthetic data for an explicitly isolated demo database only."""
import hashlib
import math
from datetime import datetime, timedelta, timezone
from sqlalchemy import select, func
from .config import settings
from .db import SessionLocal, Brand, Product, Seller, LatestOffer, Observation, Notification
from .collector import Offer
from .monitor import apply_observations, later


TITLES = [
    ('سرم آبرسان هیالورونیک اسید', '۳۰ میلی‌لیتر', 'serum'),
    ('ژل شست‌وشوی صورت پوست چرب', '۲۰۰ میلی‌لیتر', 'cleanser'),
    ('کرم ضد آفتاب بی‌رنگ SPF50', '۴۰ میلی‌لیتر', 'sunscreen'),
    ('کرم مرطوب‌کننده پوست خشک', '۵۰ میلی‌لیتر', 'cream'),
    ('میسلار واتر پاک‌کننده صورت', '۲۵۰ میلی‌لیتر', 'water'),
    ('کرم دور چشم ضد چروک', '۱۵ میلی‌لیتر', 'eye'),
]


def seed_demo():
    if settings.app_mode != 'demo':
        raise RuntimeError('Demo data is forbidden in a live database')
    with SessionLocal.begin() as db:
        if db.scalar(select(func.count()).select_from(Product)):
            return
        for bi, brand in enumerate(db.scalars(select(Brand))):
            for i, (title, volume, _) in enumerate(TITLES):
                p = Product(brand_id=brand.id, source_id=f'demo-{brand.id}-{i}', title=f'{title} {brand.name} حجم {volume}', url='', tracked=i < 4, created_at=later(minutes=-3000 + bi * 7 + i))
                db.add(p)
                db.flush()
                base = 280000 + bi * 67000 + i * 83000
                for hour in range(24):
                    time = (datetime.now(timezone.utc) - timedelta(hours=23-hour)).isoformat(timespec='microseconds')
                    offers = []
                    for si, name in enumerate(['فروشگاه نمونه آفتاب', 'داروخانه نمونه سلامت', 'فروشگاه نمونه سپید', 'داروخانه نمونه بهار', 'فروشگاه نمونه آوا']):
                        sid = f'demo-seller-{si}'
                        key = hashlib.sha256(f'{p.id}-{sid}-default'.encode()).hexdigest()
                        price = int((base + si * 18000 + math.sin(hour / 3 + i) * 15000) / 1000) * 1000
                        offers.append(Offer(sid, name, key, 'پیش‌فرض', price, int(base * 1.3), True, 'demo-default'))
                    apply_observations(db, p, offers, time)
                p.next_check = later(minutes=60)
            brand.last_discovered = later(minutes=-30)
        # Most historical demo events are read. Keep the recent feed manageable.
        db.flush()
        for n in db.scalars(select(Notification)):
            n.read = n.created_at < later(minutes=-65)
