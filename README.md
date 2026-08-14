# MusicVault

**下载即整理，整理即播放** — 跨平台个人数字音乐管理工具。

基于 **Avalonia UI 11** + **.NET 8** + **C# 12**。  
**推荐平台：Windows 10/11 x64**（完整播放 + 可选捆绑 API）。macOS 可编译运行，但播放与 API 捆绑见下方说明。

> ⚠️ **macOS 用户**：当前版本内置播放器通过原生 AVAudioPlayer 实现（需编译 `libMusicVaultPlayer.dylib`）。播放功能需要 Xcode Command Line Tools 编译原生库，详见 [RELEASE.md](RELEASE.md)。

## 功能概览

图例：**完整** = 默认可用 · **实验** = 可用但不保证稳定 · **需配置** = 依赖自建 API 或环境变量

| 优先级 | 功能 | 状态 |
|--------|------|------|
| P0 | 本地曲库 + 增量扫描 | **完整** |
| P0 | 元数据编辑（TagLib、封面、LRC） | **完整** |
| P0 | 歌单 / M3U 导入导出 | **完整** |
| P0 | 文件整理与重命名模板 | **完整** |
| P0 | 网易云搜索与下载 | **完整**（建议自建 [Netease API](docs/DISTRIBUTION.md)） |
| P0 | QQ / 酷狗 / 酷我 / 咪咕 | **实验** / **需配置** |
| P1 | 内置播放器 | **完整**（仅 Windows）· macOS：**可用**（需原生库） |
| P1 | 同步滚动歌词 | **完整**（Windows 播放时） |
| P1 | 下载队列（暂停/继续/重试） | **完整** |
| P1 | 主题（浅/深/跟随系统） | **完整** |
| P1 | Velopack 自动更新 | **完整**（默认 GitHub 源，见 [ONLINE_UPDATE](docs/ONLINE_UPDATE.md)） |
| P2 | 数据库备份（保留 7 份） | **完整** |
| P2 | 艺术家百科简介 / 头像 | **完整**（百度百科 + 缓存，见 [LEGAL](docs/LEGAL.md)） |
| P2 | 重复歌曲检测 | **完整** |

## 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 x64 或 macOS 12+

## 第三步新增功能

| 模块 | 功能 |
|------|------|
| 多音乐库 | 增删改、每库命名模板、文件夹选择器、单库扫描 |
| 歌单 | 重命名/删除、M3U 导入导出、平台歌单解析、批量下载、失败重试 |
| 设置 | 主题/强调色、并发下载、快捷键、备份恢复 |
| 统计 | 曲目/专辑/艺术家/容量/播放统计 |
| 平台 API | QQ/酷狗/酷我/咪咕 搜索（需配置环境变量） |
| 备份 | 每日自动备份 + 完整 ZIP 打包 |
| 动画 | 页面淡入、列表悬停高亮 |

### 平台 API 环境变量

```powershell
$env:MUSICVAULT_NETEASE_API = "http://localhost:3000"
$env:MUSICVAULT_QQ_API = "https://api.qq.jsososo.com"
$env:MUSICVAULT_KUGOU_API = ""   # 留空使用酷狗公开搜索
$env:MUSICVAULT_KUWO_API = ""
$env:MUSICVAULT_MIGU_API = ""
```

## 第二步新增服务（Core/Services）

| 模块 | 类 | 说明 |
|------|-----|------|
| 元数据 | `BatchMetadataService` | 批量编辑、查找替换、批量刮削 |
| 元数据 | `LrcParser` / `LyricsReader` | LRC 解析、内嵌封面读取 |
| 扫描 | `LibraryScanService` | 增量扫描、删除失效索引、后台扫描 |
| 整理 | `BatchOrganizeService` | 批量重命名预览与执行 |
| 下载 | `DownloadQueueService` | 实时进度、去重、完成后入库 |
| 数据 | `TrackRepository` | 专辑/艺术家分组、播放状态 |

## 快速开始

```powershell
# 克隆后进入项目目录
cd MusicVault

# 还原依赖并编译
dotnet restore
dotnet build

# 运行（开发模式）
dotnet run --project src\MusicVault.App\MusicVault.App.csproj
```

### 网易云 API 配置

网易音乐搜索/下载依赖 NeteaseCloudMusicApi 兼容服务。可设置环境变量指向自建实例：

```powershell
$env:MUSICVAULT_NETEASE_API = "http://localhost:3000"
```

默认使用公共演示端点（稳定性不保证，**生产环境请自建 API**）。

## 发布单文件 EXE / APP

详见 [RELEASE.md](RELEASE.md) 与 [TESTING.md](TESTING.md)。

```powershell
# Windows（推荐：Trimmed 单文件）
.\scripts\publish-windows.ps1
# 输出: publish\win-x64\MusicVault.exe

# 可选 NativeAOT
.\scripts\publish-windows.ps1 -Aot

# Velopack 安装包 + GitHub 在线更新（见 docs/ONLINE_UPDATE.md）
.\scripts\velopack-release.ps1
.\scripts\publish-github-release.ps1
```

```bash
chmod +x scripts/publish-macos.sh
./scripts/publish-macos.sh osx-arm64
```

更新源默认指向 `https://github.com/jixiangzhou007/MusicVault`（可在设置页覆盖，或设 `MUSICVAULT_UPDATE_URL`）。首次推送仓库：`.\scripts\github-bootstrap.ps1`。

## 项目结构

```
MusicVault/
├── src/
│   ├── MusicVault.App/       # Avalonia UI、ViewModel、主题
│   ├── MusicVault.Core/      # 模型、枚举、接口
│   ├── MusicVault.Data/      # SQLite 数据库与仓储
│   └── MusicVault.Services/  # 业务逻辑、平台提供者、播放器
├── scripts/                  # 发布与 Velopack 脚本
├── tests/MusicVault.Tests/   # 单元测试
├── .github/workflows/        # CI
├── RELEASE.md
├── TESTING.md
└── README.md
```

## 数据目录

用户数据默认存储在：

- **Windows**: `%LOCALAPPDATA%\MusicVault\`
- **macOS**: `~/Library/Application Support/MusicVault/`（可通过代码调整）

包含：`musicvault.db`、下载目录、`backups/`。

## 扩展其他音乐平台

实现 `IMusicSourceProvider` 接口并在 `AppServices` 中注册：

```csharp
public sealed class MyPlatformProvider : IMusicSourceProvider { ... }
```

## 法律声明与合规

- 请仅下载、存储、播放您拥有**合法权利**的音乐内容。
- 各音乐平台接口均为**非官方**实现；使用风险由用户自行承担。
- 百度百科等在线资料抓取仅供**个人学习研究**，详见 [docs/LEGAL.md](docs/LEGAL.md)。
- 本软件**不包含**内置遥测；用户数据默认保存在本机。

## 许可证与第三方组件

- 源代码：[MIT License](LICENSE)
- 归属说明：[NOTICE](NOTICE)
- 依赖摘要：[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
- 版本历史：[CHANGELOG.md](CHANGELOG.md)
- 发布检查清单：[docs/GITHUB_RELEASE.md](docs/GITHUB_RELEASE.md)
- 仓库管理：[docs/REPOSITORY.md](docs/REPOSITORY.md)
