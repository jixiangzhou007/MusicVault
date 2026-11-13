# 朋友包 + GitHub 正式发布检查单（1.1）

目标：发给朋友能用；推到 GitHub 能让别人按 README 安装。  
版本号以 `Directory.Build.props` 为准（当前目标：**1.1.0**）。

## A. 发版前（作者本机）

### 代码与测试

- [ ] `dotnet build MusicVault.sln -c Release`
- [ ] `dotnet test MusicVault.sln -c Release`
- [ ]（可选）`.\scripts\smoke-ui-headless.ps1`、`.\scripts\smoke-settings-designsystem.ps1`

### 朋友包（Windows 主路径）

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] `publish\win-x64-vol\MusicVault.exe` 能启动
- [ ] 存在 `api\go-music-api.exe`、`api\version.txt`
- [ ] 包根有 `LICENSE`、`NOTICE`、`THIRD_PARTY_LICENSES.md`
- [ ] 首次向导可选「本地」或「发现」
- [ ] 本地曲：加文件夹 → 出声
- [ ] 发现：搜索 / 试听至少一平台可用
- [ ] 传输页：下载失败有可见原因（尽力路径）
- [ ] 设置 → 导出诊断包可用

完整回归见 [../TESTING.md](../TESTING.md)（朋友包至少做完上列 P0）。

### 文档

- [ ] `README.md` 安装步骤与仓库 URL 正确
- [ ] `CHANGELOG.md` 已写本版条目
- [ ] `docs/LEGAL.md` 仍适用
- [ ] 无密钥 / `.env` / 个人路径写进仓库

### 仓库卫生

- [ ] 未跟踪 `publish/`、`releases/`、`tmp-*`、`.tmp/`
- [ ] 大二进制（ico 除外）不误提交

## B. GitHub 发布

推荐两条资产：

| 资产 | 命令 / 来源 | 用途 |
|------|-------------|------|
| 便携 zip | 压缩 `publish\win-x64-vol` → `MusicVault-1.1.0-win-x64-vol.zip` | 朋友首次安装 |
| Velopack | `velopack-release.ps1` + `publish-github-release.ps1` 或 tag Actions | 安装版在线更新 |

```powershell
# 1) 便携包
Compress-Archive -Path .\publish\win-x64-vol\* -DestinationPath .\MusicVault-1.1.0-win-x64-vol.zip -Force
Get-FileHash .\MusicVault-1.1.0-win-x64-vol.zip -Algorithm SHA256

# 2) tag 触发 CI（需已推送含 1.1.0 的提交）
git tag v1.1.0
git push origin v1.1.0
```

Release 正文用 [GITHUB_RELEASE.md](GITHUB_RELEASE.md) 模板；贴上 SHA256。

## C. 开源叙事（README 已对齐）

对外只强调：

1. 本地听与管  
2. 跨平台搜听 / 歌单汇聚  
3. 下载尽力、非官方 API 可能失效  
4. MIT + 本机数据、无遥测  

不强调：设计系统审计历史、内部「第 N 步」施工日志、未完成平台细节。

## D. 已知不做（本版可发）

- 不保证全平台扫码与稳定下载  
- 不保证 macOS 下载合格  
- 不清理全部历史 `docs/superpowers`（保留给贡献者；访客看 docs/README 即可）  
- 不做大规模死代码删除（另开 PR）；发版以稳定与文档为准
