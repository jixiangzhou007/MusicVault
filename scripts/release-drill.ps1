param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [switch]$SkipPublish,
    [switch]$SkipSmoke,
    [switch]$StrictSmoke
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) { $OutputDir = Join-Path $root "publish\win-x64-vol" }
$docsDir = Join-Path $root "docs\execution"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $docsDir "release-drill-$timestamp.md"
$artifactsDir = Join-Path $docsDir "release-drill-artifacts"
$runArtifactsDir = Join-Path $artifactsDir $timestamp
$appProject = Join-Path $root "src\MusicVault.App\MusicVault.App.csproj"
$smokeScript = Join-Path $PSScriptRoot "smoke-settings-designsystem.ps1"
$uiSmokeScript = Join-Path $PSScriptRoot "smoke-ui-navigation.ps1"
$headlessSmokeScript = Join-Path $PSScriptRoot "smoke-ui-headless.ps1"
$publishScript = Join-Path $PSScriptRoot "publish-with-api.ps1"
$auditScript = Join-Path $PSScriptRoot "audit-design-system.ps1"
$capabilityGuardrailsDoc = Join-Path $root "docs\execution\phase-4-capability-guardrails.md"

if (-not (Test-Path $docsDir)) {
    New-Item -ItemType Directory -Path $docsDir | Out-Null
}
if (-not (Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir | Out-Null
}
if (-not (Test-Path $runArtifactsDir)) {
    New-Item -ItemType Directory -Path $runArtifactsDir | Out-Null
}
if ($StrictSmoke -and $SkipSmoke) {
    throw "StrictSmoke is enabled, so SkipSmoke is not allowed."
}

$report = [System.Collections.Generic.List[string]]::new()
$stepResults = [System.Collections.Generic.List[object]]::new()
$failed = $false
$smokePolicy = if ($StrictSmoke) { "strict" } else { "normal" }

function Add-ReportLine {
    param([string]$Line)
    $script:report.Add($Line)
}

function Get-CmdOutput {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    try {
        $output = & $FileName @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            return "failed(exit=$LASTEXITCODE)"
        }

        return ($output | Select-Object -First 1).ToString().Trim()
    }
    catch {
        return "failed($($_.Exception.Message))"
    }
}

function Get-PathState {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return "missing"
    }

    $item = Get-Item $Path -ErrorAction SilentlyContinue
    if ($null -eq $item) {
        return "exists(unreadable)"
    }

    if ($item.PSIsContainer) {
        return "exists(dir)"
    }

    return "exists(file, $([Math]::Round($item.Length / 1KB, 1))KB, mtime=$($item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")))"
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name"
    $startedAt = Get-Date
    $logFileName = ($Name -replace '[^a-zA-Z0-9]+', '-').Trim('-').ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($logFileName)) {
        $logFileName = "step"
    }

    $logPath = Join-Path $runArtifactsDir "$logFileName.log"
    try {
        & $Action *>&1 | Tee-Object -FilePath $logPath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "exit code: $LASTEXITCODE"
        }

        $elapsed = [Math]::Round(((Get-Date) - $startedAt).TotalSeconds, 1)
        Add-ReportLine "- PASS: $Name (${elapsed}s)"
        Add-ReportLine "  - Log: $logPath"
        $stepResults.Add([pscustomobject]@{
            Name = $Name
            Status = "PASS"
            ElapsedSeconds = $elapsed
            LogPath = $logPath
            Error = ""
        })
    }
    catch {
        $elapsed = [Math]::Round(((Get-Date) - $startedAt).TotalSeconds, 1)
        Add-ReportLine "- FAIL: $Name (${elapsed}s)"
        Add-ReportLine "  - Error: $($_.Exception.Message)"
        Add-ReportLine "  - Log: $logPath"
        $stepResults.Add([pscustomobject]@{
            Name = $Name
            Status = "FAIL"
            ElapsedSeconds = $elapsed
            LogPath = $logPath
            Error = $_.Exception.Message
        })
        $script:failed = $true
        throw
    }
}

$appVersion = "unknown"
try {
    [xml]$csproj = Get-Content -Path $appProject
    $appVersion = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($appVersion)) {
        $appVersion = "unknown"
    }
}
catch {
    $appVersion = "unknown"
}

$dotnetVersion = Get-CmdOutput -FileName "dotnet" -Arguments @("--version")
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if ($null -ne $gitCmd) {
    $gitBranch = Get-CmdOutput -FileName $gitCmd.Source -Arguments @("branch", "--show-current")
    $gitCommit = Get-CmdOutput -FileName $gitCmd.Source -Arguments @("rev-parse", "--short", "HEAD")
}
else {
    $gitBranch = "unavailable"
    $gitCommit = "unavailable"
}
$startupErrorLog = Join-Path $env:LOCALAPPDATA "MusicVault\startup-error.log"
$appDataDir = Join-Path $env:LOCALAPPDATA "MusicVault"
$publishExe = Join-Path $OutputDir "MusicVault.exe"

Add-ReportLine "# Release Drill Report"
Add-ReportLine ""
Add-ReportLine "- Time: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")"
Add-ReportLine "- RunId: $timestamp"
Add-ReportLine "- AppVersion: $appVersion"
Add-ReportLine "- Configuration: $Configuration"
Add-ReportLine "- Runtime: $Runtime"
Add-ReportLine "- SkipPublish: $SkipPublish"
Add-ReportLine "- SkipSmoke: $SkipSmoke"
Add-ReportLine "- StrictSmoke: $StrictSmoke"
Add-ReportLine "- SmokePolicy: $smokePolicy"
Add-ReportLine "- ReportPath: $reportPath"
Add-ReportLine "- ArtifactsDir: $runArtifactsDir"
Add-ReportLine "- CapabilityGuardrailsDoc: $capabilityGuardrailsDoc"
Add-ReportLine ""
Add-ReportLine "## Environment"
Add-ReportLine "- Machine: $env:COMPUTERNAME"
Add-ReportLine "- User: $env:USERNAME"
Add-ReportLine "- OS: $([Environment]::OSVersion.VersionString)"
Add-ReportLine "- PowerShell: $($PSVersionTable.PSVersion)"
Add-ReportLine "- Dotnet: $dotnetVersion"
Add-ReportLine "- GitBranch: $gitBranch"
Add-ReportLine "- GitCommit: $gitCommit"
Add-ReportLine ""
Add-ReportLine "## Key Log Paths"
Add-ReportLine "- $startupErrorLog -> $(Get-PathState $startupErrorLog)"
Add-ReportLine "- $appDataDir -> $(Get-PathState $appDataDir)"
Add-ReportLine "- $publishExe -> $(Get-PathState $publishExe)"
Add-ReportLine "- $capabilityGuardrailsDoc -> $(Get-PathState $capabilityGuardrailsDoc)"
Add-ReportLine ""
Add-ReportLine "## Steps"

Push-Location $root
try {
    Invoke-Step -Name "dotnet format verify" -Action {
        dotnet format MusicVault.sln --verify-no-changes --verbosity minimal
    }

    Invoke-Step -Name "dotnet build" -Action {
        dotnet build MusicVault.sln -c $Configuration
    }

    Invoke-Step -Name "dotnet test" -Action {
        dotnet test MusicVault.sln -c $Configuration --no-build
    }

    Invoke-Step -Name "capability guardrails check" -Action {
        if (-not (Test-Path $capabilityGuardrailsDoc)) {
            throw "Missing guardrails doc: $capabilityGuardrailsDoc"
        }
        Write-Host "guardrails doc exists"
    }

    Invoke-Step -Name "design system audit" -Action {
        & $auditScript -Root $root -FailOnViolation
    }

    if (-not $SkipSmoke) {
        Invoke-Step -Name "smoke settings/designsystem" -Action {
            & $smokeScript
        }
        Invoke-Step -Name "smoke ui navigation" -Action {
            & $uiSmokeScript
        }
        Invoke-Step -Name "smoke ui headless" -Action {
            & $headlessSmokeScript -Configuration $Configuration
        }
    }
    else {
        Add-ReportLine "- SKIP: smoke settings/designsystem"
        Add-ReportLine "- SKIP: smoke ui navigation"
        Add-ReportLine "- SKIP: smoke ui headless"
    }

    if (-not $SkipPublish) {
        Invoke-Step -Name "publish with api and restart" -Action {
            & $publishScript -OutputDir $OutputDir -Restart
        }

        Invoke-Step -Name "verify bundled api artifacts" -Action {
            $goExe = Join-Path $OutputDir "api\go-music-api.exe"
            $neEntry = Join-Path $OutputDir "api\netease-cloud-music-api\entry-point.txt"
            if (-not (Test-Path $goExe)) { throw "Missing $goExe" }
            if (-not (Test-Path $neEntry)) { throw "Missing $neEntry" }
            Write-Host "bundled api artifacts OK"
        }

        $probeScript = Join-Path $PSScriptRoot "probe-all-apis.ps1"
        if (Test-Path $probeScript) {
            Invoke-Step -Name "probe bundled apis" -Action {
                & $probeScript -AppDir $OutputDir -StartBundled
            }
        }
    }
    else {
        Add-ReportLine "- SKIP: publish with api and restart"
    }
}
finally {
    Pop-Location
    Add-ReportLine ""
    Add-ReportLine "## Summary"
    $passCount = ($stepResults | Where-Object { $_.Status -eq "PASS" }).Count
    $failCount = ($stepResults | Where-Object { $_.Status -eq "FAIL" }).Count
    Add-ReportLine "- PassedSteps: $passCount"
    Add-ReportLine "- FailedSteps: $failCount"
    Add-ReportLine "- Overall: $(if ($failed) { "FAIL" } else { "PASS" })"
    Add-ReportLine "- SmokePolicy: $smokePolicy"
    $guardrailsChecked = $stepResults | Where-Object {
        $_.Name -eq "capability guardrails check" -and $_.Status -eq "PASS"
    }
    Add-ReportLine "- CapabilityGuardrails: $(if ($guardrailsChecked) { "checked" } else { "missing-or-failed" })"

    if ($failCount -gt 0) {
        Add-ReportLine ""
        Add-ReportLine "## Failure Details"
        foreach ($item in $stepResults | Where-Object { $_.Status -eq "FAIL" }) {
            Add-ReportLine "- $($item.Name): $($item.Error)"
            Add-ReportLine "  - Log: $($item.LogPath)"
        }
    }

    Add-ReportLine ""
    Add-ReportLine "## Output"
    Add-ReportLine "- PublishDir: $OutputDir"
    Set-Content -Path $reportPath -Value ($report -join [Environment]::NewLine) -Encoding UTF8
}

Write-Host ""
Write-Host "Release drill completed."
Write-Host "Report: $reportPath"
