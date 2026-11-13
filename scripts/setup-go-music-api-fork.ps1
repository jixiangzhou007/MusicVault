param(
    [string]$RepoUrl = "https://github.com/guohuiyuan/go-music-api.git",
    [string]$Branch = "",
    [string]$Version = "",
    [string]$TargetDir = "",
    [switch]$SkipBuild,
    [switch]$InstallToPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $TargetDir) { $TargetDir = Join-Path $root "tools\go-music-api" }

Write-Host "Target: $TargetDir"
Write-Host "Repo:   $RepoUrl"

if (-not (Test-Path (Join-Path $TargetDir ".git"))) {
    $parent = Split-Path $TargetDir -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-Path $TargetDir) {
        throw "Directory exists but is not a git repo: $TargetDir"
    }
    git clone $RepoUrl $TargetDir
}
else {
    Push-Location $TargetDir
    git fetch --all --tags
    if ($Branch) {
        git checkout $Branch
        git pull --ff-only origin $Branch
    }
    Pop-Location
}

if (-not $SkipBuild) {
    if (-not (Get-Command go -ErrorAction SilentlyContinue)) {
        throw "Go toolchain not found. Install Go or pass -SkipBuild and use install-custom-music-api.ps1 with a prebuilt exe."
    }
    Write-Host "Building go-music-api.exe ..."
    Push-Location $TargetDir
    $env:GOPROXY = if ($env:GOPROXY) { $env:GOPROXY } else { "https://goproxy.cn,direct" }
    go build -ldflags "-H=windowsgui" -o go-music-api.exe .
    Pop-Location
}

$exe = Join-Path $TargetDir "go-music-api.exe"
if (-not (Test-Path $exe)) {
    throw "Missing $exe (build failed or -SkipBuild without exe)."
}

if (-not $Version) {
    try {
        Push-Location $TargetDir
        $describe = (git describe --tags --always 2>$null)
        Pop-Location
        if ($describe) { $Version = $describe.Trim() }
    } catch { }
    if (-not $Version) { $Version = "custom" }
}

$installArgs = @{
    ExePath = $exe
    Version = $Version
}
if ($InstallToPublish) { $installArgs.InstallToPublish = $true }

& (Join-Path $PSScriptRoot "install-custom-music-api.ps1") @installArgs

Write-Host ""
Write-Host "Next:"
Write-Host "  1) Point Settings → Unified API to http://localhost:8080 (or leave blank with auto-start)."
Write-Host "  2) Optional: Settings → API update repo = owner/your-fork"
Write-Host "  3) Publish: .\scripts\publish-with-api.ps1 -Restart -GoMusicApiDir `"$TargetDir`""
Write-Host "  Docs: docs\GO_MUSIC_API_EXTENSION.md"
