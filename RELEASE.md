# MusicVault 发布说明

> 法律与合规：[docs/LEGAL.md](docs/LEGAL.md) · GitHub Release 正文模板：[docs/GITHUB_RELEASE.md](docs/GITHUB_RELEASE.md) · 变更日志：[CHANGELOG.md](CHANGELOG.md)

## 版本策略

- 应用版本：`Directory.Build.props` 中的 `Version`
- 程序集名：`MusicVault`（输出 `MusicVault.exe` / `MusicVault`）

## Windows 发布（推荐）

```powershell
cd <MusicVault 仓库根目录>
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

脚本顺序：**先 `dotnet publish`，再编译 `libMusicVaultPlayer.dylib`（AVFoundation 回退），再旁路捆绑 LibVLC**。  
缺省引擎优先 LibVLC：建议本机安装 [VLC](https://www.videolan.org/) 后执行（或设置 `VLC_APP=/path/to/VLC.app`）。未捆绑 LibVLC 时回退 AVFoundation（格式与均衡器能力较弱）。需 Xcode Command Line Tools（`clang`）。

**注意**：macOS 包**不**捆绑 go-music-api；在线搜索/下载请在设置填写外部统一 API。

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
