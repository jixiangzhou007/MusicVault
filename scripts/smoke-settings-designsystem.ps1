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

Write-Host "[smoke] Validate settings and design system structure..."

$settingsView = Join-Path $Root "src\MusicVault.App\Views\Pages\SettingsPageView.axaml"
$designView = Join-Path $Root "src\MusicVault.App\Views\Pages\DesignSystemPageView.axaml"
$appStyles = Join-Path $Root "src\MusicVault.App\Styles\AppStyles.axaml"

Assert-Contains -Path $settingsView -Pattern "antd-card-tab-nav" -Message "Settings card tab nav is missing"
Assert-Contains -Path $settingsView -Pattern "OpenDesignSystemPreviewCommand" -Message "Design system preview command is missing"
Assert-Contains -Path $designView -Pattern "Popconfirm" -Message "Design system Popconfirm section is missing"
Assert-Contains -Path $designView -Pattern "Result" -Message "Design system Result section is missing"
Assert-Contains -Path $designView -Pattern "ds-batch-a-theme-player" -Message "Design system theme/player chrome section is missing"
Assert-Contains -Path (Join-Path $Root "src\MusicVault.App\Styles\ThemeResources.axaml") -Pattern "PlayerChromeBaseBrush" -Message "PlayerChrome tokens missing in ThemeResources"
Assert-Contains -Path (Join-Path $Root "src\MusicVault.App\Styles\Components\PageLayout.axaml") -Pattern "library-pane-header" -Message "PageLayout margin classes missing"
Assert-Contains -Path $designView -Pattern "audit-design-system.ps1" -Message "Design system audit script reference is missing"
Assert-Contains -Path $appStyles -Pattern "Components/Popconfirm.axaml" -Message "Popconfirm style include missing"
Assert-Contains -Path $appStyles -Pattern "Components/Result.axaml" -Message "Result style include missing"
Assert-Contains -Path $appStyles -Pattern "Components/Steps.axaml" -Message "Steps style include missing"
Assert-Contains -Path $appStyles -Pattern "Components/Skeleton.axaml" -Message "Skeleton style include missing"
Assert-Contains -Path $appStyles -Pattern "Components/Badge.axaml" -Message "Badge style include missing"
Assert-Contains -Path $appStyles -Pattern "Components/Popover.axaml" -Message "Popover style include missing"

Write-Host "[smoke] Run design system audit..."
& (Join-Path $PSScriptRoot "audit-design-system.ps1") -Root $Root -FailOnViolation
if ($LASTEXITCODE -ne 0) {
    throw "Design system audit script failed"
}

Write-Host "[smoke] Run focused unit tests..."
dotnet test (Join-Path $Root "MusicVault.sln") -c Release --filter "FullyQualifiedName~Localization|FirstRunWizard|DownloadQualityMigration|Theme"
if ($LASTEXITCODE -ne 0) {
    throw "Settings/design system smoke test failed"
}

Write-Host "[smoke] Settings/DesignSystem smoke passed"
