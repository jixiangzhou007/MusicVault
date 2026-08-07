param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

$viewsRoot = Join-Path $Root "src\MusicVault.App\Views"
$files = Get-ChildItem -Path $viewsRoot -Filter "*.axaml" -Recurse

$replacements = @(
    @{ Old = 'Margin="16,20,28,0"'; New = 'Margin="{DynamicResource DetailHeroMargin}"' }
    @{ Old = 'Margin="12,12,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="12,0,12,12"'; New = 'Margin="{DynamicResource LibraryPaneFooterMargin}"' }
    @{ Old = 'Margin="12,12,16,8"'; New = 'Margin="{DynamicResource LibraryPaneToolbarMargin}"' }
    @{ Old = 'Margin="16,10,16,0"'; New = 'Margin="{DynamicResource PageHeaderMargin}"' }
    @{ Old = 'Margin="16,10,16,8"'; New = 'Margin="{DynamicResource PageHeaderBlockMargin}"' }
    @{ Old = 'Margin="16,10,16,4"'; New = 'Margin="{DynamicResource PageToolbarGridMargin}"' }
    @{ Old = 'Margin="16,0,16,12"'; New = 'Margin="{DynamicResource PageContentBottomMargin}"' }
    @{ Old = 'Margin="16,0,16,2"'; New = 'Margin="{DynamicResource TrackTableHeaderMargin}"' }
    @{ Old = 'Margin="16,4,16,8"'; New = 'Margin="{DynamicResource PageContentHorizontalMargin}"' }
    @{ Old = 'Margin="16,0,16,0"'; New = 'Margin="{DynamicResource PageContentHorizontalMargin}"' }
    @{ Old = 'Margin="12,0,12,0"'; New = 'Margin="{DynamicResource PageContentHorizontalMarginSm}"' }
    @{ Old = 'Margin="12,12,16,10"'; New = 'Margin="{DynamicResource PlayerBarInnerMargin}"' }
    @{ Old = 'Margin="18,10,18,12"'; New = 'Margin="{DynamicResource CompactPlayerOverlayMargin}"' }
    @{ Old = 'Margin="16,10,12,12"'; New = 'Margin="{DynamicResource BrowseDockMargin}"' }
    @{ Old = 'Margin="12,14,12,14"'; New = 'Margin="{DynamicResource PageContentMargin}"' }
    @{ Old = 'Margin="0,0,20,16"'; New = 'Margin="{DynamicResource ListScrollBottomMargin}"' }
    @{ Old = 'Margin="0,0,12,12"'; New = 'Margin="{DynamicResource CardGridItemMargin}"' }
    @{ Old = 'Margin="0,0,10,10"'; New = 'Margin="{DynamicResource SettingsSwatchGridMargin}"' }
    @{ Old = 'Margin="0,0,8,12"'; New = 'Margin="{DynamicResource MarginTrailingSm}"' }
    @{ Old = 'Margin="0,0,8,8"'; New = 'Margin="{DynamicResource ChipGridItemMargin}"' }
    @{ Old = 'Margin="0,0,0,16"'; New = 'Margin="{DynamicResource MarginBottomLg}"' }
    @{ Old = 'Margin="0,0,0,14"'; New = 'Margin="{DynamicResource MarginBottomMd}"' }
    @{ Old = 'Margin="0,0,0,12"'; New = 'Margin="{DynamicResource MarginBottomMd}"' }
    @{ Old = 'Margin="0,0,0,10"'; New = 'Margin="{DynamicResource EqualizerSectionGap}"' }
    @{ Old = 'Margin="0,0,0,8"'; New = 'Margin="{DynamicResource MarginBottomSm}"' }
    @{ Old = 'Margin="0,0,0,6"'; New = 'Margin="{DynamicResource SectionSplitMargin}"' }
    @{ Old = 'Margin="0,0,0,4"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,0,0,2"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,0,0,1"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,0,14,0"'; New = 'Margin="{DynamicResource PlayerBarCoverMargin}"' }
    @{ Old = 'Margin="0,0,12,0"'; New = 'Margin="{DynamicResource MarginTrailingMd}"' }
    @{ Old = 'Margin="0,0,16,0"'; New = 'Margin="{DynamicResource MarginTrailingLg}"' }
    @{ Old = 'Margin="0,0,8,0"'; New = 'Margin="{DynamicResource MarginTrailingSm}"' }
    @{ Old = 'Margin="0,0,6,0"'; New = 'Margin="{DynamicResource MarginToolbarBtnGap}"' }
    @{ Old = 'Margin="0,0,4,0"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="24,0,0,0"'; New = 'Margin="{DynamicResource SettingsIndentMargin}"' }
    @{ Old = 'Margin="22,0,0,0"'; New = 'Margin="{DynamicResource SettingsGathererIndentMargin}"' }
    @{ Old = 'Margin="12,0,20,0"'; New = 'Margin="{DynamicResource MarginLeadingMd}"' }
    @{ Old = 'Margin="12,0,16,0"'; New = 'Margin="{DynamicResource WindowLogoMargin}"' }
    @{ Old = 'Margin="12,0,10,0"'; New = 'Margin="{DynamicResource MarginLeadingMd}"' }
    @{ Old = 'Margin="12,0,0,0"'; New = 'Margin="{DynamicResource WindowNavLeadingMargin}"' }
    @{ Old = 'Margin="12,0,8,0"'; New = 'Margin="{DynamicResource MarginLeadingMd}"' }
    @{ Old = 'Margin="8,0,8,0"'; New = 'Margin="{DynamicResource TrackColCellMargin}"' }
    @{ Old = 'Margin="8,4,0,0"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="8,2,0,0"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="8,0,0,0"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="4,4,4,0"'; New = 'Margin="{DynamicResource CompactPlayerChromeMargin}"' }
    @{ Old = 'Margin="4,4,0,4"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="4,6,4,6"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,12,0,8"'; New = 'Margin="{DynamicResource MarginBottomMd}"' }
    @{ Old = 'Margin="0,12,0,4"'; New = 'Margin="{DynamicResource MarginBottomMd}"' }
    @{ Old = 'Margin="0,10,0,0"'; New = 'Margin="{DynamicResource EqualizerSectionGap}"' }
    @{ Old = 'Margin="0,8,0,6"'; New = 'Margin="{DynamicResource MarginTopSm}"' }
    @{ Old = 'Margin="0,8,0,0"'; New = 'Margin="{DynamicResource MarginTopSm}"' }
    @{ Old = 'Margin="0,6,6,24"'; New = 'Margin="{DynamicResource EqualizerRowGap}"' }
    @{ Old = 'Margin="0,6,0,0"'; New = 'Margin="{DynamicResource MarginTopSm}"' }
    @{ Old = 'Margin="0,4,0,14"'; New = 'Margin="{DynamicResource SettingsOverviewCardMargin}"' }
    @{ Old = 'Margin="0,4,0,4"'; New = 'Margin="{DynamicResource MarginTopXs}"' }
    @{ Old = 'Margin="0,4,0,0"'; New = 'Margin="{DynamicResource SettingsOverviewCardMargin}"' }
    @{ Old = 'Margin="0,2,0,0"'; New = 'Margin="{DynamicResource MarginTopXs}"' }
    @{ Old = 'Margin="16,12"'; New = 'Margin="{DynamicResource PageHeaderBlockMargin}"' }
    @{ Old = 'Margin="16,0,16,4"'; New = 'Margin="{DynamicResource PageToolbarGridMargin}"' }
    @{ Old = 'Margin="12,10,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="16,8,12,0"'; New = 'Margin="{DynamicResource PageHeaderBlockMargin}"' }
    @{ Old = 'Margin="16,0,16,8"'; New = 'Margin="{DynamicResource MarginBottomSm}"' }
    @{ Old = 'Margin="12,8,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="16,8,16,4"'; New = 'Margin="{DynamicResource PageToolbarGridMargin}"' }
    @{ Old = 'Margin="16,0,16,6"'; New = 'Margin="{DynamicResource SectionSplitMargin}"' }
    @{ Old = 'Margin="14,0,12,0"'; New = 'Margin="{DynamicResource MarginLeadingMd}"' }
    @{ Old = 'Margin="12,0,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="12,0,12,3"'; New = 'Margin="{DynamicResource TrackTableHeaderMargin}"' }
    @{ Old = 'Margin="16,12,16,16"'; New = 'Margin="{DynamicResource PageContentMargin}"' }
    @{ Old = 'Margin="16,24"'; New = 'Margin="{DynamicResource PageContentMargin}"' }
    @{ Old = 'Margin="20,16,20,0"'; New = 'Margin="{DynamicResource PageHeaderMargin}"' }
    @{ Old = 'Margin="20,12,20,0"'; New = 'Margin="{DynamicResource PageHeaderMargin}"' }
    @{ Old = 'Margin="20,0,20,20"'; New = 'Margin="{DynamicResource MarginBottomLg}"' }
    @{ Old = 'Margin="28,0,28,20"'; New = 'Margin="{DynamicResource DetailHeroMargin}"' }
    @{ Old = 'Margin="32,0,36,0"'; New = 'Margin="{DynamicResource DetailHeroMargin}"' }
    @{ Old = 'Margin="32"'; New = 'Margin="{DynamicResource PageContentMargin}"' }
    @{ Old = 'Margin="12,0"'; New = 'Margin="{DynamicResource MarginLeadingMd}"' }
    @{ Old = 'Margin="6,0"'; New = 'Margin="{DynamicResource MarginLeadingSm}"' }
    @{ Old = 'Margin="6,0,0,0"'; New = 'Margin="{DynamicResource MarginLeadingSm}"' }
    @{ Old = 'Margin="4,0,0,0"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="4,0,8,8"'; New = 'Margin="{DynamicResource ChipGridItemMargin}"' }
    @{ Old = 'Margin="4,0,0,8"'; New = 'Margin="{DynamicResource MarginBottomSm}"' }
    @{ Old = 'Margin="0,0,10,0"'; New = 'Margin="{DynamicResource MarginTrailingSm}"' }
    @{ Old = 'Margin="0,0,4,8"'; New = 'Margin="{DynamicResource MarginBottomSm}"' }
    @{ Old = 'Margin="8,0"'; New = 'Margin="{DynamicResource TrackColHeaderLastMargin}"' }
    @{ Old = 'Margin="8,4"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="8,6"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="10,8"'; New = 'Margin="{DynamicResource PageContentMargin}"' }
    @{ Old = 'Margin="0,2"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,3"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="0,4"'; New = 'Margin="{DynamicResource MarginTopXs}"' }
    @{ Old = 'Margin="0,6"'; New = 'Margin="{DynamicResource MarginTopSm}"' }
    @{ Old = 'Margin="4,0"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="4,0,0,0"'; New = 'Margin="{DynamicResource MarginBottomXs}"' }
    @{ Old = 'Margin="8,0,6,0"'; New = 'Margin="{DynamicResource FieldCaptionLeadingMargin}"' }
    @{ Old = 'Margin="12,0,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="12,10,12,6"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="20"'; New = 'Margin="{DynamicResource DesignSystemPageMargin}"' }
    @{ Old = 'Margin="12,0,12,8"'; New = 'Margin="{DynamicResource LibraryPaneHeaderMargin}"' }
    @{ Old = 'Margin="0,0,0,0"'; New = 'Margin="0"' }
)

$changed = 0
foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $original = $content
    foreach ($pair in $replacements) {
        $content = $content.Replace($pair.Old, $pair.New)
    }
    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        $changed++
        Write-Host "[migrate] $($file.Name)"
    }
}

Write-Host "[migrate] Updated $changed view file(s)"
