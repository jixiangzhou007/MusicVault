# Design System Audit Report

- Generated: 2026-06-08 05:00:34
- Command: .\scripts\audit-design-system.ps1 -FailOnViolation
- FailOnViolation: True

## Summary

| Metric | Count |
|--------|------:|
| Views hex (all) | 4 |
| Views hex (violations) | 0 |
| Components hex (all) | 0 |
| Components hex (violations) | 0 |
| Views inline Margin lines | 12 |
| Views files with inline Margin | 4 |

## Views hex violations

(none)

## Views hex exempt

- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #1E3D28
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #6634C759
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #3D1F1C
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #66FF453A

## Components hex violations

(none)

## Components hex exempt

(none)

## Views inline Margin violations

- MusicVault.App\Views\Pages\HomePageView.axaml:56
- MusicVault.App\Views\Pages\HomePageView.axaml:79
- MusicVault.App\Views\Pages\SearchPageView.axaml:154
- MusicVault.App\Views\Pages\SettingsPageView.axaml:358
- MusicVault.App\Views\Pages\SettingsPageView.axaml:416
- MusicVault.App\Views\Pages\SettingsPageView.axaml:439
- MusicVault.App\Views\Pages\SettingsPageView.axaml:514
- MusicVault.App\Views\Pages\SettingsPageView.axaml:525
- MusicVault.App\Views\Pages\SettingsPageView.axaml:546
- MusicVault.App\Views\Pages\SettingsPageView.axaml:562
- MusicVault.App\Views\Pages\SettingsPageView.axaml:567
- MusicVault.App\Views\Player\PlayerBarView.axaml:14

## Views inline Margin (top 15 files)

| File | Lines |
|------|------:|
| MusicVault.App\Views\Pages\SettingsPageView.axaml | 8 |
| MusicVault.App\Views\Pages\HomePageView.axaml | 2 |
| MusicVault.App\Views\Player\PlayerBarView.axaml | 1 |
| MusicVault.App\Views\Pages\SearchPageView.axaml | 1 |

## Gate status

- Batch A/B/C/D design-system batches: completed
- Release gate (-FailOnViolation): hex + inline Margin literals in Views
