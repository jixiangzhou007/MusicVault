# Probe MusicVault bundled / configured API endpoints.
param(
    [string]$AppDir = "",
    [string]$GoMusicApiUrl = "http://localhost:8080",
    [string]$NetEaseApiUrl = "http://localhost:3000",
    [switch]$StartBundled
)

$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
if (-not $AppDir) { $AppDir = Join-Path $root "publish\win-x64-vol" }

function Test-ApiEndpoint {
    param(
        [string]$Label,
        [string]$Url,
        [int]$TimeoutSec = 12,
        [scriptblock]$Validate = { param($json) $json.code -eq 200 }
    )

    try {
        $resp = Invoke-RestMethod -Uri $Url -TimeoutSec $TimeoutSec
        $ok = & $Validate $resp
        $detail = if ($resp.code) { "code=$($resp.code)" } else { "ok" }
        if ($resp.list -is [Array]) { $detail += " list=$($resp.list.Count)" }
        if ($resp.result) { $detail += " hasResult" }
        [pscustomobject]@{ Service = $Label; Url = $Url; Ok = [bool]$ok; Detail = $detail }
    }
    catch {
        [pscustomobject]@{ Service = $Label; Url = $Url; Ok = $false; Detail = $_.Exception.Message }
    }
}

if ($StartBundled) {
    Write-Host "Starting bundled APIs..."
    & (Join-Path $PSScriptRoot "start-netease-cloud-music-api.ps1") -AppDir $AppDir | Out-Host
    $goExe = Join-Path $AppDir "api\go-music-api.exe"
    if (Test-Path $goExe) {
        $goProc = Get-Process go-music-api -ErrorAction SilentlyContinue
        if (-not $goProc) {
            Start-Process -FilePath $goExe -WorkingDirectory (Split-Path $goExe) -WindowStyle Minimized
            Start-Sleep -Seconds 3
        }
    }
}

$results = @()

# go-music-api (unified)
$goCandidates = @($GoMusicApiUrl.TrimEnd('/'), "http://localhost:18080")
$goBase = $null
foreach ($candidate in $goCandidates | Select-Object -Unique) {
    $probe = Test-ApiEndpoint "go-music-api health ($candidate)" "$candidate/api/v1/music/search?q=ping&type=song&sources=kugou"
    $results += $probe
    if ($probe.Ok) { $goBase = $candidate; break }
}
if (-not $goBase) { $goBase = $GoMusicApiUrl.TrimEnd('/') }

if ($goBase) {
$results += Test-ApiEndpoint "go-music-api search netease" "$goBase/api/v1/music/search?q=test&type=song&sources=netease"
$results += Test-ApiEndpoint "go-music-api qr sources" "$goBase/api/v1/system/qr_login/sources" -Validate { param($j) $j.data -or $j.code -eq 200 }
$results += Test-ApiEndpoint "go-music-api cookies" "$goBase/api/v1/system/cookies" -Validate { param($j) $null -ne $j }
}

# netease-cloud-music-api (extension)
$neBase = $NetEaseApiUrl.TrimEnd('/')
$results += Test-ApiEndpoint "netease search" "$neBase/search?keywords=ping&type=1&limit=1"
$results += Test-ApiEndpoint "netease cloudsearch" "$neBase/cloudsearch?keywords=ping&type=1&limit=1"
$results += Test-ApiEndpoint "netease toplist (charts)" "$neBase/toplist" -Validate { param($j) $j.code -eq 200 -and $j.list.Count -gt 0 }
$results += Test-ApiEndpoint "netease playlist detail" "$neBase/playlist/detail?id=3778678" -Validate { param($j) $j.code -eq 200 -and ($j.playlist -or $j.data) }

Write-Host ""
Write-Host "=== MusicVault API probe ==="
Write-Host "AppDir: $AppDir"
Write-Host "GoMusicApi: $goBase"
Write-Host "NetEaseApi: $neBase"
Write-Host ""

$results | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.Ok })
if ($failed.Count -gt 0) {
    Write-Host "FAILED: $($failed.Count) / $($results.Count)"
    exit 1
}

Write-Host "All $($results.Count) probes passed."
exit 0
