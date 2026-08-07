# 第三方组件与许可证

MusicVault 源代码以 [MIT License](LICENSE) 发布。下列组件随应用**编译引用**或**可选捆绑分发**；具体版本以各 `.csproj` 与 `scripts/publish-with-api.ps1` 为准。

## .NET / NuGet（编译进 MusicVault）

| 组件 | 用途 | 许可证（常见） |
|------|------|----------------|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) 11.2.x | 跨平台 UI | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.3.x | MVVM 工具 | MIT |
| [Velopack](https://github.com/velopack/velopack) | 应用更新 | 见上游仓库 |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) 8.0.x | SQLite | MIT（运行时） |
| [TagLibSharp](https://github.com/mono/taglib-sharp) 2.3.x | 音频标签读写 | LGPL-2.1 / MPL（见上游） |
| [NAudio](https://github.com/naudio/NAudio) 2.2.x | Windows 播放（仅 Windows 构建） | MIT |
| [NAudio.Vorbis](https://github.com/naudio/Vorbis) | Vorbis 解码 | MIT |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) 3.9.x | 可选播放引擎（Windows opt-in；macOS 缺省优先） | LGPL-2.1+ |
| [VideoLAN.LibVLC](https://www.videolan.org/vlc/) 3.0.x | LibVLC 原生运行时（动态链接，随发布旁路拷贝到 libvlc/） | LGPL-2.1+ |
| [PinYin.NET](https://www.nuget.org/packages/PinYin.NET) | 拼音检索 | 见 NuGet 页 |

完整依赖树可在发布前执行：

```powershell
dotnet list MusicVault.sln package --include-transitive
```

## 可选捆绑运行时（`publish/win-x64-vol/api/`，不由本仓库授权音乐内容）

| 组件 | 用途 | 说明 |
|------|------|------|
| **go-music-api** | 多平台搜索/解析 | 由 `scripts/publish-with-api.ps1` 从本机构建复制；许可证见 [guohuiyuan/music-lib](https://github.com/guohuiyuan/music-lib) 或你使用的 fork |
| **netease-cloud-music-api-alger** | 网易云兼容 API | npm 包，随 `bundle-netease-cloud-music-api.ps1` 安装；许可证见包内 `LICENSE` |
| **Node.js** | 运行网易 API | 若脚本捆绑了 `api/node`，遵循 [Node.js 许可证](https://github.com/nodejs/node/blob/main/LICENSE) |

分发 zip 时，应在压缩包内附带本文件及上游 `licenses/` 目录（`node_modules` 内各包 LICENSE），或提供获取方式链接。

## 外部服务（用户自行配置或运行时访问）

| 服务 | 用途 | 合规提示 |
|------|------|----------|
| 各音乐平台非官方 API | 搜索、元数据、下载 | 非官方接口；用户须遵守平台服务条款与当地法律 |
| 百度百科 (baike.baidu.com) | 艺术家/专辑简介、头像 | HTML 抓取；见 [docs/LEGAL.md](docs/LEGAL.md) |
| 用户配置的远程 API URL | 可选自建后端 | 由用户负责服务可用性与合规 |

## 致谢

界面与交互思路参考了 Dopamine、Music Tool、Music Tag 等优秀工具（见设置页「软件介绍」）。与这些项目无隶属关系。

---

**注意**：本表为摘要，不构成法律意见。发布二进制包前请核对各组件当前版本的上游许可证全文。
