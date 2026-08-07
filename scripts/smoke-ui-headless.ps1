param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "[smoke] Build solution ($Configuration)..."
dotnet build (Join-Path $Root "MusicVault.sln") -c $Configuration --no-restore 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet build (Join-Path $Root "MusicVault.sln") -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed before headless UI smoke"
    }
}

Write-Host "[smoke] Run Avalonia Headless UI smoke tests..."
dotnet test (Join-Path $Root "MusicVault.sln") -c $Configuration --no-build --filter "FullyQualifiedName~MainWindowHeadlessSmokeTests"
if ($LASTEXITCODE -ne 0) {
    throw "Headless UI smoke test failed"
}

Write-Host "[smoke] Headless UI smoke passed"
