from pathlib import Path
from pydantic_settings import BaseSettings, SettingsConfigDict

ROOT = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=ROOT / '.env', extra='ignore')
    app_mode: str = 'live'
    database_url: str = ''
    session_secret: str = ''
    admin_username: str = 'admin'
    admin_password: str = ''
    viewer_username: str = 'viewer'
    viewer_password: str = ''
    secure_cookies: bool = False
    embedded_scheduler: bool = True
    redis_url: str = 'redis://redis:6379/0'
    snappshop_proxy: str = ''
    request_delay_seconds: float = 1.0
    request_timeout_seconds: int = 25
    discovery_max_pages: int = 500
    max_report_rows: int = 200000


settings = Settings()
if settings.app_mode not in ('live', 'demo'):
    raise RuntimeError('APP_MODE must be live or demo')
(ROOT / 'data').mkdir(exist_ok=True)
DATABASE_URL = (settings.database_url if settings.app_mode == 'live' else '') or f'sqlite:///{(ROOT / "data" / (settings.app_mode + ".db")).as_posix()}'
