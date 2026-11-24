"""Read-only reachability probe. It never prints guest tokens or proxy credentials."""
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from backend.collector import source, SourceError
from backend.db import now

result = {'checked_at': now(), 'host': 'apix.snappshop.ir', 'live_verified': False}
try:
    payload = source.fetch('search/v1', {'render': 4, 'query': 'ویتالیر'})
    sections = (payload.get('data') or {}).get('structure') or []
    plp = next((s for s in sections if s.get('section_type') == 'plp'), None)
    if not plp:
        raise SourceError('ساختار نتایج جست‌وجو قابل تشخیص نیست.')
    result.update(live_verified=True, sample_search_count=len(plp.get('items') or []), pagination=plp.get('pagination'))
except SourceError as exc:
    result['error'] = str(exc)
directory = Path(__file__).resolve().parent.parent / 'deliverables'
directory.mkdir(exist_ok=True)
(directory / 'source-check.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
