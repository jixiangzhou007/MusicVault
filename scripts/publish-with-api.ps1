param(
    [string]$GoMusicApiDir = "",
    [string]$OutputDir = "",
    [string]$ApiVersion = "",
    [switch]$Restart,
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) { $OutputDir = Join-Path $root "publish\win-x64-vol" }
if (-not $GoMusicApiDir) {
    $goCandidates = @(
        (Join-Path (Split-Path $root -Parent) "go-music-api"),
        (Join-Path $root "tools\go-music-api"),
        (Join-Path $root "vendor\go-music-api")
    )
    $GoMusicApiDir = $goCandidates | Where-Object {
        (Test-Path (Join-Path $_ "main.go")) -or (Test-Path (Join-Path $_ "go-music-api.exe"))
    } | Select-Object -First 1
    if (-not $GoMusicApiDir) { $GoMusicApiDir = $goCandidates[0] }
}
$lifecycle = Join-Path $PSScriptRoot "MusicVaultAppLifecycle.ps1"
$exeOut = Join-Path $OutputDir "MusicVault.exe"
if (-not $PSBoundParameters.ContainsKey('Restart') -and -not $NoRestart) { $Restart = $true }

Write-Host "Stopping running MusicVault (if any)..."
& $lifecycle -Action Stop

Write-Host "Publishing MusicVault..."
Push-Location $root
dotnet publish src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64 --self-contained -o $OutputDir
Pop-Location

# PublishSingleFile does not ship VideoLAN.LibVLC native plugins; copy beside exe.
$libVlcSrc = $null
$appLibVlc = Join-Path $root "src\MusicVault.App\bin\Release\net8.0-windows10.0.22621.0\win-x64\libvlc"
$appLibVlcAlt = Join-Path $root "src\MusicVault.App\bin\Release\net8.0\win-x64\libvlc"
$svcLibVlc = Join-Path $root "src\MusicVault.Services\bin\Release\net8.0\libvlc"
if (Test-Path $appLibVlc) { $libVlcSrc = $appLibVlc }
elseif (Test-Path $appLibVlcAlt) { $libVlcSrc = $appLibVlcAlt }
elseif (Test-Path $svcLibVlc) { $libVlcSrc = $svcLibVlc }

$libVlcDst = Join-Path $OutputDir "libvlc"
if ($libVlcSrc) {
    Write-Host "Bundling LibVLC runtime from $libVlcSrc ..."
    if (Test-Path $libVlcDst) { Remove-Item $libVlcDst -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $libVlcDst | Out-Null
    $winX64 = Join-Path $libVlcSrc "win-x64"
    if (Test-Path $winX64) {
        Copy-Item $winX64 (Join-Path $libVlcDst "win-x64") -Recurse -Force
    }
    else {
        Copy-Item (Join-Path $libVlcSrc "*") $libVlcDst -Recurse -Force
    }

    # Audio-only trim: drop video/GUI/streaming plugin trees to shrink publish size.
    $pluginsRoot = Join-Path $libVlcDst "win-x64\plugins"
    if (-not (Test-Path $pluginsRoot)) {
        $pluginsRoot = Join-Path $libVlcDst "plugins"
    }
    if (Test-Path $pluginsRoot) {
        $removePluginDirs = @(
            "access_output", "d3d11", "d3d9", "gui", "lua", "mux",
            "services_discovery", "spu", "stream_out", "text_renderer",
            "video_chroma", "video_filter", "video_output", "video_splitter",
            "visualization"
        )
        foreach ($name in $removePluginDirs) {
            $p = Join-Path $pluginsRoot $name
            if (Test-Path $p) { Remove-Item $p -Recurse -Force }
        }
    }
    foreach ($extra in @("lua", "hrtfs")) {
        $p = Join-Path $libVlcDst "win-x64\$extra"
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
        $p2 = Join-Path $libVlcDst $extra
        if (Test-Path $p2) { Remove-Item $p2 -Recurse -Force }
    }

    $dllA = Join-Path $libVlcDst "win-x64\libvlc.dll"
    $dllB = Join-Path $libVlcDst "libvlc.dll"
    if (-not (Test-Path $dllA) -and -not (Test-Path $dllB)) {
        Write-Warning "LibVLC folder copied but libvlc.dll not found; experimental engine may fail to start."
    }
    else {
        $libSizeMb = [math]::Round(((Get-ChildItem $libVlcDst -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
        Write-Host "LibVLC bundled (audio-trimmed): $libSizeMb MB"
    }
}
else {
    Write-Warning "LibVLC native runtime not found in build output; experimental LibVLC engine will be unavailable."
}

$targetApi = Join-Path $OutputDir "api"
New-Item -ItemType Directory -Force -Path $targetApi | Out-Null
$targetApiExe = Join-Path $targetApi "go-music-api.exe"

$apiExe = Join-Path $GoMusicApiDir "go-music-api.exe"
if (-not (Test-Path $apiExe) -and (Test-Path $targetApiExe)) {
    Write-Host "Reusing existing go-music-api.exe from output folder."
    $apiExe = $targetApiExe
}
elseif (-not (Test-Path $apiExe)) {
    if (-not (Test-Path $GoMusicApiDir)) {
        Write-Warning "go-music-api source not found at $GoMusicApiDir; skipping go-music-api build."
    }
    else {
        Write-Host "Building go-music-api..."
        Push-Location $GoMusicApiDir
        $env:GOPROXY = "https://goproxy.cn,direct"
        go build -ldflags "-H=windowsgui" -o go-music-api.exe .
        Pop-Location
        $apiExe = Join-Path $GoMusicApiDir "go-music-api.exe"
    }
}

if (Test-Path $apiExe) {
    if ($apiExe -ne $targetApiExe) {
        Copy-Item $apiExe $targetApiExe -Force
    }
}
else {
    Write-Warning "go-music-api.exe unavailable; online search/download may fail until you add api/go-music-api.exe."
}

if (-not (Test-Path $targetApiExe)) {
    throw "Missing required publish artifact: $targetApiExe"
}

$neteaseEntry = Join-Path $OutputDir "api\netease-cloud-music-api\entry-point.txt"
if (-not (Test-Path $neteaseEntry)) {
    throw "Missing required publish artifact: $neteaseEntry"
}

$version = "unknown"
$localVersionFile = Join-Path $GoMusicApiDir "version.txt"
$repoApiVersion = Join-Path $root "api\version.txt"
if ($ApiVersion) {
    $version = $ApiVersion.Trim()
}
elseif (Test-Path $localVersionFile) {
    $version = (Get-Content $localVersionFile -Raw).Trim()
}
elseif (Test-Path $repoApiVersion) {
    $version = (Get-Content $repoApiVersion -Raw).Trim()
}
else {
    try {
        $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/guohuiyuan/go-music-api/releases/latest" -TimeoutSec 15
        $version = $rel.tag_name
    } catch { $version = "v1.0.1" }
}
if (-not $version) { $version = "v1.0.1" }
Set-Content -Path (Join-Path $targetApi "version.txt") -Value $version -Encoding UTF8

$appVer = (Select-Xml -Path (Join-Path $root "src\MusicVault.App\MusicVault.App.csproj") -XPath "//Version" -ErrorAction SilentlyContinue).Node.InnerText
if (-not $appVer) { $appVer = "1.0.0" }
Set-Content -Path (Join-Path $targetApi "shipped-with-app.txt") -Value $appVer -Encoding UTF8

Write-Host "Bundling netease-cloud-music-api..."
& (Join-Path $PSScriptRoot "bundle-netease-cloud-music-api.ps1") -OutputDir $OutputDir

$scriptsOut = Join-Path $OutputDir "scripts"
New-Item -ItemType Directory -Force -Path $scriptsOut | Out-Null
Copy-Item (Join-Path $PSScriptRoot "setup-netease-cloud-music-api.ps1") $scriptsOut -Force
Copy-Item (Join-Path $PSScriptRoot "start-netease-cloud-music-api.ps1") $scriptsOut -Force

Write-Host "Copying legal notices..."
foreach ($legalFile in @("LICENSE", "NOTICE", "THIRD_PARTY_LICENSES.md")) {
    $src = Join-Path $root $legalFile
    if (Test-Path $src) { Copy-Item $src $OutputDir -Force }
}
$legalMd = Join-Path $root "docs\LEGAL.md"
if (Test-Path $legalMd) {
    $legalOutDir = Join-Path $OutputDir "docs"
    New-Item -ItemType Directory -Force -Path $legalOutDir | Out-Null
    Copy-Item $legalMd $legalOutDir -Force
}

Write-Host "Done. Zip this folder for distribution:"
Write-Host "  Bundled API version: $version"
Write-Host "  $OutputDir"
Write-Host "  MusicVault.exe + api/go-music-api.exe + api/netease-cloud-music-api"

if ($Restart -and -not $NoRestart -and (Test-Path $exeOut)) {
    Write-Host "Starting MusicVault..."
    & $lifecycle -Action Start -ExePath $exeOut
} elseif ($Restart -and -not $NoRestart) {
    Write-Host "Publish OK but MusicVault.exe not found; skip start."
}
