param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"

function Assert-Contains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw "Missing file: $Path"
    }

    $matches = Select-String -Path $Path -Pattern $Pattern -SimpleMatch
    if (-not $matches) {
        throw "Validation failed: $Message"
    }
}

function Assert-CapabilityBinding {
    param(
        [string]$Path,
        [string]$BindingExpression,
        [string]$CapabilityName
    )

    Assert-Contains -Path $Path -Pattern $BindingExpression -Message "$CapabilityName binding is missing"
}

Write-Host "[smoke] Validate top navigation and settings entry wiring..."

$mainWindow = Join-Path $Root "src\MusicVault.App\Views\MainWindow.axaml"
$settingsView = Join-Path $Root "src\MusicVault.App\Views\Pages\SettingsPageView.axaml"
$mainWindowVm = Join-Path $Root "src\MusicVault.App\ViewModels\MainWindowViewModel.cs"

# Top-level navigation tabs
Assert-Contains -Path $mainWindow -Pattern "CommandParameter=""{x:Static core:NavigationSection.AllTracks}""" -Message "Library navigation tab binding is missing"
Assert-Contains -Path $mainWindow -Pattern "CommandParameter=""{x:Static core:NavigationSection.Search}""" -Message "Discover navigation tab binding is missing"
Assert-Contains -Path $mainWindow -Pattern "CommandParameter=""{x:Static core:NavigationSection.Downloads}""" -Message "Transfers navigation tab binding is missing"
Assert-Contains -Path $mainWindow -Pattern "CommandParameter=""{x:Static core:NavigationSection.AudioTools}""" -Message "Tools navigation tab binding is missing"
Assert-Contains -Path $mainWindow -Pattern "CommandParameter=""{x:Static core:NavigationSection.Settings}""" -Message "Settings navigation tab binding is missing"

# Settings -> design system preview entry + capability matrix (chips via PlatformCapabilities)
$settingsVm = Join-Path $Root "src\MusicVault.App\ViewModels\Pages\SettingsPageViewModel.cs"
Assert-Contains -Path $settingsView -Pattern "Command=""{Binding OpenDesignSystemPreviewCommand}""" -Message "Settings design-system preview command is missing"
Assert-CapabilityBinding -Path $settingsView -BindingExpression "Text=""{Binding PlatformCapabilityTitle}""" -CapabilityName "Platform capability title"
Assert-Contains -Path $settingsView -Pattern "ItemsSource=""{Binding PlatformCapabilities}""" -Message "Platform capabilities list binding is missing"
Assert-Contains -Path $settingsVm -Pattern "LocalPlaybackCapabilityStatus" -Message "Local playback capability status property is missing"
Assert-Contains -Path $settingsVm -Pattern "EqualizerCapabilityStatus" -Message "Equalizer capability status property is missing"
Assert-CapabilityBinding -Path $settingsView -BindingExpression "IsVisible=""{Binding IsEqualizerSupported}""" -CapabilityName "Equalizer gating"
Assert-CapabilityBinding -Path $settingsView -BindingExpression "IsVisible=""{Binding IsBundledMusicApiFeatureAvailable}""" -CapabilityName "Bundled API gating"

$aboutWindow = Join-Path $Root "src\MusicVault.App\Views\Dialogs\AboutWindow.axaml.cs"
Assert-Contains -Path $aboutWindow -Pattern "About.PlatformNote" -Message "About dialog platform note must use localized keys"

# Section to page mappings in shell VM
Assert-Contains -Path $mainWindowVm -Pattern "[NavigationSection.Search] = Search" -Message "Search page mapping is missing"
Assert-Contains -Path $mainWindowVm -Pattern "[NavigationSection.Settings] = Settings" -Message "Settings page mapping is missing"
Assert-Contains -Path $mainWindowVm -Pattern "[NavigationSection.Downloads] = Downloads" -Message "Downloads page mapping is missing"

Write-Host "[smoke] Run shell/navigation focused tests..."
dotnet test (Join-Path $Root "MusicVault.sln") -c Release --filter "FullyQualifiedName~ShellNavigationCoordinatorTests|FullyQualifiedName~ShellHeaderStateStoreTests|FullyQualifiedName~MetadataSidebarStateStoreTests"
if ($LASTEXITCODE -ne 0) {
    throw "UI navigation smoke test failed"
}

Write-Host "[smoke] UI navigation smoke passed"
