# Start bundled netease-cloud-music-api (port 3000).
param(
    [string]$AppDir = "",
    [int]$Port = 3000
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($AppDir)) {
    $AppDir = Join-Path $root "publish\win-x64-vol"
}

$apiDir = Join-Path $AppDir "api\netease-cloud-music-api"
$entryFile = Join-Path $apiDir "entry-point.txt"
if (-not (Test-Path $entryFile)) {
    Write-Host "Missing $entryFile"
    Write-Host "Run: .\scripts\setup-netease-cloud-music-api.ps1 -TargetDir `"$AppDir`""
    exit 1
}

$entryRel = (Get-Content $entryFile -Raw).Trim()
$entryPath = Join-Path $apiDir $entryRel
if (-not (Test-Path $entryPath)) {
    Write-Host "Entry script not found: $entryPath"
    exit 1
}

$nodeExe = Join-Path $AppDir "api\node\node.exe"
if (-not (Test-Path $nodeExe)) {
    $nodeCmd = Get-Command node -ErrorAction SilentlyContinue
    if (-not $nodeCmd) {
        Write-Host "Node.js not found. Run setup script or install Node.js LTS."
        exit 1
    }
    $nodeExe = $nodeCmd.Source
}

$existing = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
if ($existing) {
    Write-Host "Port $Port is already in use."
    Write-Host "URL: http://localhost:$Port"
    exit 0
}

$scriptArg = $entryRel -replace '\\', '/'
Start-Process -FilePath $nodeExe -ArgumentList $scriptArg -WorkingDirectory $apiDir -WindowStyle Minimized
Start-Sleep -Seconds 4

$probeUri = "http://localhost:$Port/search?keywords=test&type=1&limit=1"
try {
    $probe = Invoke-RestMethod -Uri $probeUri -TimeoutSec 12
    if ($probe.code -eq 200) {
        Write-Host "Netease API started: http://localhost:$Port"
    }
    else {
        Write-Host "Started but API code=$($probe.code)"
    }
}
catch {
    Write-Host "Process started; probe not ready yet: $($_.Exception.Message)"
}
