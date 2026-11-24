param(
    [Parameter(Mandatory)][ValidateSet('Install','Start','Stop','Backup')][string]$Action,
    [ValidateRange(1024,65535)][int]$Port = 8000,
    [switch]$SkipFrontend,
    [switch]$NoMonitoring
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$data = Join-Path $PSScriptRoot 'data'
$release = Join-Path $data 'dotnet-release'
$dll = Join-Path $release 'PriceTracker.Api.dll'
$recordPath = Join-Path $data "process-dotnet-$Port.json"
$accessPath = Join-Path $data 'dotnet-access.txt'
$configPath = Join-Path $data 'dotnet-local.json'
$url = "http://127.0.0.1:$Port"
$project = Join-Path $PSScriptRoot 'PriceTracker.Api/PriceTracker.Api.csproj'
$frontend = Join-Path (Split-Path -Parent $PSScriptRoot) 'frontend'

function Get-PanelProcess {
    if (-not (Test-Path -LiteralPath $recordPath)) { return }
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    $processId = [int]$record.pid
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $processId"
    if ($process -and ($process.Name -ne 'dotnet.exe' -or
        -not $process.CommandLine.Contains($dll) -or -not $process.CommandLine.Contains("--urls $url"))) {
        throw 'The recorded process belongs to another program.'
    }
    return $process
}

if ($Action -eq 'Stop') {
    $process = Get-PanelProcess
    if ($process) { Stop-Process -Id $process.ProcessId -Force }
    if (Test-Path -LiteralPath $recordPath) { Remove-Item -LiteralPath $recordPath }
    Write-Host "Stopped .NET panel on port $Port."
    return
}

$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
if ($Action -eq 'Install') {
    if (-not $SkipFrontend) {
        Push-Location -LiteralPath $frontend
        try {
            & npm.cmd ci
            if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
            & npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
        } finally { Pop-Location }
    }
    & $dotnet publish $project --configuration Release --output $release --nologo
    if ($LASTEXITCODE -ne 0) { throw '.NET publish failed. Stop the panel before updating.' }
    Write-Host 'Installation complete. Use Run.cmd to start.'
    return
}

if (-not (Test-Path -LiteralPath $dll)) { throw 'Run Install.cmd first.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DB__SqlConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=PriceTrackerDotNetLive;Integrated Security=True;TrustServerCertificate=True'

if ($Action -eq 'Backup') {
    $backupDir = Join-Path $data 'backups'
    $destination = Join-Path $backupDir ("dotnet-{0:yyyyMMdd-HHmmssfff}.bak" -f [DateTime]::UtcNow)
    & $dotnet $dll --backup-panel-db $destination
    if ($LASTEXITCODE -ne 0) { throw 'Database backup failed.' }
    return
}

$staticPath = Join-Path $frontend 'out'
if (-not (Test-Path -LiteralPath (Join-Path $staticPath 'index.html'))) { throw 'Run Install.cmd to build the panel.' }

function New-Secret {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes)
}

if (-not (Test-Path -LiteralPath $configPath)) {
    if (Test-Path -LiteralPath $accessPath) { throw 'Local login configuration is incomplete. Restore data/dotnet-local.json.' }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    @{
        signingKey = New-Secret
        ready = $false
    } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
}
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if (-not $config.ready -and -not (Test-Path -LiteralPath $accessPath)) {
    @(
        'Admin username: admin'
        "Admin password: $(New-Secret)"
        'Viewer username: viewer'
        "Viewer password: $(New-Secret)"
    ) | Set-Content -LiteralPath $accessPath -Encoding UTF8
}

$env:Jwt__SigningKey = $config.signingKey
$env:Panel__StaticPath = $staticPath
$env:Monitoring__Enabled = if ($NoMonitoring) { 'false' } else { 'true' }
$env:DOTNET_NOLOGO = 'true'

function Get-Health {
    try { Invoke-RestMethod -Uri "$url/api/health" -TimeoutSec 2 } catch { return $null }
}

$process = Get-PanelProcess
if (-not $process) {
    $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    try { $probe.Start() } catch { throw "Port $Port is in use. Stop the other instance before launching." }
    finally { $probe.Stop() }
    $logPath = Join-Path $data "server-dotnet-$Port.log"
    $errPath = Join-Path $data "server-dotnet-$Port-error.log"
    $process = Start-Process -FilePath $dotnet -ArgumentList @("`"$dll`"", '--urls', $url) `
        -WorkingDirectory $release -WindowStyle Hidden -RedirectStandardOutput $logPath -RedirectStandardError $errPath -PassThru
    @{ pid = $process.Id; port = $Port } | ConvertTo-Json | Set-Content -LiteralPath $recordPath -Encoding UTF8
}

$timer = [System.Diagnostics.Stopwatch]::StartNew()
while ((Get-Health).backend -ne 'dotnet') {
    if ($timer.Elapsed.TotalSeconds -ge 40) { throw "Read data/server-dotnet-$Port.log for startup details." }
    Start-Sleep -Milliseconds 500
}

if (-not $config.ready) {
    if (-not (Test-Path -LiteralPath $accessPath)) { throw 'Local login credentials are missing.' }
    $lines = Get-Content -LiteralPath $accessPath
    foreach ($role in @('admin','viewer')) {
        $label = if ($role -eq 'admin') { 'Admin' } else { 'Viewer' }
        $password = ($lines | Where-Object { $_.StartsWith("$label password: ") }).Substring("$label password: ".Length)
        $body = @{ username = $role; password = $password } | ConvertTo-Json
        $canLogin = $false
        try { $canLogin = (Invoke-RestMethod "$url/api/auth/login" -Method Post -ContentType 'application/json' -Body $body).role -eq $role } catch { }
        if (-not $canLogin) {
            $password | & $dotnet $dll --create-panel-user $role $role
            if ($LASTEXITCODE -ne 0) { throw "Failed to create the local $role account." }
        }
    }
    $config.ready = $true
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
}
Write-Host "Started: $url"
Write-Host 'Credentials: data/dotnet-access.txt'
