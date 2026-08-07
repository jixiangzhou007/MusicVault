param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"

function Assert-FileExists {
    param([string]$Path, [string]$Message)
    if (-not (Test-Path $Path)) {
        throw $Message
    }
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )
    $content = Get-Content -Path $Path -Raw
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

Write-Host "[verify-macos] Checking macOS publish assets and capability alignment..."

$publishScript = Join-Path $Root "scripts\publish-macos.sh"
$nativeSource = Join-Path $Root "src\MusicVault.App\native\macOS\MusicVaultPlayer.m"
$aboutWindow = Join-Path $Root "src\MusicVault.App\Views\Dialogs\AboutWindow.axaml.cs"
$settingsVm = Join-Path $Root "src\MusicVault.App\ViewModels\Pages\SettingsPageViewModel.cs"
$l10n = Join-Path $Root "src\MusicVault.App\Localization\LocalizationResources.cs"
$matrix = Join-Path $Root "docs\execution\platform-capability-matrix.md"

Assert-FileExists -Path $publishScript -Message "Missing publish-macos.sh"
Assert-FileExists -Path $nativeSource -Message "Missing MusicVaultPlayer.m"
Assert-FileExists -Path $matrix -Message "Missing platform-capability-matrix.md"

# publish-macos.sh: dotnet publish must run before dylib compile
Assert-FileContains -Path $publishScript -Pattern 'dotnet "\$\{ARGS\[@\]\}"' -Message "publish-macos.sh must invoke dotnet publish"
Assert-FileContains -Path $publishScript -Pattern 'clang -dynamiclib' -Message "publish-macos.sh must compile libMusicVaultPlayer.dylib"
Assert-FileContains -Path $publishScript -Pattern 'bundle_libvlc_from_vlc_app' -Message "publish-macos.sh must bundle LibVLC from VLC.app"
Assert-FileContains -Path $publishScript -Pattern 'VLC_APP' -Message "publish-macos.sh must honor VLC_APP for LibVLC natives"
Assert-FileContains -Path $publishScript -Pattern 'libvlc/\$\{LIBVLC_PLATFORM\}' -Message "publish-macos.sh must place LibVLC under libvlc/<platform>"
$scriptContent = Get-Content -Path $publishScript -Raw
$publishIndex = $scriptContent.IndexOf('dotnet "${ARGS[@]}"')
$clangIndex = $scriptContent.IndexOf('clang -dynamiclib')
$libVlcIndex = $scriptContent.IndexOf('bundle_libvlc_from_vlc_app')
if ($publishIndex -lt 0 -or $clangIndex -lt 0 -or $publishIndex -gt $clangIndex) {
    throw "publish-macos.sh order invalid: dotnet publish must run before clang dylib build"
}
if ($libVlcIndex -lt 0 -or $libVlcIndex -lt $clangIndex) {
    throw "publish-macos.sh order invalid: LibVLC bundle must run after clang dylib build"
}

# Capability copy alignment
Assert-FileContains -Path $aboutWindow -Pattern 'About\.PlatformNote' -Message "AboutWindow must use localized About.PlatformNote keys"
Assert-FileContains -Path $settingsVm -Pattern 'MacNativePlayerSupport\.IsDylibPresent' -Message "Settings must gate macOS playback on dylib presence"
Assert-FileContains -Path $settingsVm -Pattern 'Settings\.Capability\.OnlinePreview\.Mac' -Message "Settings must use OnlinePreview.Mac detail key"
Assert-FileContains -Path $l10n -Pattern '\["Settings\.Capability\.OnlinePreview\.Mac"\]' -Message "Missing Settings.Capability.OnlinePreview.Mac l10n key"
Assert-FileContains -Path $l10n -Pattern '\["About\.PlatformNote\.Mac"\]' -Message "Missing About.PlatformNote.Mac l10n key"

Write-Host "[verify-macos] Building solution..."
dotnet build (Join-Path $Root "MusicVault.sln") -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed"
}

Write-Host "[verify-macos] macOS publish drill checks passed"
