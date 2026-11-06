param([switch]$Demo)
$ErrorActionPreference = 'Stop'
$mode = 'live'
if ($Demo) { $mode = 'demo' }
$recordPath = Join-Path $PSScriptRoot "data/process-$mode.json"
if (-not (Test-Path -LiteralPath $recordPath)) { Write-Host 'No background process recorded.'; exit 0 }
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$process = Get-CimInstance Win32_Process -Filter "ProcessId = $($record.pid)"
if ($process) {
    $expectedPath = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
    $expectedPath = [System.IO.Path]::GetFullPath($expectedPath)
    if ($process.ExecutablePath -ne $expectedPath -or $process.CommandLine -notmatch 'uvicorn backend.main:app' -or $process.CommandLine -notmatch "--port $($record.port)(\s|$)") { throw 'Recorded PID no longer belongs to this application; refusing to stop it.' }
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($record.pid)"
    foreach ($child in $children) {
        if ($child.CommandLine -match 'uvicorn backend.main:app') { Stop-Process -Id $child.ProcessId -Force }
    }
    Stop-Process -Id $record.pid -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $recordPath
Write-Host "Stopped $mode."
