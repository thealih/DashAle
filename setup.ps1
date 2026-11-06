param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$pythonPath = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'

function Find-CompatiblePython {
    $candidates = @()
    $launcher = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($launcher) {
        $candidates += @{ File = $launcher.Source; Prefix = @('-3.12') }
        $candidates += @{ File = $launcher.Source; Prefix = @('-3.11') }
    }
    foreach ($version in @('Python312', 'Python311')) {
        $candidatePath = Join-Path $env:LOCALAPPDATA "Programs/Python/$version/python.exe"
        if (Test-Path -LiteralPath $candidatePath) { $candidates += @{ File = $candidatePath; Prefix = @() } }
    }
    $command = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += @{ File = $command.Source; Prefix = @() } }
    foreach ($candidate in $candidates) {
        try {
            $prefix = $candidate.Prefix
            $versionResult = & $candidate.File @prefix -c "import sys; print('compatible' if sys.version_info[:2] in ((3,11),(3,12)) else 'unsupported')" 2>$null
            if ($LASTEXITCODE -eq 0 -and $versionResult -eq 'compatible') { return $candidate }
        } catch { continue }
    }
    throw 'Python 3.11 or 3.12 was not found. Install Python, enable Add python.exe to PATH, then run Install.cmd again. See README.md.'
}

if (-not (Test-Path -LiteralPath $pythonPath)) {
    Write-Host '[1/4] Preparing Python environment...'
    $basePython = Find-CompatiblePython
    $prefix = $basePython.Prefix
    & $basePython.File @prefix -m venv .venv
    if ($LASTEXITCODE -ne 0) { throw 'Could not create Python environment.' }
}
Write-Host '[2/4] Installing backend dependencies...'
& $pythonPath -m pip install -r requirements.txt
if ($LASTEXITCODE -ne 0) { throw 'Dependency download failed. Check your connection and run Install.cmd again.' }

if (-not $SkipFrontend) {
    if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'Node.js with npm is required. Install Node.js 22 LTS, then run Install.cmd again. See README.md.' }
    Write-Host '[3/4] Installing and building the panel. This can take several minutes...'
    Push-Location -LiteralPath (Join-Path $PSScriptRoot 'frontend')
    try {
        & npm.cmd ci --no-audit --no-fund --registry=https://registry.npmjs.org
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency download failed.' }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    } finally { Pop-Location }
} elseif (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'frontend/out/index.html'))) {
    throw 'Compiled frontend is missing. Run Install.cmd without SkipFrontend.'
}
Write-Host '[4/4] Preparing local settings and login information...'
& $pythonPath -X utf8 scripts/bootstrap.py
if ($LASTEXITCODE -ne 0) { throw 'Could not prepare local settings.' }
Write-Host ''
Write-Host 'Installation complete. Double-click Run.cmd to open the application.'
Write-Host 'Login information: data/local-access.txt'
