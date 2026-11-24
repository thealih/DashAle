from datetime import datetime, timezone
from sqlalchemy import create_engine, event, String, Text, Boolean, Float, Integer, BigInteger, ForeignKey, UniqueConstraint, Index, inspect, text, select, update
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, sessionmaker
from .config import DATABASE_URL


def now():
    return datetime.now(timezone.utc).isoformat(timespec='microseconds')


engine = create_engine(DATABASE_URL, connect_args={'check_same_thread': False, 'timeout': 30} if DATABASE_URL.startswith('sqlite') else {}, pool_pre_ping=True)
if DATABASE_URL.startswith('sqlite'):
    @event.listens_for(engine, 'connect')
    def sqlite_setup(connection, _):
        connection.execute('PRAGMA foreign_keys=ON')
        connection.execute('PRAGMA journal_mode=WAL')

SessionLocal = sessionmaker(engine, expire_on_commit=False)


class Base(DeclarativeBase):
    pass


class Brand(Base):
    __tablename__ = 'brands'
    id: Mapped[str] = mapped_column(String(40), primary_key=True)
    name: Mapped[str] = mapped_column(String(80))
    english: Mapped[str] = mapped_column(String(80))
    aliases: Mapped[str] = mapped_column(Text)
    last_discovered: Mapped[str | None] = mapped_column(String(40))
    discovery_error: Mapped[str | None] = mapped_column(Text)


class Product(Base):
    __tablename__ = 'products'
    id: Mapped[int] = mapped_column(primary_key=True)
    brand_id: Mapped[str] = mapped_column(ForeignKey('brands.id'), index=True)
    source_id: Mapped[str] = mapped_column(String(80), unique=True)
    title: Mapped[str] = mapped_column(String(600))
    url: Mapped[str] = mapped_column(Text)
    image: Mapped[str | None] = mapped_column(Text)
    tracked: Mapped[bool] = mapped_column(Boolean, default=False, index=True)
    deleted_at: Mapped[str | None] = mapped_column(String(40))
    created_at: Mapped[str] = mapped_column(String(40), default=now)
    last_attempt: Mapped[str | None] = mapped_column(String(40))
    last_success: Mapped[str | None] = mapped_column(String(40))
    next_check: Mapped[str | None] = mapped_column(String(40), index=True)
    lease_until: Mapped[str | None] = mapped_column(String(40))
    error: Mapped[str | None] = mapped_column(Text)
    failure_count: Mapped[int] = mapped_column(Integer, default=0)


class Seller(Base):
    __tablename__ = 'sellers'
    id: Mapped[str] = mapped_column(String(100), primary_key=True)
    name: Mapped[str] = mapped_column(String(300))


class LatestOffer(Base):
    __tablename__ = 'latest_offers'
    __table_args__ = (UniqueConstraint('product_id', 'offer_key'),)
    id: Mapped[int] = mapped_column(primary_key=True)
    product_id: Mapped[int] = mapped_column(ForeignKey('products.id'), index=True)
    seller_id: Mapped[str] = mapped_column(ForeignKey('sellers.id'), index=True)
    offer_key: Mapped[str] = mapped_column(String(200))
    comparison_key: Mapped[str | None] = mapped_column(String(200))
    variant: Mapped[str] = mapped_column(String(500), default='پیش‌فرض')
    price: Mapped[int | None] = mapped_column(BigInteger)
    list_price: Mapped[int | None] = mapped_column(BigInteger)
    available: Mapped[bool | None] = mapped_column(Boolean)
    observed_at: Mapped[str] = mapped_column(String(40))
    missing: Mapped[bool] = mapped_column(Boolean, default=False)


class Observation(Base):
    __tablename__ = 'observations'
    __table_args__ = (Index('ix_history_product_time', 'product_id', 'observed_at'),)
    id: Mapped[int] = mapped_column(primary_key=True)
    product_id: Mapped[int] = mapped_column(ForeignKey('products.id'))
    seller_id: Mapped[str] = mapped_column(ForeignKey('sellers.id'))
    offer_key: Mapped[str] = mapped_column(String(200))
    variant: Mapped[str] = mapped_column(String(500))
    price: Mapped[int | None] = mapped_column(BigInteger)
    list_price: Mapped[int | None] = mapped_column(BigInteger)
    available: Mapped[bool | None] = mapped_column(Boolean)
    observed_at: Mapped[str] = mapped_column(String(40), index=True)


class Notification(Base):
    __tablename__ = 'notifications'
    id: Mapped[int] = mapped_column(primary_key=True)
    kind: Mapped[str] = mapped_column(String(40), index=True)
    brand_id: Mapped[str | None] = mapped_column(String(40), index=True)
    product_id: Mapped[int | None] = mapped_column(ForeignKey('products.id'), index=True)
    seller_id: Mapped[str | None] = mapped_column(String(100))
    title: Mapped[str] = mapped_column(Text)
    old_price: Mapped[int | None] = mapped_column(BigInteger)
    new_price: Mapped[int | None] = mapped_column(BigInteger)
    percent: Mapped[float | None] = mapped_column(Float)
    previous_observed_at: Mapped[str | None] = mapped_column(String(40))
    created_at: Mapped[str] = mapped_column(String(40), default=now, index=True)
    read: Mapped[bool] = mapped_column(Boolean, default=False)
    alerts: Mapped[bool] = mapped_column(Boolean, default=True)


class AppSetting(Base):
    __tablename__ = 'settings'
    key: Mapped[str] = mapped_column(String(80), primary_key=True)
    value: Mapped[str] = mapped_column(Text)


class Job(Base):
    __tablename__ = 'jobs'
    id: Mapped[str] = mapped_column(String(40), primary_key=True)
    kind: Mapped[str] = mapped_column(String(40))
    brand_id: Mapped[str | None] = mapped_column(String(40))
    status: Mapped[str] = mapped_column(String(40), default='queued')
    created_at: Mapped[str] = mapped_column(String(40), default=now)
    finished_at: Mapped[str | None] = mapped_column(String(40))
    lease_until: Mapped[str | None] = mapped_column(String(40))
    message: Mapped[str | None] = mapped_column(Text)


BRANDS = [
    ('vitalayer', 'ویتالیر', 'Vitalayer', 'ویتالیر,ویتالایر,vitalayer'),
    ('eudoramax', 'ادورامکس', 'Eudora Max', 'ادورامکس,ادورا مکس,eudora max,eudoramax'),
    ('elvina', 'الوینا', 'Elvina', 'الوینا,elvina'),
    ('isol', 'آیسول', 'Isol', 'آیسول,ایسول,isol'),
    ('vitaplex', 'ویتاپلکس', 'Vitaplex', 'ویتاپلکس,ویتا پلکس,vitaplex'),
    ('belfamed', 'بلفامد', 'Belfamed', 'بلفامد,belfamed'),
    ('bergamia', 'برگامیا', 'Bergamia', 'برگامیا,bergamia'),
]


def init_db():
    Base.metadata.create_all(engine)
    if 'deleted_at' not in {c['name'] for c in inspect(engine).get_columns('products')}:
        with engine.begin() as connection:
            connection.execute(text('ALTER TABLE products ADD COLUMN deleted_at VARCHAR(40)'))
    # Additive upgrade: historical prices and observations remain intact. Old
    # offers stay unclassified until a fresh source response supplies identity.
    upgraded = 'comparison_key' not in {c['name'] for c in inspect(engine).get_columns('latest_offers')}
    if upgraded:
        with engine.begin() as connection:
            connection.execute(text('ALTER TABLE latest_offers ADD COLUMN comparison_key VARCHAR(200)'))
    with SessionLocal.begin() as db:
        if upgraded:
            db.execute(update(Product).where(Product.tracked == True, Product.id.in_(select(LatestOffer.product_id).where(LatestOffer.comparison_key == None))).values(next_check=now()))
        for id_, name, english, aliases in BRANDS:
            if not db.get(Brand, id_):
                db.add(Brand(id=id_, name=name, english=english, aliases=aliases))
        for key, value in [('interval_minutes', '60'), ('threshold_percent', '0'), ('retention_days', '30'), ('discovery_hours', '24')]:
            if not db.get(AppSetting, key):
                db.add(AppSetting(key=key, value=value))
        # Repair existing installations too; do not touch synthetic demo links.
        from .urls import product_url
        for p in db.query(Product).filter(Product.source_id.notlike('demo-%')):
            if p.source_id.isdigit():
                p.url = product_url(p.source_id)


def get_settings(db):
    return {s.key: float(s.value) for s in db.query(AppSetting).all() if s.key in ('interval_minutes', 'threshold_percent', 'retention_days', 'discovery_hours')}
