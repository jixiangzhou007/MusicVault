# MusicVault 发布说明

> 法律与合规：[docs/LEGAL.md](docs/LEGAL.md) · GitHub Release 正文模板：[docs/GITHUB_RELEASE.md](docs/GITHUB_RELEASE.md) · 变更日志：[CHANGELOG.md](CHANGELOG.md) · **朋友包检查单**：[docs/FRIEND_RELEASE.md](docs/FRIEND_RELEASE.md)

## 版本策略

- 应用版本：`Directory.Build.props` 中的 `Version`
- 程序集名：`MusicVault`（输出 `MusicVault.exe` / `MusicVault`）

## Windows 朋友包（推荐）

发给朋友或上传便携 Release 附件时用这一条（含统一 API）：

```powershell
cd <MusicVault 仓库根目录>
.\scripts\publish-with-api.ps1 -Restart
# 输出: publish\win-x64-vol\  （整夹打 zip，勿只拷 exe）
```

## Windows 轻量 / Velopack 基线

```powershell
.\scripts\publish-windows.ps1
# 输出: publish\win-x64\MusicVault.exe
```

### 可选 NativeAOT

```powershell
.\scripts\publish-windows.ps1 -Aot
```

> Avalonia + NativeAOT 可能需额外 `rd.xml` 调整；默认使用 **PublishTrimmed 单文件** 兼容性更好。

## macOS 发布

```bash
chmod +x scripts/publish-macos.sh
./scripts/publish-macos.sh osx-arm64
# 或 Intel: ./scripts/publish-macos.sh osx-x64
# AOT: AOT=true ./scripts/publish-macos.sh osx-arm64
```

脚本顺序：**先 `dotnet publish`，再编译 `libMusicVaultPlayer.dylib`**（避免 publish 清空输出目录导致 dylib 丢失）。需安装 Xcode Command Line Tools（`clang`）。

**osx-arm64 朋友包必须含 `libMusicVaultPlayer.dylib`**：缺 dylib 时脚本会失败，不会打出 zip。LibVLC / VLC.app 为增强，不是合格线。成功后 zip 在 `publish/MusicVault-<Version>-osx-arm64.zip`。

Windows 上可跑结构校验（不替代真机构建）：

```powershell
.\scripts\verify-macos-publish.ps1
```

## Velopack 自动更新（GitHub）

完整步骤见 **[docs/ONLINE_UPDATE.md](docs/ONLINE_UPDATE.md)**。

```powershell
.\scripts\publish-windows.ps1 -NoRestart
.\scripts\velopack-release.ps1
.\scripts\publish-github-release.ps1   # 需 gh 已登录
# 或: git tag v1.0.11; git push origin v1.0.11  → Actions 自动发版
```

用户侧更新源填写 GitHub 仓库根 URL（设置可留空，已内置默认）：

`https://github.com/jixiangzhou007/MusicVault`

也可用环境变量 `MUSICVAULT_UPDATE_URL`。

## 环境变量汇总

| 变量 | 用途 |
|------|------|
| `MUSICVAULT_NETEASE_API` | 网易云 API |
| `MUSICVAULT_QQ_API` | QQ 音乐 API |
| `MUSICVAULT_KUGOU_API` | 酷狗 API |
| `MUSICVAULT_KUWO_API` | 酷我 API |
| `MUSICVAULT_MIGU_API` | 咪咕 API |
| `MUSICVAULT_UPDATE_URL` | Velopack 更新源（优先于设置） |

## 数据目录

| 平台 | 路径 |
|------|------|
| Windows | `%LOCALAPPDATA%\MusicVault` |
| macOS | `~/Library/Application Support/MusicVault` |

包含：`musicvault.db`、`Downloads/`、`backups/`、`settings.json`

## 免责声明

本软件仅供个人学习与研究使用。请遵守当地法律法规与音乐平台服务条款，勿用于商业或侵权用途。
