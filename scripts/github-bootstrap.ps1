# MusicVault: init local git and prepare GitHub push.
# Does not modify git user.name / user.email; does not push.
param(
    [string]$RemoteUrl = "",
    [switch]$Commit,
    [switch]$SkipCommit
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$defaultsPath = Join-Path $root "src\MusicVault.App\Services\AppUpdateDefaults.cs"
$defaultRepo = "https://github.com/jixiangzhou007/MusicVault"
if (Test-Path $defaultsPath) {
    $m = Select-String -Path $defaultsPath -Pattern 'DefaultGithubRepoUrl\s*=\s*"([^"]+)"' | Select-Object -First 1
    if ($m) { $defaultRepo = $m.Matches[0].Groups[1].Value }
}

Write-Host "MusicVault GitHub bootstrap" -ForegroundColor Cyan
Write-Host "Default update feed: $defaultRepo" -ForegroundColor DarkGray
Write-Host ""

$isRepo = Test-Path (Join-Path $root ".git")
if (-not $isRepo) {
    Write-Host "Initializing git repository..." -ForegroundColor Yellow
    git init -b main
    if ($LASTEXITCODE -ne 0) { throw "git init failed" }
} else {
    Write-Host "Already a git repository." -ForegroundColor Green
}

$status = git status --porcelain
if ($status -and -not $SkipCommit) {
    if (-not $Commit) {
        Write-Host "Working tree has changes. Re-run with -Commit to create the initial commit:" -ForegroundColor Yellow
        Write-Host "  .\scripts\github-bootstrap.ps1 -Commit" -ForegroundColor White
    } else {
        git add -A
        $msg = "chore: initial import for GitHub + Velopack updates"
        git commit -m $msg
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Commit failed (check git user.name / user.email)." -ForegroundColor Red
            exit 1
        }
        Write-Host "Initial commit created." -ForegroundColor Green
    }
} elseif (-not $status) {
    Write-Host "Working tree clean." -ForegroundColor Green
}

$hasOrigin = $false
git remote get-url origin 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { $hasOrigin = $true }

if ($RemoteUrl) {
    if ($hasOrigin) {
        git remote set-url origin $RemoteUrl
    } else {
        git remote add origin $RemoteUrl
    }
    Write-Host "origin => $RemoteUrl" -ForegroundColor Green
    $hasOrigin = $true
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Create empty repo on GitHub (no README), ideally matching:" -ForegroundColor White
Write-Host "       $defaultRepo" -ForegroundColor Yellow
Write-Host "  2. Install GitHub CLI: https://cli.github.com/  then:" -ForegroundColor White
Write-Host "       gh auth login" -ForegroundColor Yellow
Write-Host "       gh repo create MusicVault --public --source=. --remote=origin --push" -ForegroundColor Yellow
Write-Host "     Or manually:" -ForegroundColor White
if (-not $hasOrigin) {
    Write-Host "       git remote add origin git@github.com:OWNER/MusicVault.git" -ForegroundColor Yellow
}
Write-Host "       git push -u origin main" -ForegroundColor Yellow
Write-Host "  3. Ship a release:" -ForegroundColor White
Write-Host "       .\scripts\publish-windows.ps1 -NoRestart" -ForegroundColor Yellow
Write-Host "       .\scripts\velopack-release.ps1" -ForegroundColor Yellow
Write-Host "       .\scripts\publish-github-release.ps1" -ForegroundColor Yellow
Write-Host "  See docs\ONLINE_UPDATE.md for the full guide." -ForegroundColor DarkGray
