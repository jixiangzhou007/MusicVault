param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$PublishDir = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

function Assert-FileExists {
    param([string]$Path, [string]$Message)
    if (-not (Test-Path -LiteralPath $Path)) { throw $Message }
}

function Assert-FileContains {
    param([string]$Path, [string]$Pattern, [string]$Message)
    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
    if ($content -notmatch $Pattern) { throw $Message }
}

if (-not $PublishDir) {
    $PublishDir = Join-Path $Root "publish\win-x64-vol"
}

Write-Host "[smoke-libvlc] Structural checks..."

$factory = Join-Path $Root "src\MusicVault.Services\Audio\AudioPlayerServiceFactory.cs"
$snapshot = Join-Path $Root "src\MusicVault.Services\Audio\PlaybackEngineSnapshot.cs"
$support = Join-Path $Root "src\MusicVault.Services\Audio\LibVlcPlayerSupport.cs"
$settingsVm = Join-Path $Root "src\MusicVault.App\ViewModels\Pages\SettingsPageViewModel.cs"
$settingsView = Join-Path $Root "src\MusicVault.App\Views\Pages\SettingsPageView.axaml"
$diagnostics = Join-Path $Root "src\MusicVault.App\Services\PlaybackDiagnostics.cs"
$publishScript = Join-Path $Root "scripts\publish-with-api.ps1"
$macosScript = Join-Path $Root "scripts\publish-macos.sh"
$phase4 = Join-Path $Root "docs\execution\libvlc-phase-4.md"
$coordinator = Join-Path $Root "src\MusicVault.Services\Metadata\PlaybackFileWriteCoordinator.cs"
$smtc = Join-Path $Root "src\MusicVault.App\Services\WindowsSystemMediaTransportService.cs"
$l10n = Join-Path $Root "src\MusicVault.App\Localization\LocalizationResources.cs"

Assert-FileExists -Path $factory -Message "Missing AudioPlayerServiceFactory"
Assert-FileExists -Path $snapshot -Message "Missing PlaybackEngineSnapshot"
Assert-FileExists -Path $phase4 -Message "Missing libvlc-phase-4.md"
Assert-FileExists -Path $smtc -Message "Missing WindowsSystemMediaTransportService (SMTC)"

Assert-FileContains -Path $factory -Pattern 'PlaybackEngineSnapshot\.Write' -Message "Factory must write structured snapshot"
Assert-FileContains -Path $factory -Pattern '\[Engine\]' -Message "Factory must append Engine line to playback.log"
Assert-FileContains -Path $snapshot -Pattern 'nativeRoot=' -Message "Snapshot must include nativeRoot field"
Assert-FileContains -Path $support -Pattern 'TryResolveNativeRootFrom' -Message "Support must expose path resolution for tests"
Assert-FileContains -Path $settingsVm -Pattern 'ActivePlaybackEngineDetail' -Message "Settings must expose engine detail"
Assert-FileContains -Path $settingsView -Pattern 'ActivePlaybackEngineDetail' -Message "Settings view must bind engine detail"
Assert-FileContains -Path $diagnostics -Pattern 'playback-engine\.txt' -Message "Diagnostics export must include playback-engine.txt"
Assert-FileContains -Path $publishScript -Pattern 'Bundling LibVLC' -Message "Windows publish must bundle LibVLC"
Assert-FileContains -Path $macosScript -Pattern 'bundle_libvlc_from_vlc_app' -Message "macOS publish must bundle from VLC.app"
Assert-FileContains -Path $coordinator -Pattern 'LibVLC' -Message "Tag-write coordinator must mention LibVLC"
Assert-FileContains -Path $l10n -Pattern 'Settings\.Playback\.ActiveEngine\.DetailNative' -Message "Missing ActiveEngine.DetailNative l10n"
Assert-FileContains -Path $l10n -Pattern 'Settings\.Playback\.ActiveEngine\.DetailFallback' -Message "Missing ActiveEngine.DetailFallback l10n"

Write-Host "[smoke-libvlc] Publish output checks..."
Assert-FileExists -Path (Join-Path $PublishDir "MusicVault.exe") -Message "Missing published MusicVault.exe at $PublishDir"
$libDll = Join-Path $PublishDir "libvlc\win-x64\libvlc.dll"
Assert-FileExists -Path $libDll -Message "Missing bundled libvlc.dll (run publish-with-api.ps1 first)"

$dataDir = Join-Path $env:LOCALAPPDATA "MusicVault"
$engineMarker = Join-Path $dataDir "playback-engine.txt"
if (Test-Path -LiteralPath $engineMarker) {
    $marker = Get-Content -LiteralPath $engineMarker -Raw
    if ($marker -match '(?m)^engine=') {
        Write-Host "[smoke-libvlc] playback-engine.txt is structured (Phase 4+)."
    }
    elseif ($marker -match '\t') {
        Write-Host "[smoke-libvlc] playback-engine.txt is legacy tab format (will upgrade on next launch)."
    }
    else {
        Write-Warning "playback-engine.txt present but unrecognized format."
    }
}
else {
    Write-Host "[smoke-libvlc] No playback-engine.txt yet (app not launched after Phase 4)."
}

if (-not $SkipTests) {
    Write-Host "[smoke-libvlc] Running engine unit tests..."
    Push-Location $Root
    try {
        dotnet test (Join-Path $Root "tests\MusicVault.Tests\MusicVault.Tests.csproj") `
            -c Release `
            --filter "FullyQualifiedName~PlaybackEngineSnapshot|FullyQualifiedName~AudioPlayerServiceFactory|FullyQualifiedName~LibVlcPlayerSupport|FullyQualifiedName~PlaybackEngineBootstrap" `
            --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "LibVLC-related unit tests failed" }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "[smoke-libvlc] Automated checks passed."
Write-Host "Manual (still required): play / Seek / EQ / tag-write while playing / SMTC media keys."
Write-Host "  Settings → 播放与音频 → 当前引擎 detail should match $engineMarker"
