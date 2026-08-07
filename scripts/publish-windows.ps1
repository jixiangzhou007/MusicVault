# MusicVault Windows 发布脚本
# 默认：自包含单文件 + Trim（推荐）
# 可选：-Aot 启用 NativeAOT（需本机验证 Avalonia 兼容性）
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
    Write-Host "发布模式: NativeAOT 单文件" -ForegroundColor Yellow
} else {
    Write-Host "发布模式: 自包含单文件 + PublishTrimmed（推荐）" -ForegroundColor Cyan
}

Push-Location $root
try {
    dotnet @publishArgs
    $exe = Join-Path $OutputDir "MusicVault.exe"
    if (Test-Path $exe) {
        Write-Host "发布完成: $exe" -ForegroundColor Green
    } else {
        Write-Host "发布完成，输出目录: $OutputDir" -ForegroundColor Green
    }

    if ($Restart -and -not $NoRestart -and (Test-Path $exeOut)) {
        Write-Host "Starting MusicVault..." -ForegroundColor Cyan
        & $lifecycle -Action Start -ExePath $exeOut
    }
}
finally {
    Pop-Location
}
