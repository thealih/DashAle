import base64
import hashlib
import hmac
import json
import secrets
import time
from collections import OrderedDict
from fastapi import HTTPException, Request
from .config import settings

SECRET = settings.session_secret or secrets.token_urlsafe(48)
attempts = OrderedDict()


def make_token(username, role):
    payload = base64.urlsafe_b64encode(json.dumps({'username': username, 'role': role, 'exp': int(time.time()) + 43200}).encode()).decode()
    signature = hmac.new(SECRET.encode(), payload.encode(), hashlib.sha256).hexdigest()
    return payload + '.' + signature


def current_user(request: Request):
    if settings.app_mode == 'demo':
        return {'username': 'نسخهٔ نمایشی', 'role': 'admin'}
    try:
        payload, signature = request.cookies.get('didban_session', '').rsplit('.', 1)
        expected = hmac.new(SECRET.encode(), payload.encode(), hashlib.sha256).hexdigest()
        if not hmac.compare_digest(signature, expected):
            raise ValueError()
        user = json.loads(base64.urlsafe_b64decode(payload))
        if user['exp'] < time.time() or user['role'] not in ('admin', 'viewer'):
            raise ValueError()
        return user
    except (ValueError, KeyError, TypeError):
        raise HTTPException(401, 'برای ادامه وارد حساب کاربری شوید.') from None


def admin(request: Request):
    user = current_user(request)
    if user['role'] != 'admin':
        raise HTTPException(403, 'این عملیات به دسترسی مدیر نیاز دارد.')
    return user


def login(username, password, ip):
    stamp = time.monotonic()
    record = attempts.setdefault(ip, [])
    record[:] = [t for t in record if t > stamp - 300]
    if len(record) >= 10:
        raise HTTPException(429, 'تلاش‌های ورود بیش از حد است؛ پنج دقیقه بعد دوباره امتحان کنید.')
    record.append(stamp)
    if len(attempts) > 10000:
        attempts.popitem(last=False)
    for name, configured, role in [(settings.admin_username, settings.admin_password, 'admin'), (settings.viewer_username, settings.viewer_password, 'viewer')]:
        if configured and secrets.compare_digest(username.encode(), name.encode()) and secrets.compare_digest(password.encode(), configured.encode()):
            attempts.pop(ip, None)
            return {'username': name, 'role': role}
    raise HTTPException(401, 'نام کاربری یا رمز عبور درست نیست.')
