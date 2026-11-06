from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent.parent
destination = root / 'deliverables' / 'didban-source-v1.3.zip'
exclude = {'.venv', '.git', 'node_modules', '.next', '__pycache__', '.pytest_cache', 'data', 'research', 'test-results', 'deliverables'}
with ZipFile(destination, 'w', ZIP_DEFLATED) as archive:
    for path in root.rglob('*'):
        relative = path.relative_to(root)
        if any(part in exclude for part in relative.parts):
            continue
        if not path.is_file() or path.name in {'.env', 'snappshop-home.html'} or path.suffix in {'.log', '.tsbuildinfo', '.pyc'}:
            continue
        archive.write(path, relative.as_posix())
    for name in ['VALIDATION.md', 'source-check.json', 'report-demo-v1.1.xlsx', 'dashboard-v1.1.png', 'add-product.png', 'price-position.png', 'price-position-demo.xlsx', 'delete-products.png']:
        path = root / 'deliverables' / name
        if path.exists(): archive.write(path, 'deliverables/' + name)
print(f'Packaged source and compiled panel: {destination.name} ({destination.stat().st_size:,} bytes)')
