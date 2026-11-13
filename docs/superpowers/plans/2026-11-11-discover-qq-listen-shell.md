# Discover QQ listen shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Discover「推荐|搜索」分段 with a QQ-like dark listen shell: always-on thin search bar; no active session = recommend feed (Banner + quick entries + playlists + charts); active session = results; Clear returns to feed.

**Architecture:** Evolve `DiscoverBrowseHubState` from shell-index to **session-driven** visibility (`hasActiveSearchSession`). Keep one Compact search chrome always visible. Reskin browse hub into Banner + quick entry row + larger rails. Gate failure/login alerts to results state only.

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, existing browse hub data, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-11-discover-qq-listen-shell-design.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

**Publish:** After UI milestones, `.\scripts\publish-with-api.ps1 -Restart`. Tests use `-r win-x64`.

---

## File map

| File | Role |
|------|------|
| Modify `src/MusicVault.App/Services/DiscoverBrowseHubState.cs` | Session-driven ShowBrowseHub / UseCompactSearch / failure gates |
| Modify `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs` | New pure rules |
| Modify `SearchPageViewModel.cs` | Drop shell segment as UX; bind hub to session; always compact search; quick entries; CTA chip |
| Modify `SearchPageView.axaml` | Remove segment; always search bar; Banner + quick row + feed; results when session |
| Modify `SearchPageView.axaml.cs` | Focus always Compact box |
| Modify `DiscoverLocalizationResources.cs` | Banner / quick entry strings |
| Modify `MainWindowHeadlessSmokeTests.cs` | Session toggles feed↔results (no shell index) |
| Modify `docs/PRODUCT_DESIGN.md` §7.4 | QQ shell wording |

---

### Task 1: Session-driven visibility helper (TDD)

**Files:**
- Modify: `src/MusicVault.App/Services/DiscoverBrowseHubState.cs`
- Modify: `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs`

- [ ] **Step 1: Replace tests**

```csharp
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverBrowseHubStateTests
{
    [Theory]
    [InlineData(false, true)]  // no session → recommend feed
    [InlineData(true, false)] // session → hide feed
    public void ShowBrowseHub_follows_inactive_session(bool hasActiveSearchSession, bool expected) =>
        Assert.Equal(expected, DiscoverBrowseHubState.ShowBrowseHub(hasActiveSearchSession));

    [Fact]
    public void UseCompactSearch_always_true()
    {
        Assert.True(DiscoverBrowseHubState.UseCompactSearch(hasActiveSearchSession: false));
        Assert.True(DiscoverBrowseHubState.UseCompactSearch(hasActiveSearchSession: true));
    }

    [Fact]
    public void UseHeroSearch_always_false()
    {
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: true));
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: false));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShowUnifiedSourceFailure_only_during_search_session(
        bool hasActiveSearchSession, bool bannerWouldShow, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowUnifiedSourceFailure(hasActiveSearchSession, bannerWouldShow));

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void ShowRecommendHealthCue_only_on_feed_when_unhealthy(
        bool hasActiveSearchSession, bool apiHealthBad, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowRecommendHealthCue(hasActiveSearchSession, apiHealthBad));
}
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests" -r win-x64
```

- [ ] **Step 3: Implement helper**

```csharp
namespace MusicVault.App.Services;

/// <summary>
/// Discover dual-state visibility (spec 2026-11-11 QQ listen shell):
/// inactive session = recommend feed; active session = results. Thin search always on.
/// </summary>
public static class DiscoverBrowseHubState
{
    public static bool ShowBrowseHub(bool hasActiveSearchSession) =>
        !hasActiveSearchSession;

    public static bool UseCompactSearch(bool hasActiveSearchSession) => true;

    public static bool UseHeroSearch(bool showBrowseHub) => false;

    public static bool ShowUnifiedSourceFailure(bool hasActiveSearchSession, bool bannerWouldShow) =>
        hasActiveSearchSession && bannerWouldShow;

    public static bool ShowRecommendHealthCue(bool hasActiveSearchSession, bool apiHealthBad) =>
        !hasActiveSearchSession && apiHealthBad;
}
```

Remove `ShellRecommend` / `ShellSearch` constants (update all call sites in Task 2).

- [ ] **Step 4: Run — expect PASS** (if App won't compile, apply Task 2 minimal call-site fixes in same PR of work).

- [ ] **Step 5: Commit only if user asked**

---

### Task 2: ViewModel — session shell, drop segment UX

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Modify: `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs`

- [ ] **Step 1: Localization**

Add zh/en:

```csharp
["Discover.Banner.Title"] = "今日推荐",
["Discover.Banner.Subtitle"] = "精选歌单与榜单，点封面就能听",
["Discover.Quick.Charts"] = "榜单",
["Discover.Quick.Playlists"] = "歌单",
["Discover.Quick.Recognize"] = "识曲",
["Discover.Quick.Parse"] = "粘贴链接",
```

English: `For you today` / `Featured playlists and charts` / `Charts` / `Playlists` / `Identify` / `Paste link`.

- [ ] **Step 2: Retarget visibility to session**

Replace shell-index driven properties:

```csharp
public bool ShowBrowseHub =>
    DiscoverBrowseHubState.ShowBrowseHub(HasActiveSearchSession);

public bool UseHeroSearch => DiscoverBrowseHubState.UseHeroSearch(ShowBrowseHub);

public bool UseCompactSearch =>
    DiscoverBrowseHubState.UseCompactSearch(HasActiveSearchSession);

public bool ShowSearchResultsPane => HasActiveSearchSession || IsParseMode || IsRecognizeMode
    ? !ShowBrowseHub
    : false;
// Prefer simpler: ShowSearchResultsPane => !ShowBrowseHub;
```

```csharp
public bool ShowUnifiedSourceFailureBanner =>
    DiscoverBrowseHubState.ShowUnifiedSourceFailure(
        HasActiveSearchSession,
        UnifiedSourceFailure.Show);

public bool ShowRecommendHealthCue =>
    DiscoverBrowseHubState.ShowRecommendHealthCue(
        HasActiveSearchSession,
        ShowApiHealthBanner);

public bool ShowDiscoverAlert =>
    HasActiveSearchSession && !string.IsNullOrWhiteSpace(DiscoverAlertMessage);

public bool ShowDiscoverAdvanced =>
    !ShowBrowseHub && (IsDiscoverAdvancedOpen || !IsSearchMode);
```

- [ ] **Step 3: Deprecate DiscoverShellIndex as UX**

- Remove XAML bindings in Task 3.
- Keep property temporarily OR remove and fix FocusSearch CTA:

```csharp
case DiscoverPrimaryCtaKind.FocusSearch:
    FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    break;
```

Do **not** set `DiscoverShellIndex`. ClearSearch already sets `HasActiveSearchSession = false` → feed returns.

- Delete `_discoverShellSegmentLabels` / shell tooltip refresh if unused.
- Ensure `NotifyDiscoverBrowseSessionUi` fires on `HasActiveSearchSession` (already via partial).

- [ ] **Step 4: Quick entry commands**

```csharp
[RelayCommand] private void QuickOpenCharts() =>
    EnsureBrowseFeaturedRequested?.Invoke(this, EventArgs.Empty); // scroll handled in view

[RelayCommand] private void QuickOpenPlaylists() =>
    EnsureBrowseFeaturedRequested?.Invoke(this, EventArgs.Empty);

[RelayCommand]
private void QuickRecognize()
{
    IsDiscoverAdvancedOpen = true;
    DiscoverModeIndex = 2;
    HasActiveSearchSession = true; // show results chrome for recognize UI
}

[RelayCommand]
private void QuickParse()
{
    IsDiscoverAdvancedOpen = true;
    DiscoverModeIndex = 1;
    HasActiveSearchSession = true;
}
```

Expose label properties from l10n. Banner title/subtitle properties. Banner click: if first playlist card exists, `OpenBrowsePlaylist`; else no-op.

- [ ] **Step 5: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64
```

---

### Task 3: XAML — always search + feed chrome

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml`
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml.cs`

- [ ] **Step 1: Remove shell `IntSegmentedBar` DockPanel** (推荐|搜索).

- [ ] **Step 2: Grid rows**

`RowDefinitions="Auto,Auto,*"`:

- Row 0: `discover-search-band` **always visible** (not `IsSearchShell`). Only Compact search shell (`UseCompactSearch` always true). Delete or leave Hero collapsed.
- Inside band: Compact + advanced (`ShowDiscoverAdvanced`) + failure banners (`ShowUnified…` / `ShowDiscoverAlert`).
- Optional health cue TextBlock near search trailing edge when `ShowRecommendHealthCue`.
- Row 1: divider when `ShowSearchResultsPane`.
- Row 2: `BrowseHubScroll` when `ShowBrowseHub`; results `DockPanel` when `ShowSearchResultsPane`.

- [ ] **Step 3: Inside BrowseHubScroll, order**

1. Optional Banner `Border` (title/subtitle bindings; click command if added).
2. Quick entry `StackPanel` Orientation=Horizontal: 4 buttons → Quick* commands.
3. Primary CTA as small chip (existing button, restyle Classes to flat/chip if tokens allow — keep `primary` if no chip class).
4. Existing Charts + Playlists sections (larger section title Classes already `discover-browse-section-title`).

- [ ] **Step 4: Focus helper**

`OnFocusSearchRequested` → always `SongNameQueryBox.Focus()`.

- [ ] **Step 5: Build Release win-x64**

---

### Task 4: Smoke tests + PRODUCT_DESIGN

**Files:**
- Modify: `tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs`
- Modify: `docs/PRODUCT_DESIGN.md`

- [ ] **Step 1: Replace AssertDiscoverBrowseHubSessionToggles**

```csharp
private static void AssertDiscoverBrowseHubSessionToggles(SearchPageViewModel search)
{
    Assert.False(search.HasActiveSearchSession);
    Assert.True(search.ShowBrowseHub);
    Assert.True(search.UseCompactSearch);
    Assert.False(search.UseHeroSearch);
    Assert.False(search.ShowSearchResultsPane);

    search.HasActiveSearchSession = true;
    Assert.False(search.ShowBrowseHub);
    Assert.True(search.UseCompactSearch);
    Assert.True(search.ShowSearchResultsPane);

    search.ClearSearchCriteriaCommand.Execute(null);
    Assert.False(search.HasActiveSearchSession);
    Assert.True(search.ShowBrowseHub);
    Assert.False(search.ShowSearchResultsPane);
}
```

- [ ] **Step 2: §7.4 listen paragraph**

```markdown
**听：** 发现页为深色乐馆壳：常驻细搜条；无搜索会话时展示 Banner / 快捷入口 / 歌单与榜单；提交搜索后同一细条下出结果。识曲 / 粘贴在「更多」或快捷入口。均 → 同一底栏在线播放。……统一源失败条以搜索结果态为主。
```

- [ ] **Step 3: Run tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~Discover" -r win-x64
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~MainWindowHeadlessSmokeTests" -r win-x64
```

---

### Task 5: Publish smoke

- [ ] **Step 1:** `.\scripts\publish-with-api.ps1 -Restart`
- [ ] **Step 2: Manual**
  1. Discover opens on feed with thin search, no 推荐|搜索 segment.
  2. Search → results; Clear → feed.
  3. Quick 识曲 opens more/recognize.
  4. No yellow login banner on feed.

---

## Self-review vs spec

| Spec | Task |
|------|------|
| Cancel segment; thin search always | T2–T3 |
| Session feed↔results | T1–T2 |
| Banner + quick + rails | T3 (+ T2 labels/commands) |
| Failure only on results | T1–T2 |
| Clear → feed | existing Clear + T1 |
| PRODUCT_DESIGN | T4 |
| publish | T5 |

No TBD. Banner may hide when no cards — implement `ShowDiscoverBanner => ShowBrowseHub && (HasBrowsePlaylistCards || HasBrowseChartCards)`.
