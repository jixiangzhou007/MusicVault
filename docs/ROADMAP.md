# MusicVault 产品路线图

产品意图与已决议边界以 **[PRODUCT_DESIGN.md](PRODUCT_DESIGN.md)** 为准。施工顺序服从该文档第 14 节（公开产品批次 1–3；90 天冻结项）。

## 已完成

| 阶段 | 主题 | 要点 |
|------|------|------|
| **架构 阶段 3（首次运行向导）** | **首次配置** | 版权同意后 3 步向导（欢迎 / 选路 / 路径详情）；`FirstRunOnboardingPath`；已有曲库用户自动跳过 |
| **架构 阶段 2（缓存/迁移）** | **统一启动迁移** | `StartupMigrationService`、`AppSchemaVersion`；`PlayHistory` 表 + `PlayHistoryRepository`；播放历史迁入 DB；`download-history.json` 由迁移统一调度 |
| **架构 阶段 1（还债）** | **DI + Features** | `ServiceCollectionExtensions`、`AppServices` DI、`ArtistDetailRefreshService` / `ArtistAlbumCatalogBuilder` / `TrackMetadataPersistenceService`；艺术家页与元数据侧栏接线；`docs/ARCHITECTURE.md`、`docs/DATA_MODEL.md` |
| 设计系统 P0–P4 | UI 规范 | Token、强调色、样式拆分、预览页 |
| 架构 阶段一～三 | 模板 / 元数据 / 列表交互 | TrackListInteraction、平台统一 |
| 稳定化 P0 | 可靠 / 诚实 / 可观测 | 播放日志、平台芯片、ShellStatus |
| 稳定化 P1 | 传输与配置 | 批量重试/清除、API 向导 |
| 产品 P2 | 下载控制 | 暂停/继续/取消、搜索防抖 |
| 产品 P3 | 发现与偏好 | 字号档位、合规说明、批量入队、复制失败原因 |
| **产品 P4（当前）** | **向导与历史** | 批量入队确认、播放历史清空/切歌、下载状态机测试、曲库基准 |

## P4 本阶段交付

- [x] **发现页**：批量入队 **确认向导**（预览曲目数 / 音质可入队数后再确认）
- [x] **播放历史**：单击/双击历史行切歌；**清空**（保留当前正在播放）
- [x] `DownloadQueueService` 暂停/继续/取消 **mock 集成测试**
- [x] `scripts/benchmark-library.ps1` + `LibraryQueryBenchmarkTests`（5000 曲搜索预算）
- [x] **关于** 对话框 macOS 播放能力说明强化

## 阶段 0（合规基线）— 已完成

- [x] `LICENSE`（MIT）、`NOTICE`、`THIRD_PARTY_LICENSES.md`
- [x] `docs/LEGAL.md`、`CHANGELOG.md`、`docs/GITHUB_RELEASE.md`、`docs/REPOSITORY.md`
- [x] README 功能分级与 macOS 播放限制说明

## 阶段 4 / P5（进行中）

| 项 | 状态 | 说明 |
|----|------|------|
| **i18n** | 已完成 | 顶栏/传输/歌单/偏好 + 发现/播放栏/曲库/整理工具/设置全部分区 UI；内置 API 版本、均衡器面板、平台能力、使用统计、更新状态与 Shell 分类已接入；统一 API 错误已映射为 `ServiceMessages`（不再透传后端原文） |
| **传输页筛选记忆** | 已完成 | 队列/历史/失败三视图独立保存关键词、平台、排序；启动时恢复当前视图筛选；Headless 集成测试 |
| **发现页批量入队** | 已完成 | 结果 ≥2 首时「全部加入下载」；歌单/专辑解析后可自动弹出确认向导（偏好 → 下载策略可关） |
| **P1 对比度** | 已完成 | `OnAccentSoftForegroundBrush`：浅强调底上的选中正文/标签 |
| **UI 冒烟（Headless）** | 已完成 | `MainWindowHeadlessSmokeTests` + `scripts/smoke-ui-headless.ps1`；顶栏渲染、四大分区导航、设置侧栏本地化 |
| **产品体验 P2（部分）** | 已完成 | 整理工具页头副标题（MainWindow 壳层）、首页播放历史 i18n、播放栏单击全屏/双击精简 |
| **NAS N2** | 已完成 | DPAPI 凭据存储、SMB 自动挂载、启动重连、旧明文密码迁移；连接对话框 i18n +「保存账号」 |
| **NAS N3** | 已完成 | 在线/离线状态、离线容量统计、重连 NAS、启动挂载失败提示 |
| **NAS N4** | 已完成 | 边播边缓存、下载到本地库、整库下载、传输页 NAS 筛选、设置目标库与缓存管理 |
| **NAS N5 WebDAV** | 已完成 | WebDAV 连接/扫描/播放缓存/下载复制；歌曲页与库来源页 + 菜单；启动检测；`WebDavClientTests` |
| **首页常听 + 推荐（P3）** | 已完成 | 常听列表；推荐「最近听过」（相对时间 + 同歌手补位）；与常听/历史去重 |
| **WebDAV 扫描元数据** | 已完成 | 新入库 ≤25MB 文件临时下载读标签；扫描后补全缺失时长/歌手 |
| **WebDAV 大文件标签** | 已完成 | >25MB Range 读头 2MB + 尾 128B（mp3/aac/m4a）；稀疏临时文件 TagLib 解析 |
| **远程播放缓存** | 已完成 | 合并并发缓存任务，避免 WebDAV/NAS 重复下载 |
| **首页相似曲推荐** | 已完成 | 库内 AcoustID 标签匹配 + 同专辑补位；推荐行「相似 / 同专辑」标签 |
| **AcoustID 在线 lookup** | 已完成 | fpcalc + AcoustID API；设置 → 数据与备份可开关/API Key；环境变量可覆盖 Key |

## 设置页 IA v4（2026-06）

- 左侧 Nav 7 项：概览 / 外观 / 播放与音频 / 下载与缓存 / 数据 / 高级 / 关于与合规
- 播放与音频页内 Tab：播放 · 歌词 · 音效 · 快捷键
- 高级页内 Tab：在线源 · 更新与诊断（后者保留 4 子 Tab）
- 概览：关于书（身份 → 说明/开源 → 轻量快捷入口）；曲库 KPI 迁「数据」；已移除功能卡片轮播
- NAS/WebDAV 播放缓存并入「下载与缓存」

## 下一阶段建议

产品意图与冻结项以 [PRODUCT_DESIGN.md](PRODUCT_DESIGN.md) 为准。

**当前施工：** [桌面播放器 UI 统一](superpowers/plans/2026-08-17-page-ui-hardening.md)（4 种页模板 + Win/Mac 窗口铬，再套曲库/发现/整理/下载）。双核体验对齐已落地。不要按下方旧 backlog 再扩平台或重做识曲。

历史体验清单（多数已落地）见 `docs/execution/optimization-backlog-2026-07-27.md`。LibVLC Windows 人工冒烟 / macOS 实机仍可作播放稳定性旁路，不挡批次 1。

## UI 冒烟测试

结构/绑定检查（快速，无 GUI）：

```powershell
.\scripts\smoke-ui-navigation.ps1
.\scripts\smoke-settings-designsystem.ps1
.\scripts\smoke-libvlc.ps1
```

Avalonia Headless（真实控件树 + 导航）：

```powershell
.\scripts\smoke-ui-headless.ps1
```

发布演练会依次执行上述三项（可用 `-SkipSmoke` 跳过）。

## 发布

```powershell
powershell -File scripts/publish-with-api.ps1
```

输出：`publish/win-x64-vol/MusicVault.exe`
