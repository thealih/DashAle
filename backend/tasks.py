from celery import Celery
from .config import settings

celery = Celery('didban', broker=settings.redis_url)
celery.conf.update(timezone='UTC', enable_utc=True, broker_connection_retry_on_startup=True, worker_prefetch_multiplier=1, task_ignore_result=True, task_routes={'didban.tick': {'queue': 'prices'}, 'didban.discover': {'queue': 'discovery'}}, beat_schedule={'monitor-due-products': {'task': 'didban.tick', 'schedule': 10.0}, 'discover-brand-products': {'task': 'didban.discover', 'schedule': 10.0}})


@celery.task(name='didban.tick', expires=30)
def monitor_tick():
    from .db import init_db
    from .monitor import tick
    init_db()
    tick()


@celery.task(name='didban.discover', expires=30)
def discovery_tick():
    from .db import init_db
    from .monitor import discovery_tick as discover
    init_db()
    discover()
