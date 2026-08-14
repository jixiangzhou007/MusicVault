# GitHub Release 发布模板

发布新版本时，在 GitHub **Releases → Draft a new release** 中参考下列内容。将 `X.Y.Z` 替换为实际版本号。

## Tag / Title

```
vX.Y.Z
```

标题示例：`MusicVault X.Y.Z (Windows x64)`

## 发布说明正文（复制后编辑）

```markdown
## 概要

<!-- 一两句话说明本版重点 -->

## 系统要求

- **Windows 10/11 x64**（推荐；本包为 win-x64 完整版）
- [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0) — 仅在使用非自包含构建时需要
- 磁盘：应用约 150–300 MB（含可选 API）；曲库空间另计

## 安装方式

### A. Velopack 安装版（支持应用内更新）

1. 下载 Release 中的 **Setup.exe**（或安装器产物）
2. 安装后从开始菜单启动 MusicVault
3. 首次启动阅读并同意版权声明
4. 之后可在 **设置 → 更新与诊断** 检查更新（需联网访问本仓库 Release）

### B. 便携 zip（不解压更新）

1. 下载 `MusicVault-X.Y.Z-win-x64-vol.zip`
2. 解压到**不含中文或特殊字符**的路径（可选，减少部分杀毒误报）
3. 运行 `MusicVault.exe`
4. 首次启动阅读并同意版权声明
5. **便携版不能应用内自动更新**，请手动下载新 zip 覆盖；见 [ONLINE_UPDATE.md](ONLINE_UPDATE.md)

**不要只复制单个 exe**，完整包需包含 `api/` 目录（若使用自带 go-music-api / 网易 API）。

## 内置组件

| 组件 | 说明 |
|------|------|
| MusicVault | 主程序（MIT，见仓库 LICENSE） |
| api/go-music-api.exe | 可选；多平台搜索（上游许可证见 THIRD_PARTY_LICENSES.md） |
| api/netease-cloud-music-api | 可选；需 Node 或脚本已捆绑 |

## 已知限制

- **macOS**：可编译运行；本地播放缺省优先 **LibVLC**（发布时建议捆绑自 VLC.app），否则回退 AVFoundation（需 `libMusicVaultPlayer.dylib`）。**无**内置 go-music-api，在线能力需外部统一 API。详见 README / RELEASE.md。
- **QQ / 酷狗 / 酷我 / 咪咕**：多为实验性支持，完整体验依赖自建 API 或环境变量。
- **百度百科 / 平台抓取**：仅供个人学习研究；请遵守 [docs/LEGAL.md](docs/LEGAL.md)。
- **曲库来源**：本地 / NAS(SMB) / WebDAV 已可用；网络库需本机可达对应共享或 URL。

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
- [ ] 可选：`MusicVault-X.Y.Z-win-x64.zip`（轻量、不带 API）
- [ ] 附 `SHA256` 校验值
- [ ] 确认 zip 内包含 `LICENSE`、`NOTICE`、`THIRD_PARTY_LICENSES.md`（建议复制到包根目录）

## 在线更新

见 [ONLINE_UPDATE.md](ONLINE_UPDATE.md)。更新源 URL 使用仓库根地址：`https://github.com/jixiangzhou007/MusicVault`。

## 发布前检查

- [ ] `Directory.Build.props` 版本号已 bump
- [ ] `CHANGELOG.md` 已更新
- [ ] `dotnet test MusicVault.sln -c Release` 通过
- [ ] 本地 smoke：启动、扫库、播放、发现搜索、下载一条、艺术家刷新
- [ ] 未将 `publish/`、`releases/` 或 `node_modules` 提交进 git（仅上传 Release 附件）
