from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

legacy = Path(__file__).resolve().parent.parent
root = legacy.parent
destination = legacy / 'deliverables' / 'didban-source-v1.3.zip'
exclude = {'.venv', '.git', 'node_modules', '.next', '__pycache__', '.pytest_cache', 'data', 'research', 'test-results', 'deliverables', 'out', 'screenshots'}
with ZipFile(destination, 'w', ZIP_DEFLATED) as archive:
    for component in (legacy, root / 'frontend'):
        for path in component.rglob('*'):
            relative = path.relative_to(root)
            if any(part in exclude for part in relative.parts):
                continue
            if not path.is_file() or path.name in {'.env', 'snappshop-home.html'} or path.suffix in {'.log', '.tsbuildinfo', '.pyc'}:
                continue
            archive.write(path, relative.as_posix())
    for name in ['VALIDATION.md', 'source-check.json', 'report-demo-v1.1.xlsx', 'dashboard-v1.1.png', 'add-product.png', 'price-position.png', 'price-position-demo.xlsx', 'delete-products.png']:
        path = root / 'frontend' / 'screenshots' / name if name.endswith('.png') else legacy / 'deliverables' / name
        if path.exists(): archive.write(path, path.relative_to(root).as_posix())
print(f'Packaged Python backend and frontend source: {destination.name} ({destination.stat().st_size:,} bytes)')
