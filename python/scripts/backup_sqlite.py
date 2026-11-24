from pathlib import Path
import argparse
import sqlite3
from datetime import datetime, timezone

parser = argparse.ArgumentParser(description='Consistent backup of the local SQLite database while the app is running.')
parser.add_argument('--demo', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
mode = 'demo' if args.demo else 'live'
source = root / 'data' / f'{mode}.db'
if not source.exists():
    parser.error('Database does not exist. Start the app first.')
directory = root / 'data' / 'backups'
directory.mkdir(exist_ok=True)
destination = directory / f'{mode}-{datetime.now(timezone.utc):%Y%m%d-%H%M%S}.db'
with sqlite3.connect(source) as live, sqlite3.connect(destination) as backup:
    live.backup(backup)
print(f'Backup created: {destination}')
