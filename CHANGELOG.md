# Changelog

本文件记录面向用户与分发者的版本变更。格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

## [Unreleased]

### Added

- **P0-1 macOS 原生播放器**：通过原生 dylib（`libMusicVaultPlayer.dylib`）调用 AVAudioPlayer，替代直接 ObjC interop，解决 ARM64 兼容性问题。新增 `src/MusicVault.App/native/macOS/MusicVaultPlayer.m` 原生文件，macOS 发布脚本自动编译。
- **P0-3 Track 实体文档化**：`Track` 类增加身份/元数据/可变统计三类属性文档注释，新增 `With()` 方法支持不可变风格更新。

### Changed

- **P0-2 服务生命周期**：`ServiceCollectionExtensions` 增加完整的生命周期策略文档。无状态编排器/工具类（`ArtistDetailRefreshService`、`BatchMetadataService`、`BatchOrganizeService` 等 15 个）从 Singleton 改为 Transient，明确设计意图。
- README 功能表按「完整 / 实验 / 需配置」分级；macOS 播放能力已更新。
- **P1-4 结构化日志**：集成 Serilog + `Microsoft.Extensions.Logging`。新增 `AppLog` 启动器（滚动文件日志，保留 14 天）、`MusicVaultLog` 强类型日志消息（Source Generator）、`ILogger<T>` DI 注入。替换 `catch { /* ignore */ }` 为结构化日志。
- **P1-5 数据库迁移版本化**：`AppSchemaVersion` 从 2 个版本扩展为 v1–v11 完整枚举，含版本说明字典。`DatabaseInitializer` 记录迁移进度并通过 `OnMigrationApplied` 回调通知。
- **P1-7 API 弹性**：新增 `PlatformHealthMonitor`（每平台健康追踪、3 次失败熔断、2 分钟冷却自动恢复）。注册到 DI 并暴露于 `AppServices.ApiHealth`。
- **P1-8 服务层 i18n**：新增 `ServiceMessages` 双语消息注册表（45 个 key，zh-CN/en-US）。`DownloadApplicationService` 已接入；其他 Application 服务逐步迁移。
- **P2-11 恢复播放位置**：`PlayerBarViewModel` 增加「从上次位置继续」提示（5 秒自动消失）。复用已有 `LastPositionSeconds` 追踪，播放时自动检测并展示恢复文案。
- **P2-10 Velopack 安装器完善**：发布脚本增加应用标题/作者/快捷方式/图标参数。生成更新源清单提示。
- **P2-9 iTunes 导入**：新增 `iTunesLibraryParser`（PList XML 解析器），支持读取曲目元数据与播放列表，提取本地可用文件路径。
- **P3-15 性能指标**：新增 `AppMetrics`（`System.Diagnostics.Metrics`）。6 个计数器 / 4 个直方图 / 1 个仪表。`LibraryScanService` 已接入扫描计时与曲目计量。内置 64 条诊断快照。
- **P3-18 Roslynator 配置**：新增 `.roslynatorconfig`，启用 23 条 Roslynator 分析器（RCS 系列），覆盖命名/简化/判空等规则。
- **P4-19 反馈渠道**：`.github/ISSUE_TEMPLATE/` 增加 Bug Report + Feature Request YAML 模板。`CONTRIBUTING.md` 含开发流程、代码规范、项目结构。
- **P4-21 品牌视觉**：新增 `assets/app-icon.svg`（黑胶唱片 + V 字母图案，强调色 `#007AFF`）。
- 阶段 0 合规基线：`LICENSE`、`NOTICE`、`THIRD_PARTY_LICENSES.md`、`docs/LEGAL.md`、本变更日志与 GitHub Release 模板。

## [1.0.10] - 2026-05-24

### Added

- 艺术家详情：进入页读库内缓存；「刷新资料」才请求网络。
- 艺术家头像优先百度百科词条主图。
- 网易云官方专辑目录过滤（单曲盘、现场、错配歌手等）。
- 刷新资料时简介与专辑并行加载，先完成先展示。

### Fixed

- 艺术家刷新竞态导致专辑列表被清空的问题。
- 非百度百科来源的简介在详情页不显示的问题。

## [1.0.0] - 2026-04

### Added

- 初始公开发布：曲库扫描、多源发现与下载、元数据编辑、播放与歌词、Velopack 更新、Windows 单文件发布脚本。

---

版本号以 `Directory.Build.props` 中的 `Version` 为准。发布前请同步更新本文件与 `src/MusicVault.App/Data/release-notes.json`（若使用）。
