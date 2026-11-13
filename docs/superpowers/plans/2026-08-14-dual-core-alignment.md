# 双核对齐 · 下一步工作计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把现行 1.0.11 对齐到 [PRODUCT_DESIGN.md](../../PRODUCT_DESIGN.md)：本地听与管 + 跨平台在线听/导歌单；下载尽力而为；朋友解压能走完第一次成功。

> **2026-08-17：** 批次 1–3 已落地。后续施工见 [2026-08-17-dual-core-experience-hardening.md](2026-08-17-dual-core-experience-hardening.md)。

**Architecture:** 不重做搜索内核、整理流水线、NAS。只改到达路径（向导/空首页）、平台歌单「导入即可听」、文档与 Mac 合格线。新状态用 `AppSettings.FirstRunOnboardingPath`；在线歌单曲目已有 `OnlinePlaylistTracksService`（`settings.json` 的 `PlaylistOnlineTracks`），导入流程补上写入即可。

**Tech Stack:** .NET 8、Avalonia 11、CommunityToolkit.Mvvm、现有 xUnit / Headless 冒烟。

**Spec:** `docs/PRODUCT_DESIGN.md` 第 1–7、10、12、14 节；切片 `docs/superpowers/specs/2026-08-14-friend-ready-public-product-design.md`

---

## 差距审计（代码 / 文档 vs 理念）

对照 2026-08-14 设计意图，**不要重做已经对齐的部分**。

| 理念要求 | 现状 | 差距 |
|----------|------|------|
| A 本地播、标签、整理、歌词 | 完整 | 无（不进本计划） |
| NAS / WebDAV 当本地盘 | 功能完整，入口在库来源 | 空态有次要「添加库来源」；第一批不改顶栏 |
| B 发现里搜到并在线听 | `OnlineTrackViewModel.PreviewAsync` + 底栏已通 | 无（不重做搜索） |
| 导入平台歌单后**在线听** | 解析有；详情主按钮是「批量下载」；导入后 `LoadSelectedPlaylistAsync` **清空** `PendingOnlineTracks` 且**未**写入 `OnlinePlaylistTracksService` → 列表容易空 | **P0 缺口** |
| 下载不合规、尽力 | 传输页与批量下载仍是一等公民 | 文案 + 无 API 时禁用；Mac 能下则保留 |
| Mac 只保证在线听 | 设置里已禁内置 API；歌单/发现下载钮未按平台降权 | 批次 3 |
| 向导：已有歌 / 在线听 | 仍是 4 步：欢迎→库→下载目录→API 开关 | **P0 缺口** |
| 空首页跟随路径 | 主按钮固定「去发现」 | **P0 缺口** |
| 朋友包 win-x64 + osx-arm64 | Release 工作流只打 Windows | 批次 2 |
| 文档与理念一致 | README 仍把「搜索与下载」当 P0；`GITHUB_RELEASE.md` 仍写 macOS 播放器占位；`CONTRIBUTING.md` Issues 为 `your-org`；`menu-ia-v1` 过期 | 随批次 1 改对外文档 |

**明确不做（本计划冻结）：** 新平台源、识曲交互重写、设置 7 项改 4 项、Mac 捆绑 darwin API、拆 Search/Settings 巨型 VM、iOS / x86。

---

## 文件地图（批次 1）

| 文件 | 职责 |
|------|------|
| `src/MusicVault.App/Services/AppSettingsService.cs` | 新增 `FirstRunOnboardingPath` |
| `src/MusicVault.App/ViewModels/Dialogs/FirstRunOnboardingPath.cs` | 枚举 `Unset / LocalLibrary / Discover` |
| `src/MusicVault.App/ViewModels/Dialogs/FirstRunWizardModalViewModel.cs` | 3 步：欢迎 → 选路 → 路径详情 |
| `src/MusicVault.App/Views/Overlays/FirstRunWizardModalView.axaml` | 选路卡片；去掉独立「下载目录」步 |
| `src/MusicVault.App/ViewModels/Pages/HomePageViewModel.cs` + `HomePageView.axaml` | 空首页主 CTA 随路径 |
| `src/MusicVault.App/Services/OnlinePlaylistTracksService.cs` | `AddManyAsync` |
| `src/MusicVault.App/ViewModels/Pages/PlaylistsPageViewModel.cs` + `PlaylistsPageView.axaml` | 导入后持久化在线曲；主按钮「播放」 |
| `src/MusicVault.App/Localization/*` | 向导 / 空首页 / 歌单文案 zh-CN+en-US |
| `tests/MusicVault.Tests/FirstRunWizardDefaultsTests.cs`（扩） | 路径枚举与默认 |
| `tests/MusicVault.Tests/App/HomeEmptyCtaTests.cs` | 纯函数测主 CTA |
| `tests/MusicVault.Tests/App/OnlinePlaylistImportPersistTests.cs` | 导入应写入在线曲目表 |
| `README.md` / `docs/GITHUB_RELEASE.md` / `CONTRIBUTING.md` | 对外叙事 |

---

## 批次顺序

```
批次 1（下一步立刻做，Windows 可验）
  1a 向导双路径 + 设置字段
  1b 空首页主按钮
  1c 平台歌单导入后可听（持久化 + 播放主按钮）
  1d 对外文档

批次 2（要 Mac / macos-latest）
  osx-arm64 zip + Release 第二资产；dylib 清单

批次 3
  发现/歌单/传输：统一 API 不可用才禁用下载；Mac 能下则保留
  向导 Discover Mac 文案：能听能导；下载能用就保留、不作保证
```

打完批次 1 就可以把 **Windows 包**发给朋友验证：加文件夹能播、发现能听、贴歌单链接能听。

---

### Task 1: `FirstRunOnboardingPath` 设置字段

**Files:**
- Create: `src/MusicVault.App/ViewModels/Dialogs/FirstRunOnboardingPath.cs`
- Modify: `src/MusicVault.App/Services/AppSettingsService.cs`（`AppSettings` 类，约 L225 `FirstRunWizardCompleted` 旁）
- Modify: `tests/MusicVault.Tests/FirstRunWizardDefaultsTests.cs`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void AppSettings_onboarding_path_defaults_unset()
{
    var settings = new AppSettings();
    Assert.Equal(FirstRunOnboardingPath.Unset, settings.FirstRunOnboardingPath);
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/MusicVault.Tests --filter FirstRunWizardDefaultsTests`

- [ ] **Step 3: 最小实现**

```csharp
namespace MusicVault.App.ViewModels.Dialogs;

public enum FirstRunOnboardingPath
{
    Unset = 0,
    LocalLibrary = 1,
    Discover = 2
}
```

在 `AppSettings` 增加：`public FirstRunOnboardingPath FirstRunOnboardingPath { get; set; }`（默认 `Unset`）。JSON 反序列化缺字段必须落到 `Unset`。

- [ ] **Step 4: 测试通过**

Run: `dotnet test tests/MusicVault.Tests --filter FirstRunWizardDefaultsTests`

- [ ] **Step 5: 提交（仅当用户要求 git commit）**

---

### Task 2: 向导改为 3 步双路径

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Dialogs/FirstRunWizardModalViewModel.cs`（现行 `LastStepIndex = 3`，步：欢迎/库/下载/API）
- Modify: `src/MusicVault.App/Views/Overlays/FirstRunWizardModalView.axaml`
- Modify: `src/MusicVault.App/Localization/LocalizationResources.cs`（或向导所用资源文件）增加选路文案
- Test: 扩 `FirstRunWizardDefaultsTests` 或新建 `FirstRunWizardPathTests`，测纯逻辑：未选路径不能进入最后一步；`Finish` 写入对应 `FirstRunOnboardingPath`；跳过整段向导 → `Unset`；下载目录不出现在步骤里，`Finish` 仍写入默认下载路径。

向导状态机（必须）：

```
Step 0 欢迎
Step 1 选路（两卡片，未选禁用「下一步」）
Step 2 LocalLibrary → 选文件夹（可跳过本步）
       Discover     → Windows 有捆绑 API：一句「打开后即可搜索并试听」，静默 AutoStart=true
                    → 否则/Mac：一句「在线搜索与播放（部分平台）」，可选展开 API 地址
Finish: FirstRunWizardCompleted=true；写入路径；默认下载目录静默创建
```

- [ ] 抽出可单测的步进规则（避免 2000 行 VM 里无法测）：例如 `FirstRunWizardFlow` record：`CanGoNext(step, pathSelected)`、`LastStepIndex=2`。
- [ ] AXAML：选路用现有 `AccentSoft` 卡片；**禁止**新圆角体系；不出现 `go-music-api.exe` 字样。
- [ ] 版权对话框顺序不变（`App.axaml.cs` 先版权后向导）。
- [ ] 已有曲库仍跳过向导（现逻辑保留），不改 `FirstRunOnboardingPath`（保持 `Unset`）。
- [ ] `dotnet test` 相关 filter + `dotnet build src/MusicVault.App`

验收：冷启动空库 → 3 步内结束；选「我已有歌曲」只问文件夹；选「在线听 / 找歌」不问下载目录。

---

### Task 3: 空首页主按钮

**Files:**
- Create: `src/MusicVault.App/Services/HomeEmptyCta.cs`（纯函数，便于测）
- Create: `tests/MusicVault.Tests/App/HomeEmptyCtaTests.cs`
- Modify: `HomePageViewModel.cs`、`HomePageView.axaml`
- Modify: 空首页文案键（`Home.Empty.Hint` 现行是「去发现里搜一首歌」——按路径改）

规则（与规范 7.3 一致）：

```csharp
public enum HomeEmptyPrimaryKind { AddLibrary, OpenTracks, Discover, Dual }

public static HomeEmptyPrimaryKind Resolve(
    FirstRunOnboardingPath path, bool hasLibrary) =>
    path switch
    {
        FirstRunOnboardingPath.Discover => HomeEmptyPrimaryKind.Discover,
        FirstRunOnboardingPath.LocalLibrary when hasLibrary => HomeEmptyPrimaryKind.OpenTracks,
        FirstRunOnboardingPath.LocalLibrary => HomeEmptyPrimaryKind.AddLibrary,
        _ when hasLibrary => HomeEmptyPrimaryKind.OpenTracks,
        _ => HomeEmptyPrimaryKind.Dual // Unset 且无库：添加文件夹 + 去发现 对等
    };
```

- [ ] 测试覆盖上表 5 种组合。
- [ ] `HomePageView.axaml`：`Dual` 时两个 `primary` 并排（添加音乐文件夹、去发现）；其余一个主按钮。次要仍保留库来源（NAS）/ 设置。
- [ ] Headless：`MainWindowHeadlessSmokeTests` 仍通过。

---

### Task 4: 平台歌单导入后可听

这是核心 B 与代码最大的断裂。

**根因：** `PlaylistsPageViewModel.ImportPlatformPlaylistAsync`（约 L1498）把解析结果放进 `PendingOnlineTracks`，随即 `LoadSelectedPlaylistAsync`（约 L897）**Clear** 该集合，只从 `OnlinePlaylistTracksService.GetAll` 恢复。导入路径**没有** `AddAsync`，故详情里在线列表经常是空的，工具栏却露出「批量下载」。

**Files:**
- Modify: `src/MusicVault.App/Services/OnlinePlaylistTracksService.cs` — 增加 `AddManyAsync(long playlistId, IReadOnlyList<OnlineTrack> tracks)`（去重、一次 `SaveAsync`、一次 `Changed`）
- Create: `tests/MusicVault.Tests/App/OnlinePlaylistTracksServiceTests.cs`（用临时 settings 或可注入的 `AppSettings`）
- Modify: `PlaylistsPageViewModel.ImportPlatformPlaylistAsync` — 创建歌单后 `AddManyAsync`，再 `LoadSelectedPlaylistAsync`
- Modify: `PlaylistsPageView.axaml` 约 L372–375：`HasPendingOnlineTracks` 时 **主按钮改为播放**（复用/抽出与 `PreviewChartPlaylistCommand` 同类的「从第一首/随机在线预览」）；`BatchDownloadPendingCommand` 改为 `flat-btn`，Mac 上 `IsVisible` 可先仍显示（批次 3 再禁）
- Modify: `Playlists.Browse.Intro.Platform` / `Playlists.Empty.Platform.Desc`：去掉「用于批量下载」主导语，改为「导入后即可在线播放」
- Modify: `PlaylistService.ImportPlatformPlaylistAsync` 注释（L156）：删掉「曲目通过批量下载入库后再添加」

- [ ] 测试：`AddManyAsync` 写入 `PlaylistOnlineTracks`；重复 Id 不加倍。
- [ ] 导入后 `GetAll(pid).Count == tracks.Count`。
- [ ] 双击行仍走现有 `PreviewCommand`（`PlaylistsPageView.axaml.cs` L154 已有）。
- [ ] 不在本任务做整单下载稳定性。

验收：粘贴网易云/QQ 歌单链接 → 详情列出在线曲 → 双击出声。下载按钮降为次要。

---

### Task 5: 对外文档对齐

**Files:**
- `README.md` 功能表：P0 改为本地库/整理/在线听/平台歌单导入；下载改为「尽力」；Mac 合格线写在线听不是下载
- `docs/GITHUB_RELEASE.md`：删「macOS 播放器占位无法出声」；两个资产文件名；Mac 三行右键打开；下载不作保证
- `CONTRIBUTING.md`：Issues 改为 `https://github.com/jixiangzhou007/MusicVault/issues`
- `docs/ARCHITECTURE.md` 向导小节：注明目标 3 步（批次 1 落地后改成现行）
- `docs/LEGAL.md`：保持免责；不新增「鼓励下载」语句
- `docs/execution/menu-ia-v1.md` 文首加过期横幅（已在 PRODUCT_DESIGN 标明）

- [ ] 与 `PRODUCT_DESIGN.md` 第 10、12 节逐条对照，无「跨平台完整对等」、无「下载为 P0」。

---

### Task 6: 回归

Run:

```powershell
dotnet test MusicVault.sln -c Release
dotnet format MusicVault.sln --verify-no-changes
.\scripts\audit-design-system.ps1 -FailOnViolation
.\scripts\smoke-ui-headless.ps1
```

Windows 人工（发给自己先走一遍）：

1. 空库启动 → 向导 3 步，选本地文件夹 → 1 分钟内出声
2. 新配置选「在线听」→ 发现搜一首 → 双击试听
3. 歌单页粘贴平台链接 → 列表有曲 → 双击试听（不点下载）
4. 下载中心仍能用（尽力，不作为本轮失败条件）

---

## 批次 2（Mac 包，不挡批次 1）

- `publish-macos.sh osx-arm64` 产物必须含 `libMusicVaultPlayer.dylib`
- GitHub `release.yml` 增加 `macos-latest` job，上传 `MusicVault-X.Y.Z-osx-arm64.zip`；LibVLC/VLC.app **不是**硬门槛
- Release 说明：Intel Mac / iOS 不支持；无法打开则右键打开
- 无 Mac 产物时仍只发 Windows

## 批次 3（诚实下载）

- 发现行、歌单「批量下载」、传输页：统一 API 不可用时 **禁用 + 文案**；Mac 配了 API **保留下载**。不按操作系统一刀切。Mac 无 API 提示「本机以在线播放为准」。
- 向导 Discover 步 Mac 文案：能听能导；下载能用就保留、不作保证
- NAS / WebDAV 下载属核心 A，不按此门禁
- 不删除 Windows 下载代码

---

## 覆盖检查（对规范）

| 规范条款 | 本计划任务 |
|----------|------------|
| 双核、听必须下尽力 | 全文；Task 4–5 |
| 向导 3 步 | Task 2 |
| 空首页 CTA | Task 3 |
| 平台歌单导入可听 | Task 4 |
| Mac 下载非合格线 | 批次 2–3；批次 1 不挡 Windows |
| 两安装包 | 批次 2 |
| 冻结项 | 「明确不做」 |

---

计划写完后且用户要求开工时：按 Task 1 → 6 顺序改代码。不要并行拆设置 IA 或识曲。
