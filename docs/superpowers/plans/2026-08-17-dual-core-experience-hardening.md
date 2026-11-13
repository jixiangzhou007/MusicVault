# 双核体验对齐 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把现行 1.0.11 的界面与文案对齐「听是必须、下是尽力」：发现/榜单下载降为次要；在线预览可在同一底栏切歌；对外口号不再营销下载。

**Architecture:** 不拆巨型 ViewModel、不改顶栏六项、不改设置 IA。文案走现有 l10n 键；按钮只改 class / 主次。预览切歌在现有 `PlayerBarViewModel` 在线预览路径上，把当前结果列表写成临时队列，不新开播放器。

**Tech Stack:** Avalonia 11、.NET 8、CommunityToolkit.Mvvm、现有 xUnit / Headless 冒烟。

**Spec:** [2026-08-17-post-dual-core-optimization.md](../specs/2026-08-17-post-dual-core-optimization.md)

**Freeze (do not schedule):** 新平台、识曲重写、设置 4 项大改、拆 Search/Settings/Artists 专项、Linux/iOS/Intel Mac 朋友包。

---

## 文件地图

| 文件 | 职责 |
|------|------|
| `LocalizationResources.cs` | `About.Tagline`、`Home.Empty.Title` |
| `DiscoverLocalizationResources.cs` | `Discover.PageHeader` |
| `README.md` | 首行口号 |
| `docs/PRODUCT_DESIGN.md` | §8 歌单在线听状态；§11.1 字段已落地 |
| `SearchPageView.axaml` | 批量入队 `primary` → `flat-btn` |
| `PlaylistsPageView.axaml` | 榜单批量下载 `primary` → `flat-btn` |
| `PlayerBarViewModel.cs` (+ Transport) | 在线预览临时队列；上一首/下一首 |
| `OnlineTrackViewModel.cs` | 预览成功后把列表交给底栏 |
| `tests/MusicVault.Tests/` | 预览队列纯逻辑 + 文案键存在 |
| `.gitattributes` | `*.cs text eol=crlf`（波次 4） |

---

## 波次总览（改什么 / 不改什么）

| 波次 | 用户能看见什么变化 | 不动 |
|------|-------------------|------|
| **0** | 关于页、设置导览、README、空首页标题、发现页职责文案；文档与代码一致 | 按钮层级、播放逻辑、顶栏 |
| **1** | 发现「全部加入下载」、榜单「批量下载」变成次要按钮；播放仍是主按钮 | 顶栏下载中心、下载功能本身 |
| **2** | 发现/歌单里听在线歌，底栏可上一首/下一首 | 不伪造本地文件、不改 NAS 播放 |
| **3** | 空首页次要区不再把「下载中心」和主路径并排强调 | 不改向导步骤、不加 NAS 向导步 |
| **4** | 朋友更敢发：测试补洞、换行策略、Mac 实机清单 | 不一次 format 全仓 |

---

## 波次 0：文档与口号自洽

**用户可见：** 打开「关于」、设置里的软件介绍、README 第一句、空首页大标题，不再把下载当卖点。

### 详细改动

| # | 文件 | 键 / 位置 | 现在 | 改为 |
|---|------|-----------|------|------|
| 1 | `LocalizationResources.cs` | `About.Tagline` zh | 下载即整理，整理即播放 | 一个播放器，听本地和多个平台 |
| 2 | 同上 | `About.Tagline` en | Download, organize, play | One player for local files and many platforms |
| 3 | `GuideLocalizationResources.cs` | `Settings.Guide.Tagline` zh/en | 同上旧口号 | 与 About 同一套 |
| 4 | `README.md` | 第 3 行 | **下载即整理，整理即播放** — … | **一个播放器，听本地和多个平台** — … |
| 5 | `LocalizationResources.cs` | `Home.Empty.Title` zh | 开始你的音乐库 | 开始听 |
| 6 | 同上 | `Home.Empty.Title` en | Start your music vault | Start listening |
| 7 | `DiscoverLocalizationResources.cs` | `Discover.PageHeader` zh | …并下载到本地；…下载中心 | 搜索、导歌单、双击即可在线听。下载能用就用，不是必须步骤。 |
| 8 | 同上 | en | Search … download to your library… | Search, import playlists, double-click to play online. Downloads are optional. |
| 9 | `LocalizationResources.cs` | `Home.Empty.Workflow` zh/en | 在线源 → 下载中心 → 曲库 | 本地文件夹或发现里听歌（空页未展示，避免死文案继续营销下载） |
| 10 | `PRODUCT_DESIGN.md` §8 | 平台歌单在线听 | **目标**（现行偏下载后入库） | **完整** |
| 11 | `PRODUCT_DESIGN.md` §11.1 | FirstRunOnboardingPath | 「目标新增字段」 | 「现行字段」 |
| 12 | `ROADMAP.md` | 当前施工 | 批次 1–3 已落地 | 指向本计划波次 0–2 |
| 13 | `2026-08-14-dual-core-alignment.md` | 任务勾选 | 仍是 `- [ ]` | 已落地项勾上 |

**不改：** 顶栏文案、发现搜索框、播放栏、任何按钮 class、向导三步。PRODUCT §1 仍可解释旧口号只适用于「下载成功之后」。发现 `PageHeaderText` 目前未绑到 Search 页，本波只改正文、不新增页眉条（避免加回 chrome）。

### Task 1: 对外口号改为核 B 向

**Files:**
- Modify: `src/MusicVault.App/Localization/LocalizationResources.cs`（`About.Tagline` zh+en）
- Modify: `README.md` 首行
- Modify: `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs`（`Discover.PageHeader`）
- Modify: `src/MusicVault.App/Localization/LocalizationResources.cs`（`Home.Empty.Title`）

建议文案（可微调，须中英同步）：

- zh `About.Tagline`：一个播放器，听本地和多个平台
- en `About.Tagline`：One player for local files and many platforms
- zh `Home.Empty.Title`：开始听
- zh `Discover.PageHeader`：搜索、导歌单、双击即可在线听。下载能用就用，不是必须步骤。

- [x] **Step 1:** 改四组 zh+en 键与 README 首行；核 A 口号可留在 PRODUCT §1 解释「下载成功之后」
- [x] **Step 2:** 绑定或删除死文案：若 `SearchPageView` 不展示 `PageHeaderText`，要么绑到页头，要么停止在 VM 暴露
- [x] **Step 3:** `dotnet test --filter "FullyQualifiedName~OnlineDownloadAvailability"` 仍通过（文案键测试不依赖口号）

### Task 2: PRODUCT 与实现对齐

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` §8 平台歌单行：「目标」→「完整」（若仍有缺口则写清剩余一句）
- Modify: `docs/PRODUCT_DESIGN.md` §11.1：去掉 `FirstRunOnboardingPath` 的「目标新增」
- Modify: `docs/ROADMAP.md` 当前施工：指向本计划波次 0–2
- Modify: `docs/superpowers/plans/2026-08-14-dual-core-alignment.md` 已完成项勾选（文档滞后）

- [x] **Step 1:** 只改文档，不改产品行为
- [x] **Step 2:** 确认没有重新把下载写成 P0

---

## 波次 1：听优先视觉（Windows 可验）

**用户可见：** 搜到歌之后，工具栏不再用蓝主按钮推「全部加入下载」；打开在线榜单时，主按钮是「在线播放」，下载变成普通按钮。下载功能还在，能下仍可下。

### 详细改动

| # | 文件 | 控件 | 现在 | 改为 |
|---|------|------|------|------|
| 1 | `SearchPageView.axaml` ~L417 | `BatchEnqueueAllCommand` 按钮 | `Classes="primary btn-toolbar"` | `Classes="flat-btn btn-toolbar"` |
| 2 | `PlaylistsPageView.axaml` ~L360 | `BatchDownloadChartCommand` | `Classes="primary btn-toolbar"` | `Classes="flat-btn btn-toolbar"` |

**保持不变：**

- `PlaylistsPageView.axaml` ~L372–378：平台歌单「在线播放」= `primary`，「批量下载」= `flat-btn`（已对齐）
- 顶栏「下载中心」仍在，角标仍在
- 行内下载图标仍显示；`IsEnabled` / `CanExecute` 仍走 `OnlineDownloadAvailability`
- 命令逻辑、入队确认弹窗、自动弹出批量向导：不改行为，只改按钮看起来像不像主操作
- 专辑/艺术家页「下载所选」：本波不动（库内补齐，偏核 A）

验收：发现结果 ≥2 首时，主色按钮不应是批量下载；榜单详情里「在线播放」比「批量下载」更醒目。

### Task 3: 发现与榜单下载按钮降权

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml` 约 L417：`BatchEnqueueAll` `primary` → `flat-btn`
- Modify: `src/MusicVault.App/Views/Pages/PlaylistsPageView.axaml` 约 L360：榜单 `BatchDownloadChart` `primary` → `flat-btn`
- Do not change: 平台歌单详情已是播放 primary + 下载 flat
- Do not change: 顶栏六项（冻结 IA）
- Do not hide: 下载按钮；`CanExecute` 仍走 `OnlineDownloadAvailability`

- [x] **Step 1:** 只改 class，不改命令逻辑
- [x] **Step 2:** Headless 冒烟：`.\scripts\smoke-ui-headless.ps1`（若有）或 `dotnet test --filter FullyQualifiedName~MainWindowHeadlessSmokeTests`
- [x] **Step 3:** 发布重启：`.\scripts\publish-with-api.ps1 -Restart`

验收：发现结果区主视觉不再是「全部加入下载」；榜单详情主按钮应是「在线播放」而非批量下载。

---

## 波次 2：在线预览临时队列

**用户可见：** 在发现或歌单里双击一首在线歌，底栏上一首/下一首可用，切到列表里相邻的歌，仍在同一窗口出声，不用先下载。点本地曲库里的歌之后，切歌恢复成曲库队列。

### 详细改动

| # | 文件 | 做什么 |
|---|------|--------|
| 1 | 新建 `OnlinePreviewQueue.cs` | 纯函数：列表 + 当前索引 + 循环模式 → 能否上/下一首、目标索引 |
| 2 | 新建 `OnlinePreviewQueueTests.cs` | 1 首不能切；≥2 可切；循环到头/尾；清空后不能切 |
| 3 | `PlayerBarViewModel` | 持有临时在线队列；`CanGoPrevious`/`CanGoNext` 在预览模式读队列；上一首/下一首调用下一首的 Preview |
| 4 | `SearchPageViewModel` / `PlaylistsPageViewModel` | 双击或 Preview 成功时，把当前结果列表 + 索引交给底栏 |
| 5 | 本地 `PlayTrackAsync` | 开始播本地/NAS 时 `ClearOnlinePreviewQueue()` |

**行为细则：**

- 队列来源：发现 `Results`（含 pager 已展示部分，优先当前可见列表）或歌单 `DetailOnlineTracks`
- 切歌仍走现有在线预览（解析 URL → 底栏 `_isOnlinePreview`），不把 `OnlineTrack` 写成本地 `Track`
- 重复模式：跟底栏现有 `RepeatMode`；默认列表循环
- 失败：下一首解析失败则提示原因，停在当前首，不静默跳过整表（可在实现时「跳过不可预览」若测试要求）
- NAS / 本地文件：不进这条队列

**不改：** 下载队列、传输页、识曲。

### Task 4: 抽出可测的预览队列规则

**Files:**
- Create: `src/MusicVault.App/Services/OnlinePreviewQueue.cs`（或放 Core/Services：纯函数，输入当前列表+索引，输出能否上一首/下一首及目标）
- Test: `tests/MusicVault.Tests/App/OnlinePreviewQueueTests.cs`

行为：

- 预览开始时：当前可见结果列表（发现 Results 或歌单 DetailOnlineTracks）成为临时队列
- 有 ≥2 首时：`CanGoPrevious` / `CanGoNext` 为 true（循环策略与本地 `RepeatMode` 对齐，默认列表循环）
- 切歌：解析下一首预览 URL，仍走现有 `PreviewAsync` / 底栏在线路径，不把在线曲当成本地 `Track`
- 用户点本地曲库播放：清除临时队列，恢复本地上一首/下一首
- NAS 播放不受影响

- [ ] **Step 1:** 写失败测试（单曲列表不能切；≥2 可切；切到本地后队列清空）
- [ ] **Step 2:** 最小纯函数实现到测试通过
- [ ] **Step 3:** 在 `PlayerBarViewModel` 接线；发现/歌单双击预览时传入列表
- [ ] **Step 4:** `dotnet test -c Release`；`.\scripts\publish-with-api.ps1 -Restart`
- [ ] **Step 5:** 手工：发现搜到 ≥2 首，双击第一首，点下一首应出声且不跳页

反模式：不要为在线曲伪造本地文件；不要把下载队列当播放队列。

---

## 波次 3：朋友路径文案摩擦（可选，波次 2 之后）

**用户可见：** 空首页次要按钮不再把「下载中心」和「去发现」同等强调；发现搜不到时，引导去配置在线源以便试听，而不是去下载。

### 详细改动

| # | 文件 | 现在 | 改为 |
|---|------|------|------|
| 1 | `HomePageView.axaml` ~L223 | 空态次要区**始终**显示「下载中心」 | 默认隐藏；或仅在已有下载任务时显示；或挪到设置旁更弱 |
| 2 | 发现空态 / API 未配置 banner | 可能仍提下载 | 主句：配置在线源后即可搜索试听；下载第二句 |
| 3 | 不改向导 | 本地步已提示稍后可加 NAS | 不加第四步连 NAS |

**不改：** `HomeEmptyCta` 主按钮分流逻辑（已对齐 §7.3）；顶栏下载中心。

### Task 5: 空首页与失败空态去下载器味

**Files:**
- Modify: `HomePageView.axaml` — 评估「下载中心」次要按钮是否改为「库来源」或降为更弱入口（规范 §7.3 允许保留下载次要；若保留则不要和双主 CTA 同等视觉）
- Modify: 发现空态 / API 未配置 CTA：强调「去设置配置在线源以便搜索试听」，下载当第二句
- Do not: 向导增加 NAS 连接步（冻结新协议；本地步文案已提示稍后可加 NAS）

- [ ] **Step 1:** 只改文案与次要按钮可见性/class
- [ ] **Step 2:** `HomeEmptyCtaTests` 仍通过

---

## 波次 4：验证与工程诚实（与 A 并行，不挡波次 1）

**用户可见：** 几乎无新 UI。价值是发朋友前更敢说「能听」。

### 详细改动

| # | 项 | 内容 |
|---|----|------|
| 1 | 测试 | `OnlinePreviewQueueTests`（随波次 2）；导入后在线列表非空（`OnlinePlaylistTracksService` 已有则补断言） |
| 2 | `.gitattributes` | 增加 `*.cs text eol=crlf` |
| 3 | 本波改过的 `.cs` | 存 CRLF；**禁止**一次改全仓行尾 |
| 4 | Mac 实机清单 | 启动、dylib 出声、发现试听、有 API 则下载可用、无 API 则禁用+文案。无真机则只记「未验」，不挡 Windows 包 |

**不改：** 全仓 `dotnet format`、设置 IA、拆 Search VM。

### Task 6: 测试缺口

**Files:**
- Test: 平台导入持久化若仍无以 VM 为名的测试，补 `OnlinePlaylistTracksService` 已有测试之上的「导入后列表非空」断言（避免只测 AddMany）
- Test: 预览队列见 Task 4

- [ ] **Step 1:** 不写 GUI 自动化；纯逻辑 + 现有 Headless
- [ ] **Step 2:** `dotnet test MusicVault.sln -c Release`

### Task 7: 换行门禁

**Files:**
- Modify: `.gitattributes` 增加 `*.cs text eol=crlf`（与 `.editorconfig` 一致）
- Do not: 一次重写全仓库行尾（巨大 diff）；新文件与本波改动的文件跟 CRLF

- [ ] **Step 1:** 加 gitattributes
- [ ] **Step 2:** 本波改过的 `.cs` 存成 CRLF
- [ ] **Step 3:** 记录：全量 `dotnet format --verify-no-changes` 仍可能因历史 LF 失败，不把「全仓 format」当本波完成条件

### Task 8: Mac 实机（有机器才做）

验收清单（写入 `docs/GITHUB_RELEASE.md` 或测试笔记，不挡 Windows 波次 1）：

- [ ] osx-arm64 zip 含 `libMusicVaultPlayer.dylib`
- [ ] 启动；本地文件出声
- [ ] 发现能搜并能在线听
- [ ] 有统一 API 时下载入口可用；无 API 时禁用 + 「本机以在线播放为准」
- [ ] Intel Mac / 无 dylib 不得当朋友资产

---

## 验证（每波结束后）

1. `dotnet test MusicVault.sln -c Release`
2. 有 UI 改动：`.\scripts\publish-with-api.ps1 -Restart`
3. 设计系统：`.\scripts\audit-design-system.ps1 -FailOnViolation`
4. 文案：关于页、README、发现页职责与 PRODUCT §1/§4 一致
5. 冻结检查：未改设置导航、未加平台、未拆 Search 专项

---

## 建议施工顺序

```
波次 0（文档/口号） → 波次 1（按钮降权，当天可验）
                 ↘ 波次 2（预览队列，核 B 体感）
波次 4 换行与测试可随时插
Mac 实机不挡 Windows 朋友包
波次 3 可并入波次 0 若时间够
```
