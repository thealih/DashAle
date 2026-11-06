param([switch]$Demo, [int]$Port = 0, [switch]$Build)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$pythonPath = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) {
    $candidates = @((Join-Path $env:LOCALAPPDATA 'Programs/Python/Python311/python.exe'), 'python')
    $basePython = $candidates | Where-Object { (Test-Path -LiteralPath $_) -or (Get-Command $_ -ErrorAction SilentlyContinue) } | Select-Object -First 1
    if (-not $basePython) { throw 'Python 3.11+ is required.' }
    & $basePython -m venv .venv
    if ($LASTEXITCODE -ne 0) { throw 'Could not create virtual environment.' }
    & $pythonPath -m pip install -r requirements.txt
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
}
if ($Build -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'frontend/out/index.html'))) {
    Push-Location -LiteralPath (Join-Path $PSScriptRoot 'frontend')
    try {
        npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend installation failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    } finally { Pop-Location }
}
& $pythonPath scripts/bootstrap.py
if ($Demo) { $env:APP_MODE = 'demo' } else { $env:APP_MODE = 'live' }
$env:EMBEDDED_SCHEDULER = 'true'
if ($Port -eq 0) { if ($Demo) { $Port = 8001 } else { $Port = 8000 } }
Write-Host "Didban: http://127.0.0.1:$Port"
if (-not $Demo) { Write-Host 'Login credentials: data/local-access.txt' }
& $pythonPath -X utf8 -m uvicorn backend.main:app --host 127.0.0.1 --port $Port
