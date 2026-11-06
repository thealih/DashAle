import os
import tempfile
from pathlib import Path

TEST_DIR = Path(tempfile.mkdtemp(prefix='didban-tests-'))
os.environ['DATABASE_URL'] = f'sqlite:///{(TEST_DIR / "test.db").as_posix()}'
os.environ['APP_MODE'] = 'live'
os.environ['EMBEDDED_SCHEDULER'] = 'false'
os.environ['ADMIN_PASSWORD'] = 'test-admin-password'
os.environ['VIEWER_PASSWORD'] = 'test-viewer-password'
os.environ['SESSION_SECRET'] = 'test-secret-only-for-automated-tests'

import pytest
from fastapi.testclient import TestClient
from backend.db import Base, engine, init_db, SessionLocal, Product
from backend.main import app


@pytest.fixture(autouse=True)
def reset_db():
    Base.metadata.drop_all(engine)
    init_db()
    yield


@pytest.fixture
def db():
    with SessionLocal() as session:
        yield session


@pytest.fixture
def product(db):
    p = Product(brand_id='vitalayer', source_id='123456', title='سرم ویتالیر ۳۰ میلی‌لیتر', url='https://snappshop.ir/product/123456', tracked=True)
    db.add(p)
    db.commit()
    return p


@pytest.fixture
def client():
    with TestClient(app) as client:
        assert client.post('/api/auth/login', json={'username': 'admin', 'password': 'test-admin-password'}).status_code == 200
        yield client
