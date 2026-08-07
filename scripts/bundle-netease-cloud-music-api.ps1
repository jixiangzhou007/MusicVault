# Bundle netease-cloud-music-api into publish output (api/netease-cloud-music-api + api/node).
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,
    [string]$PackageVersion = "4.30.0",
    [string]$NodeVersion = "22.12.0",
    [switch]$SkipNode
)

$ErrorActionPreference = "Stop"

function Test-Cmd($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

if (-not (Test-Cmd npm)) {
    Write-Error "npm not found. Install Node.js LTS before publishing."
}

$apiRoot = Join-Path $OutputDir "api"
$apiDir = Join-Path $apiRoot "netease-cloud-music-api"
$nodeDir = Join-Path $apiRoot "node"
New-Item -ItemType Directory -Force -Path $apiDir | Out-Null

$packageJson = @"
{
  "private": true,
  "dependencies": {
    "netease-cloud-music-api-alger": "$PackageVersion"
  }
}
"@
Set-Content -Path (Join-Path $apiDir "package.json") -Value $packageJson -Encoding UTF8

Write-Host "npm install netease-cloud-music-api-alger@$PackageVersion ..."
Push-Location $apiDir
try {
    npm install --omit=dev --no-fund --no-audit
}
finally {
    Pop-Location
}

$entryCandidates = @(
    "node_modules\netease-cloud-music-api-alger\app.js",
    "node_modules\netease-cloud-music-api-alger\main.js",
    "app.js"
)
$entryRel = $null
foreach ($rel in $entryCandidates) {
    if (Test-Path (Join-Path $apiDir $rel)) {
        $entryRel = $rel
        break
    }
}
if (-not $entryRel) {
    Write-Error "Entry script not found under $apiDir"
}
Set-Content -Path (Join-Path $apiDir "entry-point.txt") -Value $entryRel -Encoding UTF8
Set-Content -Path (Join-Path $apiRoot "netease-api-version.txt") -Value $PackageVersion -Encoding UTF8

$scriptsDir = Join-Path $apiDir "scripts"
New-Item -ItemType Directory -Force -Path $scriptsDir | Out-Null
$matchScriptSrc = Join-Path $PSScriptRoot "netease-audio-match.js"
if (Test-Path $matchScriptSrc) {
    Copy-Item $matchScriptSrc (Join-Path $scriptsDir "netease-audio-match.js") -Force
}

if (-not $SkipNode) {
    $nodeExe = Join-Path $nodeDir "node.exe"
    if (-not (Test-Path $nodeExe)) {
        New-Item -ItemType Directory -Force -Path $nodeDir | Out-Null
        $zipName = "node-v$NodeVersion-win-x64.zip"
        $zipUrl = "https://nodejs.org/dist/v$NodeVersion/$zipName"
        $zipPath = Join-Path $env:TEMP $zipName
        Write-Host "Downloading Node.js $NodeVersion ..."
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
        $extractDir = Join-Path $env:TEMP "node-$NodeVersion-extract"
        if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
        Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
        $nodeSrc = Join-Path $extractDir "node-v$NodeVersion-win-x64"
        Get-ChildItem $nodeSrc | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $nodeDir $_.Name) -Recurse -Force
        }
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Bundled netease API -> $apiDir"
if (Test-Path (Join-Path $nodeDir "node.exe")) {
    Write-Host "Bundled Node.js -> $nodeDir"
}
