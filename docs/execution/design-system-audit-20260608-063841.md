# Design System Audit Report

- Generated: 2026-06-08 06:38:41
- Command: .\scripts\audit-design-system.ps1 -FailOnViolation
- FailOnViolation: True

## Summary

| Metric | Count |
|--------|------:|
| Views hex (all) | 4 |
| Views hex (violations) | 0 |
| Components hex (all) | 0 |
| Components hex (violations) | 0 |
| Views inline Margin lines | 0 |
| Views files with inline Margin | 0 |

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

(none)

## Views inline Margin (top 15 files)

| File | Lines |
|------|------:|

## Gate status

- Batch A/B/C/D design-system batches: completed
- Release gate (-FailOnViolation): hex + inline Margin literals in Views
