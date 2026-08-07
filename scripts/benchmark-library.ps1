# 大曲库查询性能基准（本地开发用）
# 用法: powershell -File scripts/benchmark-library.ps1
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    Write-Host "== MusicVault 曲库查询基准 ==" -ForegroundColor Cyan
    Write-Host "配置: $Configuration" -ForegroundColor DarkGray
    Write-Host ""

    dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj `
        -c $Configuration `
        --filter "FullyQualifiedName~LibraryQueryBenchmarkTests" `
        --logger "console;verbosity=detailed"

    if ($LASTEXITCODE -ne 0) {
        throw "基准测试未通过 (exit $LASTEXITCODE)"
    }

    Write-Host ""
    Write-Host "完成。可在 LibraryQueryBenchmarkTests 中调整曲目数量与耗时预算。" -ForegroundColor Green
}
finally {
    Pop-Location
}
