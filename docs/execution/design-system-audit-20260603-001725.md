# Design System Audit Report

- Generated: 2026-06-03 00:17:25
- Command: .\scripts\audit-design-system.ps1

## Summary

| Metric | Count |
|--------|------:|
| Views hex (all) | 4 |
| Views hex (violations) | 0 |
| Components hex (all) | 19 |
| Components hex (violations) | 0 |
| Views inline Margin lines | 67 |
| Views files with inline Margin | 22 |

## Views hex violations

(none)

## Views hex exempt

- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #1E3D28
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #6634C759
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #3D1F1C
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #66FF453A

## Components hex violations

(none)

## Components hex exempt (Batch B target)

- MusicVault.App\Styles\Components\Button.axaml:443 -> #FA8C16
- MusicVault.App\Styles\Components\Button.axaml:444 -> #FA8C16
- MusicVault.App\Styles\Components\Button.axaml:449 -> #FA8C16
- MusicVault.App\Styles\Components\Button.axaml:450 -> #FA8C16
- MusicVault.App\Styles\Components\Button.axaml:455 -> #FA8C16
- MusicVault.App\Styles\Components\Button.axaml:456 -> #FA8C16
- MusicVault.App\Styles\Components\Feedback.axaml:121 -> #1A2D4D
- MusicVault.App\Styles\Components\Feedback.axaml:122 -> #40007AFF
- MusicVault.App\Styles\Components\Feedback.axaml:131 -> #3D3520
- MusicVault.App\Styles\Components\Feedback.axaml:141 -> #3D1F1C
- MusicVault.App\Styles\Components\Feedback.axaml:142 -> #66FF453A
- MusicVault.App\Styles\Components\Feedback.axaml:145 -> #FF9A8F
- MusicVault.App\Styles\Components\Feedback.axaml:148 -> #FF9A8F
- MusicVault.App\Styles\Components\Result.axaml:22 -> #1E3D28
- MusicVault.App\Styles\Components\Result.axaml:23 -> #6634C759
- MusicVault.App\Styles\Components\Result.axaml:30 -> #3D1F1C
- MusicVault.App\Styles\Components\Result.axaml:31 -> #66FF453A
- MusicVault.App\Styles\Components\Steps.axaml:37 -> #1E3D28
- MusicVault.App\Styles\Components\Steps.axaml:41 -> #3D1F1C

## Views inline Margin (top 15 files)

| File | Lines |
|------|------:|
| MusicVault.App\Views\Overlays\CombinedLabelsSearchModalView.axaml | 14 |
| MusicVault.App\Views\Pages\HomePageView.axaml | 7 |
| MusicVault.App\Views\Sidebar\RightSidebarView.axaml | 5 |
| MusicVault.App\Views\Pages\SearchPageView.axaml | 5 |
| MusicVault.App\Views\Pages\ArtistsPageView.axaml | 4 |
| MusicVault.App\Views\Pages\DownloadsPageView.axaml | 4 |
| MusicVault.App\Views\Pages\PlaylistsPageView.axaml | 4 |
| MusicVault.App\Views\Pages\SettingsPageView.axaml | 3 |
| MusicVault.App\Views\Pages\DesignSystemPageView.axaml | 3 |
| MusicVault.App\Views\Overlays\BatchScrapeResultModalView.axaml | 3 |
| MusicVault.App\Views\Player\NowPlayingFullscreenView.axaml | 2 |
| MusicVault.App\Views\Pages\TracksPageView.axaml | 2 |
| MusicVault.App\Views\Overlays\MetadataLyricsEditorModalView.axaml | 2 |
| MusicVault.App\Views\Dialogs\DesignSystemWindow.axaml | 1 |
| MusicVault.App\Views\Player\CompactPlayerView.axaml | 1 |

## Next batches

- Batch B: PlayerChrome* tokens (completed)
- Batch C: Top margin files -> Spacing.axaml classes
- Batch D: -FailOnViolation in release drill
