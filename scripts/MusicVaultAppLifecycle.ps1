# MusicVault process lifecycle: stop before publish, start after update
param(
    [string]$Action = "Restart",
    [string]$ExePath = "",
    [int]$StopTimeoutSec = 8
)

$ErrorActionPreference = "Stop"

function Get-MusicVaultProcessNames {
    @("MusicVault", "go-music-api")
}

function Stop-MusicVaultProcesses {
    param([int]$TimeoutSec = 8)
    $names = Get-MusicVaultProcessNames
    $procs = Get-Process -Name $names -ErrorAction SilentlyContinue
    if (-not $procs) {
        Write-Host "No running MusicVault / go-music-api processes."
        return
    }

    foreach ($p in $procs) {
        Write-Host "Stopping $($p.ProcessName) (PID $($p.Id))..."
        try { $null = $p.CloseMainWindow() } catch { }
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $left = Get-Process -Name $names -ErrorAction SilentlyContinue
        if (-not $left) { break }
        Start-Sleep -Milliseconds 400
    }

    $still = Get-Process -Name $names -ErrorAction SilentlyContinue
    if ($still) {
        Write-Host "Force stopping remaining processes..."
        $still | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    Write-Host "MusicVault stopped."
}

function Resolve-MusicVaultExe {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path $Preferred)) { return (Resolve-Path $Preferred).Path }

    $root = Split-Path $PSScriptRoot -Parent
    $candidates = @(
        (Join-Path $root "publish\win-x64-vol\MusicVault.exe"),
        (Join-Path $root "publish\win-x64\MusicVault.exe"),
        (Join-Path $root "src\MusicVault.App\bin\Release\net8.0\win-x64\MusicVault.exe")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    return $null
}

function Start-MusicVaultApp {
    param([string]$PreferredExe)
    $exe = Resolve-MusicVaultExe -Preferred $PreferredExe
    if (-not $exe) {
        throw "MusicVault.exe not found. Publish first or pass -ExePath."
    }

    $dir = Split-Path $exe -Parent
    Write-Host "Starting: $exe"
    Start-Process -FilePath $exe -WorkingDirectory $dir | Out-Null
}

$act = $Action.Trim()
if ($act -eq "Stop") {
    Stop-MusicVaultProcesses -TimeoutSec $StopTimeoutSec
}
elseif ($act -eq "Start") {
    Start-MusicVaultApp -PreferredExe $ExePath
}
elseif ($act -eq "Restart") {
    Stop-MusicVaultProcesses -TimeoutSec $StopTimeoutSec
    Start-MusicVaultApp -PreferredExe $ExePath
}
else {
    throw "Unknown -Action '$Action'. Use Stop, Start, or Restart."
}
