param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [string]$Version = "custom",
    [switch]$InstallToPublish,
    [string]$PublishDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

if (-not (Test-Path $ExePath)) {
    throw "Exe not found: $ExePath"
}

function Install-ApiBundle([string]$DestRoot) {
    $apiDir = Join-Path $DestRoot "api"
    New-Item -ItemType Directory -Force -Path $apiDir | Out-Null
    $destExe = Join-Path $apiDir "go-music-api.exe"
    Copy-Item $ExePath $destExe -Force
    Set-Content -Path (Join-Path $apiDir "version.txt") -Value $Version.Trim() -Encoding UTF8
    Write-Host "Installed: $destExe ($Version)"
}

# Dev / source tree (optional; publish is what ships)
$repoApi = Join-Path $root "api"
Install-ApiBundle $root

if ($InstallToPublish) {
    if (-not $PublishDir) { $PublishDir = Join-Path $root "publish\win-x64-vol" }
    if (-not (Test-Path $PublishDir)) {
        Write-Warning "Publish dir missing: $PublishDir (skip). Run publish-with-api.ps1 first or pass -PublishDir."
    }
    else {
        Install-ApiBundle $PublishDir
    }
}

Write-Host "Done. Restart MusicVault or run: .\scripts\MusicVaultAppLifecycle.ps1 -Action Restart"
