# MusicVault Windows publish script
# Default: self-contained single-file
# Optional: -Aot for NativeAOT
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [switch]$Aot,
    [switch]$Restart,
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $root "publish\$Runtime" }
$lifecycle = Join-Path $PSScriptRoot "MusicVaultAppLifecycle.ps1"
$exeOut = Join-Path $OutputDir "MusicVault.exe"
if (-not $PSBoundParameters.ContainsKey('Restart') -and -not $NoRestart) { $Restart = $true }

Write-Host "Stopping running MusicVault (if any)..."
& $lifecycle -Action Stop

$publishArgs = @(
    "publish", "src\MusicVault.App\MusicVault.App.csproj",
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $OutputDir
)

if ($Aot) {
    $publishArgs += "-p:EnableAot=true"
    Write-Host "Publish mode: NativeAOT" -ForegroundColor Yellow
} else {
    Write-Host "Publish mode: self-contained single-file" -ForegroundColor Cyan
}

Push-Location $root
try {
    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
    $exe = Join-Path $OutputDir "MusicVault.exe"
    if (Test-Path $exe) {
        Write-Host "Published: $exe" -ForegroundColor Green
    } else {
        Write-Host "Published dir: $OutputDir" -ForegroundColor Green
    }

    if ($Restart -and -not $NoRestart -and (Test-Path $exeOut)) {
        Write-Host "Starting MusicVault..." -ForegroundColor Cyan
        & $lifecycle -Action Start -ExePath $exeOut
    }
}
finally {
    Pop-Location
}
