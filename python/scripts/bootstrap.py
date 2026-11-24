"""Create unique local credentials once. Never print passwords to process logs."""
from pathlib import Path
import secrets

root = Path(__file__).resolve().parent.parent
env = root / '.env'
data = root / 'data'
data.mkdir(exist_ok=True)
if not env.exists():
    password = secrets.token_urlsafe(18)
    viewer = secrets.token_urlsafe(18)
    secret = secrets.token_urlsafe(48)
    db_password = secrets.token_urlsafe(24)
    env.write_text(f'APP_MODE=live\nSESSION_SECRET={secret}\nADMIN_USERNAME=admin\nADMIN_PASSWORD={password}\nVIEWER_USERNAME=viewer\nVIEWER_PASSWORD={viewer}\nPOSTGRES_PASSWORD={db_password}\nSECURE_COOKIES=false\nEMBEDDED_SCHEDULER=true\n', encoding='utf-8')
    (data / 'local-access.txt').write_text(f'LOCAL ACCESS — keep this file private\nPanel: http://127.0.0.1:8000\nAdmin username: admin\nAdmin password: {password}\nViewer username: viewer\nViewer password: {viewer}\n\nDemo (synthetic data, separate database): http://127.0.0.1:8001\nProduction: enable HTTPS and SECURE_COOKIES=true.\n', encoding='utf-8')
    print('Local credentials created in data/local-access.txt')
else:
    print('Existing .env preserved.')
