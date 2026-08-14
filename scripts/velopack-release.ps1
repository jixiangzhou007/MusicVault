# MusicVault Velopack pack script
# Install CLI: dotnet tool install -g vpk
param(
    [string]$PublishDir = ".\publish\win-x64",
    [string]$OutputDir = ".\releases",
    [string]$Version = "",
    [string]$IconPath = ".\assets\app-icon.ico",
    [switch]$SkipDelta = $false
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

function Get-ProjectVersion {
    $props = Join-Path $root "Directory.Build.props"
    $m = Select-String -Path $props -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if (-not $m) { throw "Cannot read Version from Directory.Build.props" }
    return $m.Matches[0].Groups[1].Value.Trim()
}

if (-not $Version) { $Version = Get-ProjectVersion }
$Version = $Version.TrimStart('v', 'V')

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Install Velopack CLI first: dotnet tool install -g vpk" -ForegroundColor Red
    exit 1
}

$exe = Join-Path $PublishDir "MusicVault.exe"
if (-not (Test-Path $exe)) {
    Write-Host "Missing $exe. Run scripts\publish-windows.ps1 -NoRestart first." -ForegroundColor Red
    exit 1
}

$iconArg = @()
if (Test-Path $IconPath) {
    $iconArg = @("--icon", (Resolve-Path $IconPath).Path)
    Write-Host "Icon: $IconPath"
} else {
    Write-Host "No icon at $IconPath; using default" -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$packArgs = @(
    "pack",
    "--packId", "MusicVault",
    "--packVersion", $Version,
    "--packTitle", "MusicVault",
    "--packAuthors", "MusicVault",
    "--packDir", (Resolve-Path $PublishDir).Path,
    "--mainExe", "MusicVault.exe",
    "--outputDir", (Resolve-Path $OutputDir).Path,
    "--channel", "win"
) + $iconArg

if ($SkipDelta) {
    $packArgs += "--delta", "None"
}

Write-Host "Packing Velopack release v$Version ..."
& vpk @packArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "vpk pack failed: $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Velopack output: $(Resolve-Path $OutputDir)" -ForegroundColor Green
Write-Host "Next: .\scripts\publish-github-release.ps1 -SkipPack" -ForegroundColor Yellow
Write-Host "Or: git tag v$Version; git push origin v$Version" -ForegroundColor Yellow
Write-Host "Update feed: https://github.com/jixiangzhou007/MusicVault" -ForegroundColor White
Pop-Location
