# MusicVault · Ant Design 结构规范（桌面版）

> **附录文档**：Ant 反馈分层与阶段 0–6 迁移验收以本文为准；**颜色 / Token / 排版 / 间距 / 组件 class 的单一真相来源**见 [DESIGN_SYSTEM.md](./DESIGN_SYSTEM.md)。

> 阶段 0 基线文档。后续 UI 批次按此验收；**每阶段完成并确认后再进入下一阶段**。

## 设计原则

- **借 Ant 的结构与反馈分层**，不照搬 Web 布局（无 FloatButton、无右下角 Notification 堆叠等）。
- **强调色** `#007AFF`，控件圆角 **4px**（`RadiusControl` / `RadiusButton`）。
- **深色为默认**；浅色通过 `ThemeResources.Light.axaml` 切换。

---

## 1. 反馈分层（何时用什么）

| 层级 | Ant 对应 | MusicVault 实现 | 使用场景 |
|------|----------|-----------------|----------|
| 轻量结果 | Message | 顶栏 `TopMessageHost` | 保存成功、入队 N 首、复制失败原因等，**不打断操作** |
| 页内说明 | Alert | `ant-alert`（info/warning/error） | 发现页合规提示、设置说明、需持续可见的警告 |
| 空数据 | Empty | `EmptyStateView` / `ant-empty` | 曲库/传输/歌单无数据 |
| 短确认 | Modal（小） | `AppModal` 居中卡片 | 2～3 按钮、少量文案；**阶段 1 起**部分改为 Popconfirm |
| 长确认/预览 | Modal（大） | `AppModal` + `app-modal-panel-wide` | 批量入队预览、删除库等 |
| 流程结束 | Result | `ResultView` / `ant-result` | 刮削完成、API 更新结果（图标+摘要+按钮） |
| 向导步骤 | Steps | `StepsIndicator` / `ant-steps` | 批量入队确认（选择→确认） |
| 侧栏任务 | Drawer | `ant-drawer-host` + `RightSidebarView` | 歌词、元数据、队列 |
| 大表单 | Drawer（Form） | `FormDrawerHost` + `ant-drawer-host`（ZIndex 170） | 歌词编辑、组合标签搜索、平台登录 |
| 悬停说明 | Tooltip | `ToolTipControlThemes` | 图标按钮、截断标题 |
| 已知进度 | Progress | `ant-progress-line*` | 下载、扫描、弹窗任务 |
| 列表加载 | Skeleton | `Skeleton*` 控件 + `ant-skeleton` | 专辑/艺术家浏览、发现搜索、曲目表首屏 |
| 加载（短） | Progress active | `IsIndeterminate` + `ant-progress-line` | 艺术家刷新资料、设置检查更新 |

**禁止混用**：不要用 Alert 代替 Message；不要用全屏 Modal 代替 Tooltip。

---

## 2. 语义状态色（四套）

Tag、Alert、Progress、Badge 应共用下列语义，**不要另造一套红/绿**。

| 语义 | 主色 Token | 典型用途 |
|------|------------|----------|
| **Info** | `AccentBrush` + `AccentSoftBrush` 底 | 提示、进行中、主操作 |
| **Success** | `SuccessBrush` | 完成、可播放、下载成功 |
| **Warning** | `WarningTextBrush` / `WarningSoftBrush` | 合规、配置缺失、需注意 |
| **Error** | `DangerBrush` | 失败、删除、危险操作 |

Alert 背景/边框见 `Styles/Components/Feedback.axaml`（`ant-alert-info/warning/error`）。

Progress 成功/失败变体：`ant-progress-line-success` / `ant-progress-line-exception`。

Result / Steps 图标圈与上述四套语义对齐，见 `Styles/Components/Result.axaml`、`Steps.axaml`。

---

## 3. 动效 Token

定义于 `Styles/Tokens/Motion.axaml`：

| Token | 值 | 用途 |
|-------|-----|------|
| `MotionDurationFast` | 150ms | Tooltip 淡入 |
| `MotionDurationBase` | 250ms | Drawer 滑入、蒙层淡入 |
| `MotionDurationSlow` | 350ms | 预留 |
| `DrawerSlideOffsetPx` | 560px | Drawer 关闭位 translateX，**必须用 px** |
| `DrawerWidthDefault` | 360px | 右侧栏默认宽 |

**技术约束（已踩坑）**：

- `RenderTransform` 勿写 `translateX(100%)`，Avalonia 会启动失败。
- `RenderTransformOrigin` 用 `0,0.5` 或 `1,0.5`，勿用 `100%,50%`。

---

## 4. 明确不适配（保持现状）

| 能力 | 决策 | 原因 |
|------|------|------|
| **均衡器** | 保持 **居中 Modal** | 横向频段多，需宽卡片；不适合右侧 Drawer |
| **播放进度** | 保持 **自定义 seek 条** | 可拖动；不用 Progress 组件 |
| **音量** | 保持 **Flyout/Popover** | 就近锚定，非 Drawer |
| **偏好中心导航** | 保持 **左侧 ListBox** | 已产品定稿，不用顶栏 Tabs |
| **右下角 Notification** | 不引入 | 已有顶栏 Message，避免双层反馈 |
| **完整 Ant Table** | 不引入 | 虚拟列表 + `ant-table-header` 即可 |

---

## 5. 阶段路线图（执行顺序）

| 阶段 | 内容 | 状态 |
|------|------|------|
| **0** | 本文档 + 设计系统预览区 + Motion Token | ✅ 已完成 |
| **1** | Popconfirm + 危险按钮 | ✅ 已完成 |
| **2** | Result + Steps（入队向导） | ✅ 已完成 |
| **3** | Skeleton | ✅ 已完成 |
| **4** | Drawer 大表单 | ✅ 已完成 |
| **5** | Badge / Popover / 扫尾 | ✅ 已完成 |
| **6** | 发布后扫尾（Modal 清理 / ant-popover / 艺术家刷新骨架） | ✅ 已完成 |

---

## 6. 阶段 0 验收清单

- [x] 偏好 → 设计系统（或 `DesignSystemWindow`）可打开预览页
- [x] 预览页含 **反馈分层**、**语义状态色**、**动效 Token**、**不适配清单** 四个区块
- [x] `Motion.axaml` 已并入 `App.axaml` 资源
- [x] 本文档路径：`docs/DESIGN-SYSTEM-ANT.md`
- [x] 无业务页面行为变更（仅文档与预览）

---

## 7. 阶段 1 验收清单（Popconfirm）

- [x] `PopconfirmHost` + `ant-popconfirm` 样式；窗口居中（`PlacementMode.Center` + `PlacementTarget=TopLevel`）
- [x] 危险主按钮 `ant-btn-danger` / `danger`
- [x] 设计系统预览区含 Popconfirm 示例
- [x] 下载/曲库等至少一处真实接入

---

## 8. 阶段 2 验收清单（Result + Steps）

- [x] `ResultView` + `Styles/Components/Result.axaml`（success / error / info / warning）
- [x] `StepsIndicator` + `StepItemView` + `Styles/Components/Steps.axaml`（wait / process / finish / error）
- [x] **Result** 接入：`BatchScrapeResultModalView`、`MusicApiUpdateModalView`（更新完成后 success/error）
- [x] **Steps** 接入：`ConfirmBatchEnqueueModalView`（选择曲目 finish → 确认入队 process）
- [x] 设计系统预览区含 Result、Steps 示例
- [x] 未改动：均衡器 Modal、Popconfirm 居中逻辑、播放条 UI

**延后（非本阶段范围）**：

- 设置页「检查更新」仅页内状态条，无独立 Result 弹窗（保持 Message/Alert 分层）
- 批量入队第 3 步「写入队列」在确认后于页内执行，未扩展为多步 Modal 向导

---

## 9. 阶段 3 验收清单（Skeleton）

- [x] `Styles/Components/Skeleton.axaml`（`ant-skeleton` / avatar / title / paragraph / pulse）
- [x] 可复用控件：`SkeletonBlock`、`SkeletonLine`、`SkeletonPlaceholder`；列表模板：`SkeletonBrowseListView`、`SkeletonDiscoverResultsView`、`SkeletonAlbumTrackListView`、`SkeletonAlbumGridView`
- [x] **艺术家 / 专辑** 浏览列表首屏：`IsLoadingBrowse` + `SkeletonBrowseListView`
- [x] **发现** 搜索中：`ShowResultsSkeleton` + `SkeletonDiscoverResultsView`（Empty 与骨架互斥）
- [x] **专辑曲目** 详情加载：`SkeletonAlbumTrackListView`（替换原 indeterminate Progress）
- [x] **曲库全部歌曲** 首屏重载：`VirtualTrackCollection.ReloadAsync` 置 `IsLoading` + `ShowTracksSkeleton`
- [x] 设计系统预览区含 Skeleton 示例
- [x] 未改动：下载/扫描等已知进度 Progress、Popconfirm 居中、Result/Steps

**扫尾后（见 §10）**：艺术家专辑轮播骨架、链接解析骨架、LoadMore 底行骨架、批量刷新资料骨架均已接入。

---

## 10. 阶段 0–3 扫尾清单

> 2026-05-28 执行；不进入阶段 4（Drawer 大表单）/ 阶段 5。

### 阶段 0

- [x] 设计系统预览页含反馈分层、语义色、动效 Token、不适配清单
- [x] 预览页含 Popconfirm（**窗口居中**）、Result、Steps、Skeleton 示例
- [x] 反馈分层表格 Popconfirm 文案改为「窗口居中」
- [x] 未改动 `Motion.axaml` / Theme Token

### 阶段 1（Popconfirm 缺口）

- [x] **歌曲页**：删除当前库 → Popconfirm（更多菜单触发）
- [x] **歌曲 / 艺术家页**：上下文「从库中删除」→ Popconfirm（双选项：仅索引 / 删文件）
- [x] **传输页**：任务菜单「取消进行中」→ Popconfirm 后再批量取消
- [x] `PopconfirmHost.ShowConfirm()` 供无触发按钮场景（菜单 / 上下文）
- [x] 复杂流程仍用 Modal：批量入队、刮削、元数据编辑等

### 阶段 2（Result / Steps）

- [x] 设置页「检查更新」保持页内 Progress + Message（**有意延后** Result 弹窗）
- [x] 批量入队未扩展第 3 步 Modal 向导（**有意延后**）

### 阶段 3（Skeleton 缺口）

- [x] 艺术家详情 **专辑横向轮播**：`ShowArtistAlbumsSkeleton` + `SkeletonAlbumGridView`
- [x] **链接解析** `IsParsingLink`：结果区 `ShowResultsSkeleton`（与搜索骨架互斥 Empty）
- [x] **歌曲页 LoadMore**：`ShowTracksLoadMoreSkeleton` 底部单行骨架
- [x] **`IsRefreshingArtistProfiles`**：批量刷新时禁用按钮 + 状态文案 + 列表轻量骨架 overlay

### 仍延后（阶段 5+）

- [x] Badge / Popover 扫尾（阶段 5）
- [x] `ConfirmRemoveTrackModalView` / `ConfirmDeleteLibraryModalView` 已删除（Popconfirm 替代，阶段 6）

---

## 11. 阶段 4 验收清单（Drawer 大表单）

- [x] `FormDrawerHostViewModel` + `IFormDrawerHost`；`MainWindow` 独立 `ant-drawer-host`（ZIndex 170，宽 420–560px）
- [x] 蒙层点击、Esc、标题栏关闭/取消按钮可关闭
- [x] **歌词编辑** `MetadataLyricsEditorModalView` → 右侧 Drawer（元数据侧栏「编辑歌词」）
- [x] **组合标签搜索** `CombinedLabelsSearchModalView` → 右侧 Drawer（在线识别）
- [x] **平台登录** `MusicApiAccountModalView` → 右侧 Drawer（设置 → 平台登录）
- [x] 小确认/编码修正等仍用居中 `AppModal`；**均衡器**仍为居中 Modal
- [x] 设计系统预览：Drawer 文案含 Form 大表单说明
- [x] 未改动：Popconfirm 窗口居中、Result/Steps、Skeleton、播放 seek、音量 Flyout、偏好 ListBox

**延后**：

- `MetadataEncodingFixModalView` 仍为居中 Modal（字段级短面板，非宽表）
- Form Drawer 与 App Modal 同时打开互斥（沿用各自 `IsOpen` 检查，未做全局编排）

---

## 12. 阶段 5 验收清单（Badge / Popover / 扫尾）

- [x] `Styles/Components/Badge.axaml`（`ant-badge` / `ant-badge-count` / `ant-badge-dot` / `ant-badge-status` 及 success/error/processing 变体）
- [x] `Styles/Components/Popover.axaml`（`ant-popover` / `ant-popover-flyout` / `ant-popover-inline`；`player-flyout-panel` / `player-elevated-popover` 别名；`FlyoutPresenter` 统一）
- [x] `AppStyles.axaml` 引入 Badge.axaml、Popover.axaml
- [x] **Badge 迁移**：顶栏传输 `ActiveDownloadCount` → `ant-badge-count`；`download-status-badge*` / `batch-scrape-status-badge*` 映射至 `ant-badge-status`（旧 class 保留为别名）
- [x] **Popover 接入**：传输页更多菜单、播放条音量 Flyout 内容区加 `ant-popover`
- [x] 设计系统预览区含 Badge、Popover 示例
- [x] 未改动：Popconfirm 居中、Drawer、Skeleton、传输页布局、均衡器 Modal
- [x] `DownloadTransferTableView` 无引用（已不存在）；删除确认 Modal 已清理（阶段 6）

**延后**：

- [x] 全量 XAML Modal 浮层追加 `ant-popover` class（保留 `player-elevated-popover` 别名，阶段 6）

---

## 13. 阶段 6 验收清单（发布后扫尾）

- [x] 删除无调用方的 `ConfirmRemoveTrackModalView` / `ConfirmDeleteLibraryModalView` 及 ViewModel、Choice 枚举、`AppModalHostViewModel.Show*` 方法、`AppModalOverlayView` DataTemplate
- [x] Popconfirm 删除流程保留：`TracksPageView`、`ArtistsPageView`、`LibrariesPageView`
- [x] Modal 浮层（`Views/Overlays/*.axaml`、`EqualizerOverlayView.axaml`、设计系统预览）根 Border 追加 `ant-popover`（保留 `player-elevated-popover` 别名）
- [x] 未对 BoxShadow 使用 `{x:Null}`（避免启动 InvalidCastException）
- [x] **艺术家批量刷新**：`IsRefreshingArtistProfiles` + `ShowArtistProfilesRefreshSkeleton` + `SkeletonBrowseListView` overlay；按钮禁用与 `BrowseStatsLine` 状态文案并存
- [x] 未影响 Popconfirm / Drawer / 传输页布局
- [x] 设计系统预览注明阶段 0–6 已完成
- [x] Release 构建与 `publish-with-api.ps1 -Restart` 通过

---

## 14. 艺术家数据治理

- **入库门槛**：曲目 `Artist` / `AlbumArtist` 写入前，每一段艺术家名须能在 **网易云** 或 **QQ 音乐** 检索到明确匹配（`ArtistNameMatcher` 同名/高相似度）；两平台均无结果则该段不入库。平台 API 均未配置时跳过校验，避免离线扫描清空字段。
- **资料拉取**：`ArtistProfileService` 在抓取百科前同样校验；未识别则记为 `NotFound`，不写入虚假资料。
- **数据治理按钮**（艺术家列表工具栏）：一次确认后顺序执行 ① 合并重复名称 → ② 清理无效艺术家（曲目字段 + `ArtistProfiles`）→ ③ 为仍有效艺术家更新百科；进度与结果通过顶栏 Message + `BrowseStatsLine` 汇总。
