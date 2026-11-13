# GitHub Release 发布模板

发布新版本时，在 GitHub **Releases → Draft a new release** 中参考下列内容。将 `X.Y.Z` 替换为实际版本号。

## Tag / Title

```
vX.Y.Z
```

标题示例：`MusicVault X.Y.Z`

## 发布说明正文（复制后编辑）

```markdown
## 概要

<!-- 一两句话说明本版重点 -->

## 系统要求

- **Windows 10/11 x64**（推荐；完整播放 + 可选捆绑 API）
- **macOS 12+ Apple Silicon**（本地能播、发现能搜能在线听；下载尽力而为）
- [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0) — 仅在使用非自包含构建时需要
- 磁盘：应用约 150–300 MB（含可选 API）；曲库空间另计

## 安装方式

### Windows

1. 下载 `MusicVault-X.Y.Z-win-x64-vol.zip`
2. 解压到**不含中文或特殊字符**的路径（可选，减少部分杀毒误报）
3. 运行 `MusicVault.exe`
4. 首次启动阅读并同意版权声明，按向导选择「已有歌曲」或「在线听」

**不要只复制单个 exe**，完整包需包含 `api/` 目录（若使用自带搜索服务）。

### macOS Apple Silicon

1. 下载 `MusicVault-X.Y.Z-osx-arm64.zip`，**不要下 Windows 包**。
2. 若提示「无法打开」：访达中右键 → 打开。
3. Intel Mac（2019 及更早）第一版不支持。

Mac 包须含 `libMusicVaultPlayer.dylib`。合格线是启动 + 本地出声 + 在线听；**不保证下载**。

## 内置组件

| 组件 | 说明 |
|------|------|
| MusicVault | 主程序（MIT，见仓库 LICENSE） |
| api/go-music-api.exe | 可选；多平台搜索（上游许可证见 THIRD_PARTY_LICENSES.md） |
| api/netease-cloud-music-api | 可选；需 Node 或脚本已捆绑 |

## 已知限制

- **下载**：尽力而为，不作保证；macOS 以在线播放为准。
- **QQ / 酷狗 / 酷我 / 咪咕**：多为实验性支持，完整体验依赖自建 API 或环境变量。
- **百度百科 / 平台抓取**：仅供个人学习研究；请遵守 [docs/LEGAL.md](docs/LEGAL.md)。

## 杀毒软件

未签名的自发布 exe 可能被 Windows Defender 误报。若信任本发布者，可添加排除项或使用源码自行 `dotnet publish` 构建。

## 校验和（发布者填写）

```
SHA256: <在此粘贴 zip 的 SHA256>
```

生成方式（PowerShell）：

```powershell
Get-FileHash .\MusicVault-X.Y.Z-win-x64-vol.zip -Algorithm SHA256
```

## 变更详情

见 [CHANGELOG.md](https://github.com/jixiangzhou007/MusicVault/blob/main/CHANGELOG.md#xyz---YYYY-MM-DD)

## 法律声明

- 请仅下载与管理您拥有合法权利的音乐内容。
- 本软件与各大音乐平台无隶属关系。
- 使用即表示您已阅读 [docs/LEGAL.md](https://github.com/jixiangzhou007/MusicVault/blob/main/docs/LEGAL.md)。
```

## 发布资产清单

- [ ] Velopack 更新产物（`releases/`：`*.nupkg`、`RELEASES`、Setup 等）— 应用内更新依赖这些
- [ ] `MusicVault-X.Y.Z-win-x64-vol.zip`（`scripts/publish-with-api.ps1` 产出，便携首次安装）
- [ ] `MusicVault-X.Y.Z-osx-arm64.zip`（CI `release-macos` 或本机 `publish-macos.sh osx-arm64`；**必须**含 `libMusicVaultPlayer.dylib`）
- [ ] 可选：`MusicVault-X.Y.Z-win-x64.zip`（轻量、不带 API）
- [ ] 附 `SHA256` 校验值
- [ ] 确认 zip 内包含 `LICENSE`、`NOTICE`、`THIRD_PARTY_LICENSES.md`（建议复制到包根目录）
- [ ] Mac CI job 失败时仍可只发 Windows，不挡 Windows 朋友
- [ ] 推送 `vX.Y.Z` tag 后确认 Release 标题为 `MusicVault X.Y.Z`（不是仅 Windows）

## 在线更新

见 [ONLINE_UPDATE.md](ONLINE_UPDATE.md)。更新源 URL 使用仓库根地址：`https://github.com/jixiangzhou007/MusicVault`。

## 发布前检查

- [ ] `Directory.Build.props` 版本号已 bump
- [ ] `CHANGELOG.md` 已更新
- [ ] `dotnet test MusicVault.sln -c Release` 通过
- [ ] 本地 smoke：启动、扫库、播放、发现搜索、下载一条、艺术家刷新
- [ ] 未将 `publish/`、`releases/` 或 `node_modules` 提交进 git（仅上传 Release 附件）

说明：全仓 `dotnet format --verify-no-changes` 可能因历史 `.cs` 的 LF/CRLF 混用失败，**不作为发版完成条件**。以 `dotnet test -c Release` 为准。

## Mac 实机验收（osx-arm64）

不挡 Windows 朋友包。无 Apple Silicon 真机时标「未验」，不得把 Intel Mac / 无 `dylib` 的包当朋友资产。

| 项 | 状态（2026-08-17） |
|----|-------------------|
| zip 含 `libMusicVaultPlayer.dylib` | 未验 |
| 启动 | 未验 |
| 本地文件出声 | 未验 |
| 发现能搜并能在线听 | 未验 |
| 有统一 API 时下载入口可用 | 未验 |
| 无 API 时禁用，并提示「本机以在线播放为准」 | 未验 |
| 交通灯不挡「首页」Tab（标题栏与导航合一） | 未验 |
| 无右侧假窗控（最小/最大/关闭） | 未验 |
| 最大化后顶栏与交通灯仍可点 | 未验 |
| Intel Mac / 无 dylib 不得当朋友资产 | 政策已写；未发 Intel 包 |
