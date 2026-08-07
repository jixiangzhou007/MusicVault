# Design System Audit Report

- Generated: 2026-07-27 18:09:48
- Command: .\scripts\audit-design-system.ps1 -FailOnViolation
- FailOnViolation: True

## Summary

| Metric | Count |
|--------|------:|
| Views hex (all) | 4 |
| Views hex (violations) | 0 |
| Components hex (all) |  |
| Components hex (violations) | 1 |
| Views inline Margin lines | 7 |
| Views files with inline Margin | 2 |

## Views hex violations

(none)

## Views hex exempt

- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #1E3D28
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:80 -> #6634C759
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #3D1F1C
- MusicVault.App\Views\Pages\DesignSystemPageView.axaml:95 -> #66FF453A

## Components hex violations

- MusicVault.App\Styles\Components\CommandPalette.axaml:13 -> #40000000

## Components hex exempt

(none)

## Views inline Margin violations

- MusicVault.App\Views\Overlays\CommandPaletteView.axaml:14
- MusicVault.App\Views\Pages\SettingsPageView.axaml:80
- MusicVault.App\Views\Pages\SettingsPageView.axaml:86
- MusicVault.App\Views\Pages\SettingsPageView.axaml:92
- MusicVault.App\Views\Pages\SettingsPageView.axaml:98
- MusicVault.App\Views\Pages\SettingsPageView.axaml:178
- MusicVault.App\Views\Pages\SettingsPageView.axaml:260

## Views inline Margin (top 15 files)

| File | Lines |
|------|------:|
| MusicVault.App\Views\Pages\SettingsPageView.axaml | 6 |
| MusicVault.App\Views\Overlays\CommandPaletteView.axaml | 1 |

## Gate status

- Batch A/B/C/D design-system batches: completed
- Release gate (-FailOnViolation): hex + inline Margin literals in Views
