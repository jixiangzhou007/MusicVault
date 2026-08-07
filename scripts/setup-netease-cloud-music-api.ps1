# Install bundled netease-cloud-music-api into a MusicVault publish/dev folder.
param(
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path $root "publish\win-x64-vol"
}

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

& (Join-Path $PSScriptRoot "bundle-netease-cloud-music-api.ps1") -OutputDir $TargetDir
Write-Host ""
Write-Host "Done. Start with:"
Write-Host "  .\scripts\start-netease-cloud-music-api.ps1 -AppDir `"$TargetDir`""
