namespace MusicVault.App.Localization;

/// <summary>设置页「软件介绍」与开源致谢文案（zh-CN / en-US）。</summary>
internal static class GuideLocalizationResources
{
    public static readonly IReadOnlyDictionary<string, string> ZhCn = new Dictionary<string, string>
    {
        ["Settings.Guide.Tagline"] = "下载即整理，整理即播放",

        ["Settings.Guide.Author"] = "作者：野马1/2",

        ["Settings.Guide.HeroLead"] =
            "　　MusicVault 由野马1/2 为方便个人收藏与整理数字音乐而开发，主要使用 Cursor 辅助完成设计与实现。界面与流程借鉴了 Dopamine、Music Tool、Music Tag 等优秀工具的思路。在个人曲库中完成搜索、下载、整理与播放一站衔接。",

        ["Settings.Guide.HeroDisclaimer"] =
            "仅供个人学习研究使用，请遵守当地版权法律法规；请仅下载与使用您拥有合法权利的音乐内容。",

        ["Settings.Guide.Copyright"] =
            "© 2026 野马1/2 · MusicVault. 保留所有权利。本项目源代码采用 MIT 许可证发布。",

        ["Settings.Guide.OpenSourceTitle"] = "感谢开源项目",

        ["Settings.Guide.Feature.Library.Title"] = "曲库浏览",

        ["Settings.Guide.Feature.Library.Desc"] =
            """
            • 歌曲页：浏览全部曲目，支持即时搜索筛选、多列排序与右键菜单（播放、编辑元数据、加入歌单、整理队列）。
            • 艺术家 / 专辑 / 歌单：按分组浏览本地收藏，查看封面、曲目列表与播放统计。
            • 库来源管理：在设置「数据」或概览卡片添加本地文件夹、NAS / WebDAV 远程库，增量扫描入库并统计容量。
            • 平台歌单：粘贴网易云等平台歌单链接导入，解析曲目后可在曲库中统一管理或批量下载。
            """,

        ["Settings.Guide.Feature.Discover.Title"] = "在线发现",

        ["Settings.Guide.Feature.Discover.Desc"] =
            """
            • 多平台搜索：网易云、QQ 音乐、酷狗、酷我、咪咕等，统一搜索框输入歌名或歌手。
            • 链接解析：粘贴单曲 / 专辑 / 歌单链接，自动识别平台并展示曲目。
            • 听歌识曲：麦克风录音或选择本地音频，识别后填入搜索框。
            • 在线试听：在结果列表中预览音频（依赖统一 API 与网络）。
            • API 健康：顶栏指示点显示统一 API 与扩展网易云 API 连通状态，异常时可一键前往设置。
            """,

        ["Settings.Guide.Feature.Download.Title"] = "下载入库",

        ["Settings.Guide.Feature.Download.Desc"] =
            """
            • 下载队列：查看进行中 / 已完成 / 失败任务，支持暂停、继续、重试与清空。
            • 音质策略：按平台与格式选择 FLAC / MP3 等，失败时自动切换音源重试。
            • 自动入库：下载完成后写入指定音乐库，触发增量扫描更新索引。
            • NAS 复制：从远程 NAS 库拉取文件到本地，或反向缓存播放。
            • 去重与命名：按库模板整理文件名，避免重复下载同一曲目。
            """,

        ["Settings.Guide.Feature.Playback.Title"] = "播放体验",

        ["Settings.Guide.Feature.Playback.Desc"] =
            """
            • 本地播放：Windows 默认 NAudio；可选手动开启 LibVLC。macOS 缺省优先 LibVLC（可回退 AVFoundation）。
            • 歌词体验：内嵌或 sidecar LRC 同步滚动，可开启桌面歌词浮窗（Ctrl+L）。
            • 播放队列：编辑当前队列顺序，查看播放历史与「常听 / 推荐」列表。
            • 音效调节：均衡器预设、默认音量 / 循环 / 随机策略可在设置中配置（LibVLC / Windows NAudio）。
            • 快捷键：空格播放暂停、方向键切歌等，支持自定义与导出。
            • 精简模式：双击标题区进入精简播放窗口，适合专注听歌。
            """,

        ["Settings.Guide.Feature.Metadata.Title"] = "元数据整理",

        ["Settings.Guide.Feature.Metadata.Desc"] =
            """
            • 标签编辑：读写 ID3 / Vorbis 等标签，支持标题、艺术家、专辑、封面与歌词。
            • 批量整理：按模板预览并执行文件重命名、目录迁移与缺失字段补全。
            • 在线刮削：从网易云等平台匹配元数据与封面（需配置 API）。
            • 重复检测：按哈希或标签相似度扫描重复曲目，辅助清理冗余文件。
            • 歌词管理：读取 / 编辑 / 嵌入 LRC，支持编码修复与 sidecar 导出。
            """,

        ["Settings.Guide.Feature.Settings.Title"] = "备份与设置",

        ["Settings.Guide.Feature.Settings.Desc"] =
            """
            • 外观与语言：浅色 / 深色 / 跟随系统主题，界面字号、强调色与 zh-CN / en-US 切换。
            • 在线源配置：统一 API 地址、扩展网易云 API、平台登录与连接健康探测。
            • 数据备份：自动保留最近 10 份 SQLite 数据库备份，支持手动导出 ZIP 恢复。
            • 应用更新：Velopack 增量更新，可在「更新与诊断」查看 changelog 与内置 API 版本。
            • 合规说明：关于页展示开源致谢、MIT 许可与版权提示。
            """,

        ["Settings.OpenSource.Avalonia.Name"] = "Avalonia UI 11",

        ["Settings.OpenSource.Avalonia.Summary"] = "跨平台桌面界面框架",

        ["Settings.OpenSource.Avalonia.Detail"] =
            """
            Avalonia 是面向 .NET 的跨平台 UI 框架，MusicVault 的全部窗口、页面、主题与控件样式均基于 Avalonia 11 构建。

            • 提供 XAML 声明式界面、数据绑定与样式资源系统，支持浅色 / 深色主题切换。
            • 在 Windows 与 macOS 上共用同一套 UI 代码，降低跨平台维护成本。
            • 本项目使用 Avalonia 的 Dock、ScrollViewer、ComboBox 等控件实现曲库、发现、设置等核心页面。

            许可证：MIT · https://github.com/AvaloniaUI/Avalonia
            """,

        ["Settings.OpenSource.DotNet.Name"] = ".NET 8",

        ["Settings.OpenSource.DotNet.Summary"] = "应用运行时与基础类库",

        ["Settings.OpenSource.DotNet.Detail"] =
            """
            MusicVault 基于 .NET 8 与 C# 12 开发，依赖现代运行时提供的性能、异步 I/O 与跨平台能力。

            • 作为应用宿主，负责启动 Avalonia 桌面进程、依赖注入与服务生命周期管理。
            • 提供 System.IO、System.Net.Http、System.Text.Json 等基础库，支撑文件扫描、HTTP 请求与配置序列化。
            • Windows 发布采用 trimmed 单文件；macOS 可编译运行，部分原生能力需额外 dylib。

            许可证：MIT · https://dotnet.microsoft.com/
            """,

        ["Settings.OpenSource.Mvvm.Name"] = "CommunityToolkit.Mvvm",

        ["Settings.OpenSource.Mvvm.Summary"] = "ViewModel 与命令绑定工具",

        ["Settings.OpenSource.Mvvm.Detail"] =
            """
            CommunityToolkit.Mvvm 为 MusicVault 的 ViewModel 层提供 ObservableObject、RelayCommand 等 MVVM 基础设施。

            • ObservableProperty 源生成器减少手写 INotifyPropertyChanged 样板代码。
            • RelayCommand / AsyncRelayCommand 将 UI 按钮与异步业务操作（扫描、下载、备份）解耦。
            • 与 Avalonia 数据绑定配合，实现设置页、播放栏、下载队列等双向状态同步。

            许可证：MIT · https://github.com/CommunityToolkit/dotnet
            """,

        ["Settings.OpenSource.Velopack.Name"] = "Velopack",

        ["Settings.OpenSource.Velopack.Summary"] = "安装包发布与自动更新",

        ["Settings.OpenSource.Velopack.Detail"] =
            """
            Velopack 负责 MusicVault 的安装包生成、增量更新与版本切换。

            • 发布脚本 `scripts/velopack-release.ps1` 打包 Windows 安装程序与更新源。
            • 应用启动时检查更新源（设置页或环境变量 MUSICVAULT_UPDATE_URL），支持后台下载与重启应用。
            • 相比传统安装器，Velopack 提供更快的增量更新体验。

            许可证：见上游仓库 · https://github.com/velopack/velopack
            """,

        ["Settings.OpenSource.Sqlite.Name"] = "Microsoft.Data.Sqlite",

        ["Settings.OpenSource.Sqlite.Summary"] = "曲库元数据 SQLite 存储",

        ["Settings.OpenSource.Sqlite.Detail"] =
            """
            Microsoft.Data.Sqlite 是 MusicVault 本地数据库的 ADO.NET 提供程序，曲库索引与业务数据均存储在 SQLite 文件中。

            • 持久化曲目、专辑、艺术家、歌单、播放历史、下载记录等结构化元数据。
            • 支持增量扫描写入、事务批量更新与数据库备份 / 恢复。
            • 默认路径：Windows `%LOCALAPPDATA%\MusicVault\musicvault.db`。

            许可证：MIT · https://www.nuget.org/packages/Microsoft.Data.Sqlite
            """,

        ["Settings.OpenSource.TagLib.Name"] = "TagLibSharp",

        ["Settings.OpenSource.TagLib.Summary"] = "音频标签读写",

        ["Settings.OpenSource.TagLib.Detail"] =
            """
            TagLibSharp 用于读取与写入音频文件内嵌标签（ID3、Vorbis Comment 等），是元数据整理功能的核心依赖。

            • 扫描时提取标题、艺术家、专辑、时长、封面与歌词等字段写入 SQLite 索引。
            • 元数据编辑器保存时将修改写回文件，支持批量整理与封面 / 歌词嵌入。
            • 兼容 MP3、FLAC、OGG 等常见格式（具体取决于文件编码与标签版本）。

            许可证：LGPL-2.1 / MPL（见上游） · https://github.com/mono/taglib-sharp
            """,

        ["Settings.OpenSource.NAudio.Name"] = "NAudio / NAudio.Vorbis",

        ["Settings.OpenSource.NAudio.Summary"] = "Windows 播放与 Vorbis 解码",

        ["Settings.OpenSource.NAudio.Detail"] =
            """
            NAudio 提供 Windows 平台的本地音频播放管线；NAudio.Vorbis 扩展 Ogg Vorbis 解码支持。

            • 驱动内置播放器：播放 / 暂停、进度、音量与均衡器输出链路（仅 Windows 完整构建）。
            • 与 WASAPI 输出配合，支持常见 PCM 与压缩格式（视编解码器而定）。
            • macOS 使用 AVFoundation 原生库替代，NAudio 不参与 macOS 播放。

            许可证：MIT · https://github.com/naudio/NAudio
            """,

        ["Settings.OpenSource.LibVlc.Name"] = "LibVLC / LibVLCSharp",

        ["Settings.OpenSource.LibVlc.Summary"] = "可选跨格式播放引擎（LGPL）",

        ["Settings.OpenSource.LibVlc.Detail"] =
            """
            LibVLCSharp 是 VideoLAN LibVLC 的 .NET 绑定；启用「可选 LibVLC 引擎」时用于本地音频解码与播放。

            • 覆盖更多容器/编解码器（如 APE、WMA、Opus 等），均衡器走 LibVLC 内置 EQ。
            • Windows 默认关闭（可选手动开启）；macOS 缺省优先 LibVLC。需重启后生效；初始化失败时回退 NAudio / AVFoundation。
            • 原生运行时随发布旁路拷贝到 libvlc/ 目录（音频向插件精简）。

            许可证：LGPL-2.1+ · 动态链接 · https://github.com/videolan/libvlcsharp · https://www.videolan.org/
            """,

        ["Settings.OpenSource.PinYin.Name"] = "PinYin.NET",

        ["Settings.OpenSource.PinYin.Summary"] = "中文艺术家名拼音排序",

        ["Settings.OpenSource.PinYin.Detail"] =
            """
            PinYin.NET 将中文艺术家、专辑等名称转换为拼音，用于曲库列表的拼音排序与检索。

            • 艺术家页按拼音首字母分组浏览，便于快速定位中文歌手。
            • 搜索与筛选时可匹配拼音缩写，提升中文曲库的可用性。
            • 纯本地计算，不依赖网络。

            许可证：见 NuGet 页 · https://www.nuget.org/packages/PinYin.NET
            """,

        ["Settings.OpenSource.GoMusicApi.Name"] = "go-music-api",

        ["Settings.OpenSource.GoMusicApi.Summary"] = "内置多平台统一 API",

        ["Settings.OpenSource.GoMusicApi.Detail"] =
            """
            go-music-api（music-lib）是可选捆绑的多平台音乐搜索与解析后端，为「发现」与「下载」提供统一 HTTP 接口。

            • Windows 完整发布包可通过 `publish-with-api.ps1` 内置 `api/go-music-api.exe`，设置页支持自动启动。
            • 聚合网易云、QQ 音乐、酷狗、酷我、咪咕等平台的搜索、链接解析与下载地址获取。
            • 端口默认 8080，冲突时可回退至 18080；macOS 通常需用户自行部署外部实例。

            许可证：见上游 fork · https://github.com/guohuiyuan/music-lib
            """,

        ["Settings.OpenSource.NetEaseApi.Name"] = "NeteaseCloudMusicApi",

        ["Settings.OpenSource.NetEaseApi.Summary"] = "网易云接口服务",

        ["Settings.OpenSource.NetEaseApi.Detail"] =
            """
            NeteaseCloudMusicApi（netease-cloud-music-api-alger）提供网易云音乐兼容 API，用于榜单、扩展元数据与部分封面 / 歌词匹配。

            • 与 go-music-api 分工：统一 API 负责发现 / 下载主链路，扩展网易云 API 负责榜单排行（`:3000`）等能力。
            • 发布脚本可捆绑 Node.js 运行时与 npm 包至 `api/netease-cloud-music-api/`。
            • 非官方接口，稳定性与可用性取决于部署环境；生产环境建议自建服务。

            许可证：见 npm 包 LICENSE · https://www.npmjs.com/package/netease-cloud-music-api-alger
            """,

        ["Settings.Recognition.Guide.Title"] = "识曲 API 配置指南",

        ["Settings.Recognition.Guide.Body"] =
            """
            听歌识曲配置指南

            默认（推荐）：识曲 API = netease://bundled
            使用内置网易云 API（http://localhost:3000），录音 5–20 秒后点「识别」。

            请同时确认设置中已启用「内置网易云 API」并勾选自动启动。

            可选外部：识曲 API = https://api.audd.io/?api_token=你的 token
            （上传原始音频，需第三方 token）

            详细说明见项目 docs/product/music-recognition-api-guide.md
            """,
    };

    public static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>
    {
        ["Settings.Guide.Tagline"] = "Download, organize, play",

        ["Settings.Guide.Author"] = "Author: 野马1/2",

        ["Settings.Guide.HeroLead"] =
            "    MusicVault was built by 野马1/2 to collect and organize a personal digital music library, with design and implementation assisted primarily by Cursor. Its UI and workflows draw inspiration from excellent tools such as Dopamine, Music Tool, and Music Tag. Search, download, organize, and play come together in one local library workflow.",

        ["Settings.Guide.HeroDisclaimer"] =
            "For personal learning and research only. Please comply with local copyright laws and download or use only music you have the legal right to access.",

        ["Settings.Guide.Copyright"] =
            "© 2026 野马1/2 · MusicVault. All rights reserved. Source code is released under the MIT License.",

        ["Settings.Guide.OpenSourceTitle"] = "Open source acknowledgments",

        ["Settings.Guide.Feature.Library.Title"] = "Library browsing",

        ["Settings.Guide.Feature.Library.Desc"] =
            """
            • Tracks: browse your full catalog with instant search, column sorting, and context menus (play, edit metadata, add to playlist, enqueue for organize).
            • Artists / albums / playlists: grouped views with artwork, track lists, and play statistics.
            • Library sources: add local folders or NAS / WebDAV remotes from Settings → Data or the overview card; incremental scans update the index and storage stats.
            • Platform playlists: paste NetEase or other playlist links to import, then manage or batch-download tracks from the library.
            """,

        ["Settings.Guide.Feature.Discover.Title"] = "Online discovery",

        ["Settings.Guide.Feature.Discover.Desc"] =
            """
            • Multi-platform search: NetEase, QQ Music, Kugou, Kuwo, Migu, and more from one search box.
            • Link parsing: paste track, album, or playlist URLs; the app detects the platform and lists tracks.
            • Music recognition: record from the microphone or pick a local audio file, then fill the search box with matches.
            • Online preview: audition tracks in results (requires the unified API and network access).
            • API health: the top-bar indicator shows unified and extended NetEase API status; jump to Settings when unhealthy.
            """,

        ["Settings.Guide.Feature.Download.Title"] = "Download & import",

        ["Settings.Guide.Feature.Download.Desc"] =
            """
            • Download queue: active, completed, and failed tasks with pause, resume, retry, and clear.
            • Quality policy: choose FLAC / MP3 per platform; automatic source fallback on failure.
            • Auto import: finished files land in the chosen library and trigger an incremental scan.
            • NAS copy: pull files from remote NAS libraries or cache them locally for playback.
            • Dedup & naming: library templates organize filenames and reduce duplicate downloads.
            """,

        ["Settings.Guide.Feature.Playback.Title"] = "Playback experience",

        ["Settings.Guide.Feature.Playback.Desc"] =
            """
            • Local playback: NAudio by default on Windows; optional LibVLC. macOS prefers LibVLC by default (AVFoundation fallback).
            • Lyrics: embedded or sidecar LRC with synced scrolling; optional desktop lyrics overlay (Ctrl+L).
            • Queue & history: edit the current queue; review history and Frequent / Recommended lists.
            • Sound: equalizer presets plus default volume, repeat, and shuffle in Settings.
            • Shortcuts: space to play/pause, arrow keys to skip, customizable and exportable.
            • Compact mode: double-click the title area for a minimal player window.
            """,

        ["Settings.Guide.Feature.Metadata.Title"] = "Metadata & organize",

        ["Settings.Guide.Feature.Metadata.Desc"] =
            """
            • Tag editing: read/write ID3, Vorbis, and related tags—title, artist, album, artwork, lyrics.
            • Batch organize: preview and apply rename templates, path moves, and missing-field fixes.
            • Online enrichment: match metadata and covers from NetEase and other APIs (requires configuration).
            • Duplicate detection: hash- or tag-based scans to help remove redundant files.
            • Lyrics workflow: read, edit, embed, or export LRC; encoding repair for garbled text.
            """,

        ["Settings.Guide.Feature.Settings.Title"] = "Backup & settings",

        ["Settings.Guide.Feature.Settings.Desc"] =
            """
            • Appearance & language: light / dark / system theme, font scale, accent color, zh-CN / en-US UI.
            • Online sources: unified API URL, extended NetEase API, platform login, and connection probes.
            • Data backup: keep the last 10 SQLite backups automatically; manual ZIP export and restore.
            • App updates: Velopack delta updates; changelog and bundled API versions under Updates & Diagnostics.
            • Compliance: About page with open-source credits, MIT license, and copyright notice.
            """,

        ["Settings.OpenSource.Avalonia.Name"] = "Avalonia UI 11",

        ["Settings.OpenSource.Avalonia.Summary"] = "Cross-platform desktop UI framework",

        ["Settings.OpenSource.Avalonia.Detail"] =
            """
            Avalonia is a cross-platform UI framework for .NET. All MusicVault windows, pages, themes, and control styles are built on Avalonia 11.

            • Declarative XAML, data binding, and resource dictionaries with light / dark theme support.
            • One UI codebase for Windows and macOS, reducing cross-platform maintenance.
            • Library, Discover, Settings, and other core screens use Avalonia Dock, ScrollViewer, ComboBox, and related controls.

            License: MIT · https://github.com/AvaloniaUI/Avalonia
            """,

        ["Settings.OpenSource.DotNet.Name"] = ".NET 8",

        ["Settings.OpenSource.DotNet.Summary"] = "Application runtime and base class libraries",

        ["Settings.OpenSource.DotNet.Detail"] =
            """
            MusicVault targets .NET 8 and C# 12, relying on the modern runtime for performance, async I/O, and cross-platform hosting.

            • Hosts the Avalonia desktop process, dependency injection, and service lifetimes.
            • System.IO, System.Net.Http, System.Text.Json, and related APIs power scanning, HTTP, and configuration.
            • Windows releases use a trimmed single-file layout; macOS builds may require additional native libraries for playback.

            License: MIT · https://dotnet.microsoft.com/
            """,

        ["Settings.OpenSource.Mvvm.Name"] = "CommunityToolkit.Mvvm",

        ["Settings.OpenSource.Mvvm.Summary"] = "ViewModel and command binding toolkit",

        ["Settings.OpenSource.Mvvm.Detail"] =
            """
            CommunityToolkit.Mvvm supplies ObservableObject, RelayCommand, and related MVVM primitives for MusicVault view models.

            • ObservableProperty source generators reduce INotifyPropertyChanged boilerplate.
            • RelayCommand / AsyncRelayCommand wire UI actions to async work such as scans, downloads, and backups.
            • Works with Avalonia bindings for Settings, the player bar, download queue, and other live UI state.

            License: MIT · https://github.com/CommunityToolkit/dotnet
            """,

        ["Settings.OpenSource.Velopack.Name"] = "Velopack",

        ["Settings.OpenSource.Velopack.Summary"] = "Installer packaging and auto-update",

        ["Settings.OpenSource.Velopack.Detail"] =
            """
            Velopack builds installers, serves delta updates, and manages version switches for MusicVault.

            • `scripts/velopack-release.ps1` produces Windows installers and update feeds.
            • On launch, the app checks the update URL (Settings or MUSICVAULT_UPDATE_URL), downloads in the background, and restarts when ready.
            • Delivers faster incremental updates compared with traditional full installers.

            License: see upstream repository · https://github.com/velopack/velopack
            """,

        ["Settings.OpenSource.Sqlite.Name"] = "Microsoft.Data.Sqlite",

        ["Settings.OpenSource.Sqlite.Summary"] = "SQLite storage for library metadata",

        ["Settings.OpenSource.Sqlite.Detail"] =
            """
            Microsoft.Data.Sqlite is the ADO.NET provider for MusicVault's local database; all indexed library data lives in a SQLite file.

            • Persists tracks, albums, artists, playlists, play history, download records, and related structures.
            • Supports incremental scan writes, transactional batch updates, and backup / restore workflows.
            • Default path on Windows: `%LOCALAPPDATA%\MusicVault\musicvault.db`.

            License: MIT · https://www.nuget.org/packages/Microsoft.Data.Sqlite
            """,

        ["Settings.OpenSource.TagLib.Name"] = "TagLibSharp",

        ["Settings.OpenSource.TagLib.Summary"] = "Audio tag read/write",

        ["Settings.OpenSource.TagLib.Detail"] =
            """
            TagLibSharp reads and writes embedded audio tags (ID3, Vorbis Comment, etc.) and underpins metadata editing.

            • Scanning extracts title, artist, album, duration, artwork, and lyrics into the SQLite index.
            • The metadata editor writes changes back to files, including batch organize and embedded cover / lyrics.
            • Common formats such as MP3, FLAC, and OGG are supported depending on encoding and tag versions.

            License: LGPL-2.1 / MPL (see upstream) · https://github.com/mono/taglib-sharp
            """,

        ["Settings.OpenSource.NAudio.Name"] = "NAudio / NAudio.Vorbis",

        ["Settings.OpenSource.NAudio.Summary"] = "Windows playback and Vorbis decoding",

        ["Settings.OpenSource.NAudio.Detail"] =
            """
            NAudio drives local playback on Windows; NAudio.Vorbis adds Ogg Vorbis decoding.

            • Powers the built-in player: play/pause, seek, volume, and equalizer output (full Windows build).
            • Works with WASAPI output for common PCM and compressed formats where codecs are available.
            • macOS uses AVFoundation instead; NAudio is not part of the macOS playback path.

            License: MIT · https://github.com/naudio/NAudio
            """,

        ["Settings.OpenSource.LibVlc.Name"] = "LibVLC / LibVLCSharp",

        ["Settings.OpenSource.LibVlc.Summary"] = "Optional cross-format playback engine (LGPL)",

        ["Settings.OpenSource.LibVlc.Detail"] =
            """
            LibVLCSharp is the .NET binding for VideoLAN’s LibVLC. When Optional LibVLC engine is enabled, it decodes and plays local audio.

            • Broader container/codec coverage (e.g. APE, WMA, Opus); equalizer uses LibVLC’s built-in EQ.
            • Off by default on Windows (opt-in); macOS prefers LibVLC by default. Restart required; falls back to NAudio / AVFoundation if init fails.
            • Native runtime is copied beside the publish output under libvlc/ (audio-oriented plugin set).

            License: LGPL-2.1+ · dynamically linked · https://github.com/videolan/libvlcsharp · https://www.videolan.org/
            """,

        ["Settings.OpenSource.PinYin.Name"] = "PinYin.NET",

        ["Settings.OpenSource.PinYin.Summary"] = "Pinyin sorting for Chinese artist names",

        ["Settings.OpenSource.PinYin.Detail"] =
            """
            PinYin.NET converts Chinese artist and album names to pinyin for sorting and search in the library.

            • Artist views can group by pinyin initial for faster navigation.
            • Search and filters can match pinyin abbreviations alongside Chinese characters.
            • Computed locally with no network dependency.

            License: see NuGet package page · https://www.nuget.org/packages/PinYin.NET
            """,

        ["Settings.OpenSource.GoMusicApi.Name"] = "go-music-api",

        ["Settings.OpenSource.GoMusicApi.Summary"] = "Bundled multi-platform unified API",

        ["Settings.OpenSource.GoMusicApi.Detail"] =
            """
            go-music-api (music-lib) is an optional bundled backend that exposes a unified HTTP API for Discover and Downloads.

            • Full Windows packages can ship `api/go-music-api.exe` via `publish-with-api.ps1`; Settings can auto-start it.
            • Aggregates search, link parsing, and download URLs across NetEase, QQ Music, Kugou, Kuwo, Migu, and related sources.
            • Default port 8080 with fallback to 18080 on conflict; macOS users typically deploy an external instance.

            License: see upstream fork · https://github.com/guohuiyuan/music-lib
            """,

        ["Settings.OpenSource.NetEaseApi.Name"] = "NeteaseCloudMusicApi",

        ["Settings.OpenSource.NetEaseApi.Summary"] = "NetEase Cloud Music API service",

        ["Settings.OpenSource.NetEaseApi.Detail"] =
            """
            NeteaseCloudMusicApi (netease-cloud-music-api-alger) provides a NetEase-compatible API for charts, extended metadata, and some cover / lyrics matching.

            • Complements go-music-api: unified API for primary discover/download flows; extended NetEase API for charts (`:3000`) and related features.
            • Release scripts can bundle Node.js and the npm package under `api/netease-cloud-music-api/`.
            • Unofficial interface—availability depends on your deployment; self-host for production use.

            License: see npm package LICENSE · https://www.npmjs.com/package/netease-cloud-music-api-alger
            """,

        ["Settings.Recognition.Guide.Title"] = "Music recognition API setup",

        ["Settings.Recognition.Guide.Body"] =
            """
            Music recognition setup

            Default (recommended): recognition API = netease://bundled
            Uses the bundled NetEase API (http://localhost:3000). Record 5–20 seconds, then click Identify.

            Also enable Bundled NetEase API in Settings and turn on auto-start.

            Optional external: recognition API = https://api.audd.io/?api_token=YOUR_TOKEN
            (uploads raw audio; requires a third-party token)

            See docs/product/music-recognition-api-guide.md in the repository for details.
            """,
    };

    public static bool TryGet(string key, string culture, out string value)
    {
        var dict = string.Equals(culture, LocalizationService.EnUs, StringComparison.OrdinalIgnoreCase)
            ? EnUs
            : ZhCn;
        return dict.TryGetValue(key, out value!);
    }
}
