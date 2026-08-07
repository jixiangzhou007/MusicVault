# 启动 go-music-api（方案 A），供 MusicVault 发现页搜索/下载使用
$root = Split-Path $PSScriptRoot -Parent
$apiDir = Join-Path (Split-Path $root -Parent) "go-music-api"
$exe = Join-Path $apiDir "go-music-api.exe"

if (-not (Test-Path $exe)) {
    Write-Host "未找到 $exe"
    Write-Host "请先解压/构建 go-music-api，或运行："
    Write-Host "  cd $apiDir; go build -o go-music-api.exe ."
    exit 1
}

$existing = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "端口 8080 已在监听，go-music-api 可能已在运行。"
    exit 0
}

Start-Process -FilePath $exe -WorkingDirectory $apiDir -WindowStyle Minimized
Start-Sleep -Seconds 2
Write-Host "go-music-api 已启动: http://localhost:8080"
Write-Host "在 MusicVault「偏好」中确认「统一音乐 API」为 http://localhost:8080"
