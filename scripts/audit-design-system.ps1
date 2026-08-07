param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "docs\execution"),
    [switch]$FailOnViolation
)

$ErrorActionPreference = "Stop"

$viewsRoot = Join-Path $Root "src\MusicVault.App\Views"
$componentsRoot = Join-Path $Root "src\MusicVault.App\Styles\Components"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $OutputDir "design-system-audit-$timestamp.md"

$exemptViewPatterns = @(
    "*\DesignSystemPageView.axaml",
    "*\DesignSystemWindow.axaml"
)
$exemptComponentPatterns = @(
    "*\Result.axaml",
    "*\Button.axaml",
    "*\Feedback.axaml",
    "*\Steps.axaml"
)

function Test-ExemptPath {
    param(
        [string]$RelativePath,
        [string[]]$Patterns
    )
    foreach ($pattern in $Patterns) {
        if ($RelativePath -like $pattern) { return $true }
    }
    return $false
}

function Get-HexMatches {
    param([string[]]$Files)
    $results = @()
    foreach ($file in $Files) {
        $lines = Select-String -Path $file -Pattern '#[0-9A-Fa-f]{3,8}' -AllMatches
        foreach ($line in $lines) {
            foreach ($match in $line.Matches) {
                $results += [PSCustomObject]@{
                    File = $file
                    Line = $line.LineNumber
                    Value = $match.Value
                }
            }
        }
    }
    return $results
}

function Get-MarginMatches {
    param([string[]]$Files)
    $results = @()
    foreach ($file in $Files) {
        $lines = Select-String -Path $file -Pattern 'Margin\s*=\s*"(?!\{)[^"]*[0-9]' -AllMatches
        foreach ($line in $lines) {
            $results += [PSCustomObject]@{
                File = $file
                Line = $line.LineNumber
            }
        }
    }
    return $results
}

function Get-RelativeAppPath {
    param([string]$FullPath)
    $marker = "MusicVault.App\"
    $idx = $FullPath.IndexOf($marker)
    if ($idx -ge 0) {
        return $FullPath.Substring($idx)
    }
    return $FullPath
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$viewFiles = @(Get-ChildItem -Path $viewsRoot -Filter "*.axaml" -Recurse | ForEach-Object { $_.FullName })
$componentFiles = @(Get-ChildItem -Path $componentsRoot -Filter "*.axaml" -Recurse | ForEach-Object { $_.FullName })

$hexInViews = Get-HexMatches -Files $viewFiles
$hexInComponents = Get-HexMatches -Files $componentFiles
$marginInViews = Get-MarginMatches -Files $viewFiles

function Split-Exempt {
    param(
        [array]$Items,
        [string[]]$ExemptPatterns
    )
    $violations = @()
    $exempt = @()
    foreach ($item in $Items) {
        $rel = Get-RelativeAppPath $item.File
        if (Test-ExemptPath -RelativePath $rel -Patterns $ExemptPatterns) {
            $exempt += $item
        }
        else {
            $violations += $item
        }
    }
    return @{ Violations = $violations; Exempt = $exempt }
}

$viewHexSplit = Split-Exempt -Items $hexInViews -ExemptPatterns $exemptViewPatterns
$componentHexSplit = Split-Exempt -Items $hexInComponents -ExemptPatterns $exemptComponentPatterns
$marginByFile = $marginInViews | Group-Object File | Sort-Object Count -Descending

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Design System Audit Report")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$commandLine = ".\scripts\audit-design-system.ps1"
if ($FailOnViolation) {
    $commandLine += " -FailOnViolation"
}
[void]$sb.AppendLine("- Command: $commandLine")
[void]$sb.AppendLine("- FailOnViolation: $FailOnViolation")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Summary")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Metric | Count |")
[void]$sb.AppendLine("|--------|------:|")
[void]$sb.AppendLine("| Views hex (all) | $($hexInViews.Count) |")
[void]$sb.AppendLine("| Views hex (violations) | $($viewHexSplit.Violations.Count) |")
[void]$sb.AppendLine("| Components hex (all) | $($hexInComponents.Count) |")
[void]$sb.AppendLine("| Components hex (violations) | $($componentHexSplit.Violations.Count) |")
[void]$sb.AppendLine("| Views inline Margin lines | $($marginInViews.Count) |")
[void]$sb.AppendLine("| Views files with inline Margin | $($marginByFile.Count) |")
[void]$sb.AppendLine("")

function Append-HexSection {
    param(
        [System.Text.StringBuilder]$Builder,
        [string]$Title,
        [array]$Items
    )
    [void]$Builder.AppendLine("## $Title")
    [void]$Builder.AppendLine("")
    if ($Items.Count -eq 0) {
        [void]$Builder.AppendLine("(none)")
    }
    else {
        foreach ($item in $Items) {
            $rel = Get-RelativeAppPath $item.File
            [void]$Builder.AppendLine("- $rel`:$($item.Line) -> $($item.Value)")
        }
    }
    [void]$Builder.AppendLine("")
}

Append-HexSection -Builder $sb -Title "Views hex violations" -Items $viewHexSplit.Violations
Append-HexSection -Builder $sb -Title "Views hex exempt" -Items $viewHexSplit.Exempt
Append-HexSection -Builder $sb -Title "Components hex violations" -Items $componentHexSplit.Violations
Append-HexSection -Builder $sb -Title "Components hex exempt" -Items $componentHexSplit.Exempt

if ($marginInViews.Count -eq 0) {
    [void]$sb.AppendLine("## Views inline Margin violations")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("(none)")
    [void]$sb.AppendLine("")
}
else {
    [void]$sb.AppendLine("## Views inline Margin violations")
    [void]$sb.AppendLine("")
    foreach ($item in $marginInViews) {
        $rel = Get-RelativeAppPath $item.File
        [void]$sb.AppendLine("- $rel`:$($item.Line)")
    }
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("## Views inline Margin (top 15 files)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| File | Lines |")
[void]$sb.AppendLine("|------|------:|")
foreach ($group in ($marginByFile | Select-Object -First 15)) {
    $rel = Get-RelativeAppPath $group.Name
    [void]$sb.AppendLine("| $rel | $($group.Count) |")
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Gate status")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- Batch A/B/C/D design-system batches: completed")
[void]$sb.AppendLine("- Release gate (`-FailOnViolation`): hex + inline Margin literals in Views")

[System.IO.File]::WriteAllText($reportPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

Write-Host "[audit] Report: $reportPath"
Write-Host "[audit] View hex violations: $($viewHexSplit.Violations.Count)"
Write-Host "[audit] Component hex violations: $($componentHexSplit.Violations.Count)"
Write-Host "[audit] View inline Margin lines: $($marginInViews.Count)"

$hexViolationCount = $viewHexSplit.Violations.Count + $componentHexSplit.Violations.Count
$marginViolationCount = $marginInViews.Count
if ($FailOnViolation) {
    $messages = @()
    if ($hexViolationCount -gt 0) {
        $messages += "$hexViolationCount non-exempt hardcoded color(s)"
    }
    if ($marginViolationCount -gt 0) {
        $messages += "$marginViolationCount inline Margin literal(s) in Views"
    }
    if ($messages.Count -gt 0) {
        throw "Design system audit failed: $($messages -join '; ')."
    }
    Write-Host "[audit] FailOnViolation gate passed"
}

exit 0
