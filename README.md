# MusicVault

**一个播放器，听本地和多个平台。**

面向自己和朋友的跨平台桌面音乐应用：本地曲库 / NAS / WebDAV，加上多平台搜索、在线试听、歌单汇聚与尽力而为的下载。  
基于 Avalonia 11 + .NET 8。开源协议 [MIT](LICENSE)。

> **给朋友：** 下载 Windows 便携包 → 解压 → 运行 `MusicVault.exe`。不要只拷单个 exe（需带上 `api/`）。  
> **macOS：** 合格线是本地能播、发现能搜能听；下载不作保证。详见 [docs/LEGAL.md](docs/LEGAL.md)。

## 解决什么

| 痛点 | 做法 |
|------|------|
| 歌散在各平台 | 统一搜索 / 试听 / 平台登录（Cookie） |
| 本地与 NAS 难管 | 多库扫描、标签、整理、歌词、自建歌单 |
| 歌单割裂 | 导入平台歌单后在同一底栏播放 |
| 下载不稳定 | **尽力而为**，不承诺替代会员；失败会说明 |

产品意图全文：[docs/PRODUCT_DESIGN.md](docs/PRODUCT_DESIGN.md)

## 系统要求

- **Windows 10/11 x64**（推荐；可捆绑统一音乐 API）
- **macOS 12+ Apple Silicon**（本地播放 + 在线听；下载尽力）
- 开发构建需 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 朋友怎么装（Windows）

1. 从 [Releases](https://github.com/jixiangzhou007/MusicVault/releases) 下载 `MusicVault-*-win-x64-vol.zip`
2. 解压到路径尽量不含奇怪字符
3. 运行 `MusicVault.exe`（首次同意声明并选「已有歌曲」或「在线听」）

作者本机打朋友包：

```powershell
.\scripts\publish-with-api.ps1 -Restart
# 将 publish\win-x64-vol 打成 zip 即可分发
```

说明：[docs/DISTRIBUTION.md](docs/DISTRIBUTION.md) · Release 模板：[docs/GITHUB_RELEASE.md](docs/GITHUB_RELEASE.md)

## 功能一览

| 能力 | 状态 |
|------|------|
| 本地曲库、增量扫描、元数据 / 歌词 | 完整 |
| NAS / WebDAV | 完整（Windows 凭据等能力见能力矩阵） |
| 发现：搜索、试听、浏览、平台登录 | 完整（依赖统一 API；Windows 可随包自启） |
| 多平台歌单导入后在线听 | 完整 |
| 下载队列 | 尽力（可断、可失败） |
| 内置播放器 / 歌词 / 均衡器 | Windows 完整；macOS 视引擎 |
| Velopack 应用内更新 | 完整（安装版；便携包需换 zip） |

更细的平台差异：[docs/execution/platform-capability-matrix.md](docs/execution/platform-capability-matrix.md)

## 开发者快速开始

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src\MusicVault.App\MusicVault.App.csproj
```

可选环境变量（一般朋友包不需要手动配）：

```powershell
$env:MUSICVAULT_MUSIC_API = "http://localhost:8080"   # 统一 API
$env:MUSICVAULT_NETEASE_API = "http://localhost:3000" # 网易兼容 API
$env:MUSICVAULT_UPDATE_URL = "https://github.com/jixiangzhou007/MusicVault"
```

扩展统一 API / fork：[docs/GO_MUSIC_API_EXTENSION.md](docs/GO_MUSIC_API_EXTENSION.md)

## 仓库结构

```
src/MusicVault.App|Core|Data|Services
tests/MusicVault.Tests
scripts/          # 发布、冒烟、API 捆绑
docs/             # 产品 / 架构 / 发版（见 docs/README.md）
```

## 文档索引

| 文档 | 用途 |
|------|------|
| [docs/README.md](docs/README.md) | 文档导航 |
| [CHANGELOG.md](CHANGELOG.md) | 版本变更 |
| [RELEASE.md](RELEASE.md) | 构建与发版命令 |
| [TESTING.md](TESTING.md) | 回归清单 |
| [docs/FRIEND_RELEASE.md](docs/FRIEND_RELEASE.md) | **1.1 朋友包 + GitHub 发布检查单** |
| [CONTRIBUTING.md](CONTRIBUTING.md) | 贡献方式 |
| [docs/LEGAL.md](docs/LEGAL.md) | 法律与合规 |

## 法律声明

- 请仅下载、存储、播放你拥有合法权利的音乐。
- 各平台接口为非官方实现，可能随时失效；风险自负。
- 无内置遥测；用户数据默认仅本机。
- 许可证：[LICENSE](LICENSE) · [NOTICE](NOTICE) · [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
