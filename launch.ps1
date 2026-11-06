param([switch]$Demo, [int]$Port = 0)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$pythonPath = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) { throw 'Run start.ps1 once to install the application.' }
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'frontend/out/index.html'))) { throw 'Build the frontend first: ./start.ps1 -Build' }
& $pythonPath scripts/bootstrap.py
$mode = 'live'
if ($Demo) { $mode = 'demo' }
if ($Port -eq 0) { if ($Demo) { $Port = 8001 } else { $Port = 8000 } }
try {
    $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/health" -TimeoutSec 2
    if ($health.mode -eq $mode) { Write-Host "Already running: http://127.0.0.1:$Port"; exit 0 }
    throw "Port $Port is in use."
} catch { if ($_.Exception.Message -eq "Port $Port is in use.") { throw } }
$env:APP_MODE = $mode
$env:EMBEDDED_SCHEDULER = 'true'
$env:PYTHONUTF8 = '1'
$logPath = Join-Path $PSScriptRoot "data/server-$mode.log"
$errPath = Join-Path $PSScriptRoot "data/server-$mode-error.log"
$process = Start-Process -FilePath $pythonPath -ArgumentList @('-X', 'utf8', '-m', 'uvicorn', 'backend.main:app', '--host', '127.0.0.1', '--port', "$Port") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput $logPath -RedirectStandardError $errPath -PassThru
@{ pid = $process.Id; mode = $mode; port = $Port } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot "data/process-$mode.json") -Encoding UTF8
$startupTimer = [System.Diagnostics.Stopwatch]::StartNew()
$ready = $false
while ($startupTimer.Elapsed.TotalSeconds -lt 30) {
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/health" -TimeoutSec 2
        if ($health.mode -eq $mode) { $ready = $true; break }
    } catch { }
    Start-Sleep -Milliseconds 500
}
if (-not $ready) { throw "Server did not become ready. Read data/server-$mode-error.log and try again." }
Write-Host "Started: http://127.0.0.1:$Port"
if (-not $Demo) { Write-Host 'Credentials: data/local-access.txt' }
