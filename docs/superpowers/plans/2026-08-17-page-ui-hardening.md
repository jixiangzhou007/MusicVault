# 桌面播放器 UI 统一 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 先定共享页模板和 Win/Mac 窗框，再把曲库/发现/整理/下载套进同一套播放器 chrome，而不是逐页改按钮。

**Architecture:** Token + `DESIGN_SYSTEM` 合同先行；窗口铬只在 `MainWindow` 按 OS 分支；各页只改布局 class、`IsVisible`、溢出 `MenuFlyout`。不拆 ViewModel，不换图标字体，不换色盘。

**Tech Stack:** Avalonia 11（`ExtendClientAreaToDecorationsHint` / `ExtendClientAreaChromeHints`）、现有 `primary` / `flat-btn` / `app-compact-menu-item`、Headless 冒烟。

**Spec:** [2026-08-17-page-ui-hardening.md](../specs/2026-08-17-page-ui-hardening.md)

**Freeze:** 识曲重写、设置导航大改、拆巨型 VM、换 Fluent 图标字体、换主题。

**Commits:** 仅当用户明确要求时提交。

---

## 文件地图

| 文件 | 职责 |
|------|------|
| `docs/DESIGN_SYSTEM.md` | 新增「页模板」+「平台窗框」两节（合同） |
| `src/MusicVault.App/Styles/Tokens/Spacing.axaml` | `TitleBarLeadingInset`（Win 0 / Mac 默认 78，运行时写入） |
| `src/MusicVault.App/Views/MainWindow.axaml` | 顶栏左侧绑定 inset；Mac 不显示 `WindowChromePanel` |
| `src/MusicVault.App/Views/MainWindow.axaml.cs` | `ConfigureCustomTitleBar` 拆成 Win / Mac |
| `src/MusicVault.App/Styles/ThemeResources.axaml` | Mac 下 `WindowShellCornerRadius` / stroke 可运行时改 |
| `src/MusicVault.App/Views/Pages/ArtistsPageView.axaml` | T2：标题左对齐 |
| `src/MusicVault.App/Views/Pages/AlbumsPageView.axaml` | 同上 |
| `src/MusicVault.App/Views/Pages/PlaylistsPageView.axaml` | T2：1 primary + 新建 flat + `···` |
| `src/MusicVault.App/Views/Pages/SearchPageView.axaml` | T3：默认列宽收起 |
| `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml` | T4：未载入隐藏刮削 |
| `src/MusicVault.App/Views/Pages/DownloadsPageView.axaml` | T4：短页头（若需） |
| `src/MusicVault.App/Localization/*.cs` | 短说明，去「下载补全 / 台账」 |
| `tests/MusicVault.Tests/App/PageChromeHeadlessTests.cs` | 模板合同的可自动部分 |
| `docs/GITHUB_RELEASE.md` | Mac 实机：交通灯不挡导航 |

溢出菜单模式抄：`TracksPageView.axaml` 左栏 `icon-toolbar-btn` + `MenuFlyout` + `app-compact-menu-item`。

---

## 波次总览（先合同，再窗框，再套页）

| 波次 | 用户能看见 | 为什么先做 |
|------|------------|------------|
| **0 合同** | 文档里写死 4 种模板和工具栏规则 | 后面改页有同一把尺，避免又做成五套页头 |
| **1 窗框** | Win 观感基本不变；Mac 顶栏与交通灯合一 | 平台语言只允许出现在这里 |
| **2 曲库族** | 歌曲/艺术家/专辑/歌单像同一个产品 | 朋友待得最久的面 |
| **3 听与任务** | 发现全宽搜；下载短页头 | T3/T4 |
| **4 整理** | 先载入再刮削 | T4 工具 |
| **验** | 测试 + Win 发布；Mac 清单 | |

旧计划的「降 primary / 藏刮削 / 改副标题」全部吸收进 2–4，不再单独当目标。

---

## 波次 0：写入设计合同

**Files:**
- Modify: `docs/DESIGN_SYSTEM.md`（文末或「组件 Class」之前新增两节，不要改 Token 色值）

- [x] **Step 1: 增加「页模板」**

写明 T1–T4、工具栏合同（1 primary + 可选 1 flat + `···`）、`page-title` 左对齐、说明最多一行。点名参照歌曲页 T1。禁止第五种页头。

- [x] **Step 2: 增加「平台窗框」**

Win：自绘标题栏、窗控在右、`WindowShell` 描边可保留。  
Mac：`ExtendClientAreaToDecorationsHint`、交通灯在左、`TitleBarLeadingInset`、禁止 `WindowChromePanel`、禁止假描边框。  
共享：导航、底栏、Token、Fluent 图标。本波不换图标字体。

- [x] **Step 3: 不要改任何 View**

合同没写进 DESIGN_SYSTEM 之前不开始改页。

---

## 波次 1：Win / Mac 窗口铬

**用户可见：** Windows 几乎不变。Mac 不再「系统标题栏一条 + 应用导航一条」；交通灯旁边就是首页/曲库。

**Files:**
- Modify: `src/MusicVault.App/Styles/Tokens/Spacing.axaml` — 增加

```xml
<Thickness x:Key="TitleBarLeadingInset">0</Thickness>
```

- Modify: `src/MusicVault.App/Views/MainWindow.axaml` — 顶栏左侧 `StackPanel` 的 `Margin` 改为与 `TitleBarLeadingInset` 合并（Logo 那一列），例如外层 `Margin="{DynamicResource TitleBarLeadingInset}"` 只加在 Column 0 的容器上，**不要**加在整行（否则右侧窗控也被推走）。

- Modify: `src/MusicVault.App/Views/MainWindow.axaml.cs` 的 `ConfigureCustomTitleBar`：

```csharp
private void ConfigureCustomTitleBar()
{
    if (OperatingSystem.IsWindows())
    {
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowChromePanel.IsVisible = true;
        WindowChromeDivider.IsVisible = true;
        UpdateMaximizeChrome();
        UpdateWindowShellChrome();
        return;
    }

    if (!OperatingSystem.IsMacOS())
        return;

    ExtendClientAreaToDecorationsHint = true;
    ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
    ExtendClientAreaTitleBarHeightHint = 52; // 与 TopNavHeight Token 一致；若无法在 code-behind 读 Resource，用常量并在注释写明对 Token
    WindowChromePanel.IsVisible = false;
    WindowChromeDivider.IsVisible = false;
    Application.Current?.Resources["TitleBarLeadingInset"] = new Thickness(78, 0, 0, 0);
    Application.Current?.Resources["WindowShellCornerRadius"] = new CornerRadius(0);
    Application.Current?.Resources["WindowShellStrokePadding"] = new Thickness(0);
}
```

若 `WindowShellStrokePadding` 键名不同，先在 `ThemeResources.axaml` 核对现有 `WindowShellStrokePadding` / `WindowShellInnerCornerRadius` 再写，禁止造不存在的键。

- [x] **Step 1: 加 Token，Win 路径行为与现在一致（回归：右侧仍有最小/最大/关闭）**
- [x] **Step 2: Mac 分支如上；Win 禁止走 ExtendClientArea（会和现有自绘窗控打架）**
- [x] **Step 3: 在 `docs/GITHUB_RELEASE.md` Mac 实机清单加三条：** 交通灯不挡「首页」；无右侧假窗控；最大化后面板仍可点。

Anti-pattern：给 Mac 画三个圆点；给 Win 留 78px 空白。

---

## 波次 2：曲库族套 T1/T2

**用户可见：** 艺术家/专辑/歌单和歌曲像同一个曲库，而不是三个后台。

### Task 2.1 艺术家 / 专辑 → T2

**Files:** `ArtistsPageView.axaml`、`AlbumsPageView.axaml`、`LibraryLocalizationResources.cs`

- [x] 两页 `page-title` 去掉 `HorizontalAlignment="Right"`（改为默认左）。
- [x] 缩短 `Library.Artists.PageHeader` / `Library.Albums.PageHeader` 为一行，去掉「下载补全」。

zh 艺术家：`按拼音首字母浏览。点进歌手可看专辑和曲目。`  
zh 专辑：`按拼音首字母浏览。点进专辑即可播放。`  
en 同步缩短。

筛选行「搜索」保持 `primary discover-action`（该行提交）。重置/数据治理/刷新元数据保持 `flat-btn`；若一行超过「1 primary + 2 次要」，把数据治理/刷新放进 `···` Flyout。

### Task 2.2 歌单 → T2 + 溢出

**Files:** `PlaylistsPageView.axaml`、`LocalizationResources.cs`（`Playlists.Browse.Subtitle.All`）

浏览工具栏：

- `ImportPlatformButton`：`primary btn-toolbar`
- `CreatePlaylistButton`：`flat-btn btn-toolbar`（可见，朋友要新建）
- `MergePlaylistsButton`：移入 `MenuFlyout`（`icon-toolbar-btn` + `app-compact-menu-item`），不要第三颗可见主/次按钮

详情工具栏：

- 保持 `primary`：`PreviewChartPlaylistButton` / `PreviewOnlinePlaylistButton`（可见时互斥，仍算一个主操作）
- 移入 `···`：编辑、删除、导出 M3U、加载更多、重试失败
- `BatchDownloadButton` 已是 `flat-btn`：可留在溢出里（下载不是听的主路径）或保留为可见 flat；**优先进溢出**，避免详情条又变按钮墙
- 试听记录：移除/清缓存/加载更多全部进该页 `···`，不要三颗 primary

副标题 zh：`粘贴链接导入即可听；也可新建歌单。`  
en：`Paste a link to import and listen, or create a playlist.`

空态「重试榜单」可保持单个 `primary`。M3U 导入面板可见时，导入钮可以是该面板唯一 `primary`。

抄 Flyout 结构（歌曲页已有）：

```xml
<Button Classes="icon-toolbar-btn" ToolTip.Tip="{Binding MoreActionsToolTip}">
  <Button.Flyout>
    <MenuFlyout>
      <MenuItem Header="{Binding MergePlaylistsButton}" Classes="app-compact-menu-item"
                Command="{Binding MergeSelectedPlaylistsCommand}"
                IsEnabled="{Binding CanMergeSelectedPlaylists}"/>
    </MenuFlyout>
  </Button.Flyout>
  <TextBlock Text="&#xE712;" Classes="icon-xs"/>
</Button>
```

若无 `MoreActionsToolTip` 键，加 `Common.MoreActions` zh=`更多` en=`More`，两平台共用。

---

## 波次 3：发现 T3 + 下载 T4

### Task 3.1 发现

**Files:** `SearchPageView.axaml` 第 17 行

```xml
<Grid x:Name="RootGrid" ColumnDefinitions="*,0,0"
```

不要改 `IsTransfersPaneOpen` 默认 false，不要改入队 `RevealTransfersPane`。识曲仍在「更多」。

### Task 3.2 下载任务

**Files:** `TransfersLocalizationResources.cs` `Transfers.PageSubtitle`

zh：`队列、失败与历史。下完的歌在曲库里听。`  
en：`Queue, retries, and history. Play finished tracks in the library.`

不改 Hero 结构。批量操作已在更多菜单则保持。

---

## 波次 4：整理 T4

**Files:** `AudioToolsPageView.axaml`

- 数据源/刮削/范围那一行 `StackPanel.organize-toolbar-row`：`IsVisible="{Binding HasWorkingSet}"`
- `Grid.organize-form-row`（十个勾选）：`IsVisible="{Binding HasWorkingSet}"`
- 第一行选库 + 载入保持；载入保持 `primary`
- 不要改 `ShowStandardPageHeader`（壳层副标题已在说「先载入」）
- 去重复核条 `RemoveDupIndexLabel` 仅在复核态显示，可保留该态唯一 `primary`

---

## 验证

**Files:** Create `tests/MusicVault.Tests/App/PageChromeHeadlessTests.cs`  
`[Collection("HeadlessUiSerial")]`，**一个** `AvaloniaFact`。

```csharp
await using var host = await HeadlessUiTestHost.CreateAsync();
var shell = host.Shell;
var l10n = host.Localization;

shell.NavigateCommand.Execute(NavigationSection.Search);
Assert.False(shell.Search.IsTransfersPaneOpen);

shell.NavigateCommand.Execute(NavigationSection.Playlists);
var import = l10n.Get("Playlists.ImportPlatform");
var create = l10n.Get("Nav.NewPlaylist");
var merge = l10n.Get("Playlists.MergePlaylists");
var primaries = host.Window.GetVisualDescendants().OfType<Button>()
    .Where(b => b.Classes.Contains("primary") && b.Classes.Contains("btn-toolbar") && b.IsVisible)
    .Select(b => b.Content?.ToString())
    .ToList();
Assert.Contains(import, primaries);
Assert.DoesNotContain(create, primaries);
Assert.DoesNotContain(merge, primaries);

shell.NavigateCommand.Execute(NavigationSection.AudioTools);
Assert.False(shell.AudioTools.HasWorkingSet);
```

Win 窗控：现有 `MainWindowHeadlessSmokeTests` 已覆盖顶栏；本波 Win `ConfigureCustomTitleBar` 不得把 `WindowChromePanel` 藏掉。

跑测试：

```powershell
$env:NUGET_PACKAGES = "C:\Users\54560\.nuget\packages"
dotnet test MusicVault.sln -c Release
```

发布：

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

审计：`.\scripts\audit-design-system.ps1`（或仓库现行等价命令），View hex 仍为 0。

Mac：无本机则只完成代码 + 清单；不挡 Win 交付。

---

## 自检

| Spec | 波次 |
|------|------|
| 4 种模板写进 DESIGN_SYSTEM | 0 |
| Win 右窗控 / Mac 交通灯 inset | 1 |
| 曲库族统一 T1/T2 | 2 |
| 发现全宽、下载短句 | 3 |
| 整理先载入 | 4 |
| 不换色、不换图标字体、不改设置 IA | 全程 |
