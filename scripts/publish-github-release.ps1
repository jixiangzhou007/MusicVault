# 将 Velopack releases/ 上传到 GitHub Release，供应用内 GithubSource 更新。
# 前置：git remote、gh 已登录；建议先跑 publish-windows.ps1 + velopack-release.ps1
param(
    [string]$Version = "",
    [string]$ReleasesDir = ".\releases",
    [string]$PublishDir = ".\publish\win-x64",
    [switch]$SkipPack,
    [switch]$IncludePortableZip,
    [string]$PortableDir = ".\publish\win-x64-vol",
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-ProjectVersion {
    $props = Join-Path $root "Directory.Build.props"
    $m = Select-String -Path $props -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if (-not $m) { throw "Cannot read Version from Directory.Build.props" }
    return $m.Matches[0].Groups[1].Value.Trim()
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "未找到 gh（GitHub CLI）。请安装: https://cli.github.com/" -ForegroundColor Red
    Write-Host "安装并 gh auth login 后重试本脚本。" -ForegroundColor Yellow
    exit 1
}

if (-not $Version) { $Version = Get-ProjectVersion }
$Version = $Version.TrimStart('v', 'V')
$tag = "v$Version"

$releasesPath = Join-Path $root ($ReleasesDir.TrimStart('.', '\'))
if (-not $SkipPack) {
    $packScript = Join-Path $PSScriptRoot "velopack-release.ps1"
    if (-not (Test-Path (Join-Path $releasesPath "RELEASES")) -and -not (Test-Path (Join-Path $releasesPath "releases.json"))) {
        Write-Host "releases/ 不完整，先执行 velopack-release.ps1 ..." -ForegroundColor Yellow
        & $packScript -Version $Version -PublishDir $PublishDir -OutputDir $ReleasesDir
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

if (-not (Test-Path $releasesPath)) {
    Write-Host "未找到 $releasesPath，请先: .\scripts\velopack-release.ps1" -ForegroundColor Red
    exit 1
}

$assets = Get-ChildItem -Path $releasesPath -File | Where-Object {
    $_.Name -match '\.(nupkg|exe|json)$' -or $_.Name -eq 'RELEASES' -or $_.Name -eq 'releases.win.json'
}
if ($assets.Count -eq 0) {
    Write-Host "releases/ 下没有可上传文件" -ForegroundColor Red
    exit 1
}

$portableZip = $null
if ($IncludePortableZip) {
    $portablePath = Join-Path $root ($PortableDir.TrimStart('.', '\'))
    if (-not (Test-Path $portablePath)) {
        Write-Host "便携目录不存在: $portablePath（跳过 zip）" -ForegroundColor Yellow
    } else {
        $portableZip = Join-Path $env:TEMP "MusicVault-$Version-win-x64-vol.zip"
        if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
        Write-Host "打包便携 zip: $portableZip" -ForegroundColor Cyan
        Compress-Archive -Path (Join-Path $portablePath '*') -DestinationPath $portableZip -CompressionLevel Optimal
    }
}

$releaseArgs = @("release", "view", $tag)
& gh @releaseArgs 2>$null | Out-Null
$exists = ($LASTEXITCODE -eq 0)

$title = "MusicVault $Version"
$notesFile = Join-Path $env:TEMP "musicvault-release-notes-$Version.md"
@"
## MusicVault $Version

两个安装包：Windows x64、macOS Apple Silicon（若本 Release 含 osx-arm64 zip）。不做 iOS、不做 32 位。

自动更新（Velopack）请使用本 Release 中的 **Setup / nupkg** 产物。便携 zip 解压即用，但不走应用内更新。

macOS：下载 ``MusicVault-$Version-osx-arm64.zip``，不要下 Windows 包。若提示无法打开：访达中右键 → 打开。Intel Mac 第一版不支持。Mac 合格线是启动 + 本地出声 + 在线听；下载不作保证。

详见 docs/ONLINE_UPDATE.md 与 docs/GITHUB_RELEASE.md。
"@ | Set-Content -Path $notesFile -Encoding UTF8

if (-not $exists) {
    $create = @("release", "create", $tag, "--title", $title, "--notes-file", $notesFile)
    if ($Draft) { $create += "--draft" }
    if ($Prerelease) { $create += "--prerelease" }
    Write-Host "Creating GitHub Release $tag ..." -ForegroundColor Cyan
    & gh @create
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
} else {
    Write-Host "Release $tag already exists; uploading assets..." -ForegroundColor Yellow
}

foreach ($f in $assets) {
    Write-Host "  upload $($f.Name)" -ForegroundColor DarkGray
    & gh release upload $tag $f.FullName --clobber
    if ($LASTEXITCODE -ne 0) { throw "upload failed: $($f.Name)" }
}

if ($portableZip -and (Test-Path $portableZip)) {
    Write-Host "  upload $(Split-Path $portableZip -Leaf)" -ForegroundColor DarkGray
    & gh release upload $tag $portableZip --clobber
}

$repo = (& gh repo view --json url -q .url).Trim()
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Release $tag 已就绪" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "更新源 URL（设置页可留空，已内置默认）:" -ForegroundColor Yellow
Write-Host "  $repo" -ForegroundColor White
Write-Host "浏览器打开: $repo/releases/tag/$tag" -ForegroundColor DarkGray
