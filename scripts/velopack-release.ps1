# MusicVault Velopack 安装包发布脚本
# 安装 vpk CLI: dotnet tool install -g vpk
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
    Write-Host "请先安装 Velopack CLI: dotnet tool install -g vpk" -ForegroundColor Red
    exit 1
}

$exe = Join-Path $PublishDir "MusicVault.exe"
if (-not (Test-Path $exe)) {
    Write-Host "未找到 $exe，请先运行 scripts\publish-windows.ps1 -NoRestart" -ForegroundColor Red
    exit 1
}

# 准备图标（使用应用内嵌图标或占位）
$iconArg = @()
if (Test-Path $IconPath) {
    $iconArg = @("--icon", (Resolve-Path $IconPath).Path)
    Write-Host "使用图标: $IconPath"
} else {
    Write-Host "未找到图标文件 ($IconPath)，将使用默认图标" -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ── 打包为 Velopack Release ──
$packArgs = @(
    "pack",
    "--packId", "MusicVault",
    "--packVersion", $Version,
    "--packTitle", "MusicVault",
    "--packAuthors", "MusicVault",
    "--packDir", (Resolve-Path $PublishDir).Path,
    "--mainExe", "MusicVault.exe",
    "--outputDir", (Resolve-Path $OutputDir).Path,
    "--shortcutName", "MusicVault",
    "--shortcutComment", "下载即整理，整理即播放 - 跨平台个人数字音乐管理工具"
) + $iconArg

if ($SkipDelta) {
    $packArgs += "--no-delta"
}

Write-Host "正在打包 Velopack release v$Version ..."
& vpk $packArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "打包失败" -ForegroundColor Red
    Pop-Location
    exit 1
}

# ── 生成更新源清单（供设置页 UpdateFeedUrl） ──
$releasesJson = Join-Path $OutputDir "releases.json"
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Velopack 包已生成: $(Resolve-Path $OutputDir)" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步上传 GitHub Release:" -ForegroundColor Yellow
Write-Host "  .\scripts\publish-github-release.ps1 -SkipPack" -ForegroundColor White
Write-Host "或推送 tag 走 Actions: git tag v$Version; git push origin v$Version" -ForegroundColor White
Write-Host ""
Write-Host "用户更新源（GitHub 仓库根 URL）:" -ForegroundColor Yellow
Write-Host "  https://github.com/guohuiyuan/MusicVault" -ForegroundColor White
Write-Host "详见 docs\ONLINE_UPDATE.md" -ForegroundColor DarkGray
if (Test-Path $releasesJson) {
    Write-Host "本地清单: $releasesJson" -ForegroundColor DarkGray
}

Pop-Location
