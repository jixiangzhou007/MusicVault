# Discover online-listen redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Discover an online-listen home with equal **推荐 | 搜索** page-level segments: recommend shows CTA + charts/playlists (no big search); search shows sticky search + results (识曲/粘贴 under More).

**Architecture:** Introduce a session-scoped shell index (`Recommend` / `Search`) orthogonal to existing `DiscoverModeIndex` (search / parse / recognize). Replace browse-hub visibility driven by “no active search session” with shell-driven visibility. Keep Hero search permanently off; Compact search only on Search shell. Gate the unified source-failure banner to Search shell; optional light health cue on Recommend.

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, existing `IntSegmentedBar`, xUnit, `DiscoverBrowseHubState` / `DiscoverPrimaryCta` / `DiscoverSourceFailureBanner`.

**Spec:** `docs/superpowers/specs/2026-11-11-discover-online-listen-redesign.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

**Publish:** After UI wiring milestones that change runtime UX, run `.\scripts\publish-with-api.ps1 -Restart` (close running MusicVault first if needed).

---

## File map

| File | Role |
|------|------|
| Modify `src/MusicVault.App/Services/DiscoverBrowseHubState.cs` | Shell-aware visibility: recommend hub, compact search, failure banner gate; Hero always false |
| Modify `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs` | Match new pure rules |
| Modify `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs` | `DiscoverShellIndex`, labels, notify, CTA→switch shell, banner gate, advanced only on Search |
| Modify `src/MusicVault.App/Views/Pages/SearchPageView.axaml` | Page-top 推荐\|搜索; hide Hero; pane visibility |
| Modify `src/MusicVault.App/Views/Pages/SearchPageView.axaml.cs` | Focus Compact box after shell switch |
| Modify `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs` | Shell segment + empty-search strings (zh/en) |
| Modify `docs/PRODUCT_DESIGN.md` | §7.4 Discover IA: 推荐 \| 搜索 |
| Modify spec status if still pending | Already confirmed |

---

### Task 1: Shell visibility pure helper (TDD)

**Files:**
- Modify: `src/MusicVault.App/Services/DiscoverBrowseHubState.cs`
- Modify: `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs`

- [ ] **Step 1: Rewrite failing tests for shell rules**

Replace `DiscoverBrowseHubStateTests.cs` with:

```csharp
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverBrowseHubStateTests
{
    [Theory]
    [InlineData(0, true)]   // Recommend shell
    [InlineData(1, false)]  // Search shell
    public void ShowBrowseHub_follows_recommend_shell(int shellIndex, bool expected) =>
        Assert.Equal(expected, DiscoverBrowseHubState.ShowBrowseHub(shellIndex));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void UseCompactSearch_follows_search_shell(int shellIndex, bool expected) =>
        Assert.Equal(expected, DiscoverBrowseHubState.UseCompactSearch(shellIndex));

    [Fact]
    public void UseHeroSearch_always_false()
    {
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: true));
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: false));
    }

    [Theory]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(0, true, false)]  // Recommend: never long failure banner
    [InlineData(0, false, false)]
    public void ShowUnifiedSourceFailure_gated_to_search_shell(
        int shellIndex, bool bannerWouldShow, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowUnifiedSourceFailure(shellIndex, bannerWouldShow));

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    public void ShowRecommendHealthCue_only_on_recommend_when_unhealthy(
        int shellIndex, bool apiHealthBad, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowRecommendHealthCue(shellIndex, apiHealthBad));
}
```

- [ ] **Step 2: Run tests — expect FAIL (signatures / missing methods)**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests"
```

Expected: compile errors or assertion failures against old `ShowBrowseHub(bool,bool,bool)`.

- [ ] **Step 3: Implement helper**

Replace `DiscoverBrowseHubState.cs` with:

```csharp
namespace MusicVault.App.Services;

/// <summary>
/// Discover shell visibility (spec 2026-11-11): Recommend | Search page segments.
/// Orthogonal to DiscoverModeIndex (search / parse / recognize).
/// </summary>
public static class DiscoverBrowseHubState
{
    public const int ShellRecommend = 0;
    public const int ShellSearch = 1;

    public static bool ShowBrowseHub(int shellIndex) =>
        shellIndex == ShellRecommend;

    public static bool UseCompactSearch(int shellIndex) =>
        shellIndex == ShellSearch;

    /// <summary>Hero search removed; recommend has no search chrome.</summary>
    public static bool UseHeroSearch(bool showBrowseHub) => false;

    public static bool ShowUnifiedSourceFailure(int shellIndex, bool bannerWouldShow) =>
        shellIndex == ShellSearch && bannerWouldShow;

    public static bool ShowRecommendHealthCue(int shellIndex, bool apiHealthBad) =>
        shellIndex == ShellRecommend && apiHealthBad;
}
```

- [ ] **Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests"
```

Expected: all pass.

- [ ] **Step 5: Commit (only if user asked)**

```powershell
git add src/MusicVault.App/Services/DiscoverBrowseHubState.cs tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs
git commit -m "refactor: Discover shell visibility for Recommend|Search"
```

---

### Task 2: ViewModel shell index + derived UI flags

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Modify: `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs`

- [ ] **Step 1: Add localization keys (zh + en)**

In `DiscoverLocalizationResources.cs` Chinese block, add near other Discover keys:

```csharp
["Discover.Shell.Recommend"] = "推荐",
["Discover.Shell.Search"] = "搜索",
["Discover.Shell.ToolTip"] = "切换推荐与搜索",
["Discover.Empty.SearchHint"] = "输入歌名开始搜索",
```

English block:

```csharp
["Discover.Shell.Recommend"] = "For you",
["Discover.Shell.Search"] = "Search",
["Discover.Shell.ToolTip"] = "Switch For you and Search",
["Discover.Empty.SearchHint"] = "Type a song title to search",
```

- [ ] **Step 2: Add shell state on ViewModel**

Near existing `_discoverModeSegmentLabels` / `DiscoverModeIndex`:

```csharp
private readonly string[] _discoverShellSegmentLabels = ["", ""];

[ObservableProperty]
private int _discoverShellIndex; // default 0 = Recommend

public IReadOnlyList<string> DiscoverShellSegmentLabels => _discoverShellSegmentLabels;

public string DiscoverShellToolTip => _l10n.Get("Discover.Shell.ToolTip");

public bool IsRecommendShell =>
    DiscoverShellIndex == DiscoverBrowseHubState.ShellRecommend;

public bool IsSearchShell =>
    DiscoverShellIndex == DiscoverBrowseHubState.ShellSearch;
```

In the localization refresh method that fills `_discoverModeSegmentLabels`, also:

```csharp
_discoverShellSegmentLabels[0] = _l10n.Get("Discover.Shell.Recommend");
_discoverShellSegmentLabels[1] = _l10n.Get("Discover.Shell.Search");
OnPropertyChanged(nameof(DiscoverShellSegmentLabels));
OnPropertyChanged(nameof(DiscoverShellToolTip));
```

Clamp shell index in `OnDiscoverShellIndexChanged`:

```csharp
partial void OnDiscoverShellIndexChanged(int value)
{
    var clamped = Math.Clamp(value, 0, 1);
    if (clamped != value)
    {
        DiscoverShellIndex = clamped;
        return;
    }

    NotifyDiscoverBrowseSessionUi();
    OnPropertyChanged(nameof(IsRecommendShell));
    OnPropertyChanged(nameof(IsSearchShell));
    OnPropertyChanged(nameof(ShowDiscoverAdvanced));
    NotifyUnifiedSourceFailureBanner();
    OnPropertyChanged(nameof(ShowRecommendHealthCue));
}
```

- [ ] **Step 3: Retarget derived visibility**

Replace the current browse/search chrome properties with shell-aware versions:

```csharp
public bool ShowBrowseHub =>
    DiscoverBrowseHubState.ShowBrowseHub(DiscoverShellIndex);

public bool UseHeroSearch => DiscoverBrowseHubState.UseHeroSearch(ShowBrowseHub);

public bool UseCompactSearch =>
    DiscoverBrowseHubState.UseCompactSearch(DiscoverShellIndex);

public bool ShowSearchResultsPane => IsSearchShell;

public bool ShowDiscoverAdvanced =>
    IsSearchShell && (IsDiscoverAdvancedOpen || !IsSearchMode);

public bool ShowUnifiedSourceFailureBanner =>
    DiscoverBrowseHubState.ShowUnifiedSourceFailure(
        DiscoverShellIndex,
        UnifiedSourceFailure.Show);

public bool ShowRecommendHealthCue =>
    DiscoverBrowseHubState.ShowRecommendHealthCue(
        DiscoverShellIndex,
        ShowApiHealthBanner);
```

Keep `ShowDiscoverEmpty` as empty-results on Search shell (already `!ShowBrowseHub && …`); after Step 3 `!ShowBrowseHub` equals Search shell when not busy with results — verify empty title uses search hint when query blank:

Where `DiscoverEmptyTitle` / description are built for search mode with no results, prefer `_l10n.Get("Discover.Empty.SearchHint")` for the blank-query idle empty (do not invent a new hot-search engine).

Ensure `NotifyDiscoverBrowseSessionUi` still raises `ShowBrowseHub`, `UseHeroSearch`, `UseCompactSearch`, `ShowSearchResultsPane`, skeletons/errors that depend on `ShowBrowseHub`.

**Important:** Switching `DiscoverShellIndex` must **not** clear `SongNameQuery`, `HasActiveSearchSession`, or `Results`. Clearing only happens via `ClearSearchCriteriaCommand`.

- [ ] **Step 4: Build to catch call sites**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release
```

Expected: FAIL or warnings if any leftover call to `ShowBrowseHub(bool,bool,bool)` — fix those call sites to the new API (only ViewModel should call it).

- [ ] **Step 5: Commit (only if user asked)**

---

### Task 3: Primary CTA FocusSearch → switch shell + focus

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs` (`RunPrimaryCtaAsync`)
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml.cs` (`OnFocusSearchRequested`)

- [ ] **Step 1: Switch shell before focus**

In `RunPrimaryCtaAsync`, `case DiscoverPrimaryCtaKind.FocusSearch:`:

```csharp
case DiscoverPrimaryCtaKind.FocusSearch:
    DiscoverShellIndex = DiscoverBrowseHubState.ShellSearch;
    FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    break;
```

Add a public helper used by code-behind or keep the event:

```csharp
public void RequestSearchShellAndFocus()
{
    DiscoverShellIndex = DiscoverBrowseHubState.ShellSearch;
    FocusSearchRequested?.Invoke(this, EventArgs.Empty);
}
```

Prefer using the helper from CTA so one path exists.

- [ ] **Step 2: Focus Compact box (Hero may be collapsed)**

Update `OnFocusSearchRequested` in `SearchPageView.axaml.cs`:

```csharp
private void OnFocusSearchRequested(object? sender, EventArgs e)
{
    Dispatcher.UIThread.Post(() =>
    {
        // Prefer compact (Search shell); fall back if Hero still in tree
        if (SongNameQueryBox.IsVisible)
            SongNameQueryBox.Focus();
        else if (HeroSongQueryBox.IsVisible)
            HeroSongQueryBox.Focus();
        else
            SongNameQueryBox.Focus();
    }, DispatcherPriority.Input);
}
```

- [ ] **Step 3: Keep PrimaryCta priority tests unchanged**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverPrimaryCtaTests"
```

Expected: PASS (Resolve priority unchanged; only action wiring changed).

- [ ] **Step 4: Commit (only if user asked)**

---

### Task 4: XAML — page-top segment + pane chrome

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml`

- [ ] **Step 1: Insert shell segment at top of discover-search-band**

Inside the `Border` `Classes="discover-search-band"` → first child `StackPanel`, **before** Hero/Compact shells, add:

```xml
<DockPanel Margin="{DynamicResource PageHeaderMargin}" LastChildFill="False">
  <controls:IntSegmentedBar DockPanel.Dock="Left"
                            Width="220"
                            SegmentLabels="{Binding DiscoverShellSegmentLabels}"
                            SelectedIndex="{Binding DiscoverShellIndex}"
                            ToolTip.Tip="{Binding DiscoverShellToolTip}"
                            automation:AutomationProperties.Name="{Binding DiscoverShellToolTip}"/>
  <!-- Optional light health cue on Recommend -->
  <TextBlock DockPanel.Dock="Right"
             Classes="text-caption"
             VerticalAlignment="Center"
             Margin="{DynamicResource FieldCaptionLeadingMargin}"
             Text="{Binding UnifiedApiStatusText}"
             ToolTip.Tip="{Binding UnifiedApiStatusToolTip}"
             IsVisible="{Binding ShowRecommendHealthCue}"/>
</DockPanel>
```

- [ ] **Step 2: Hide Hero permanently (or leave bound to UseHeroSearch=false)**

Keep Hero `Border` in tree but `IsVisible="{Binding UseHeroSearch}"` (always false). Do **not** delete yet unless build complains — safer for one release; can remove in cleanup.

- [ ] **Step 3: Compact search only on Search shell**

Confirm Compact `IsVisible="{Binding UseCompactSearch}"`.

Move the unified failure banner (and optional old DiscoverAlert) so it sits under Compact / advanced on Search shell — keep existing bindings; gating is now in VM `ShowUnifiedSourceFailureBanner`.

- [ ] **Step 4: Advanced block only when ShowDiscoverAdvanced**

Existing `IsVisible="{Binding ShowDiscoverAdvanced}"` wraps mode segment + artist + parse + recognize + login capsules. After Task 2 this is Search-shell-only. No XAML change required beyond verifying Recommend never shows mode/login matrix.

- [ ] **Step 5: Browse hub + results panes**

- Browse hub `ScrollViewer` stays `IsVisible="{Binding ShowBrowseHub}"` (Recommend).
- Results pane / empty / skeleton stay under `ShowSearchResultsPane` (Search).

- [ ] **Step 6: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release
```

Expected: PASS.

- [ ] **Step 7: Commit (only if user asked)**

---

### Task 5: PRODUCT_DESIGN + helper docs

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` (§7.4)
- Modify: `docs/superpowers/specs/2026-11-11-discover-online-listen-redesign.md` (status already 已确认)

- [ ] **Step 1: Update §7.4 listen paragraph**

Replace the listen bullet opening so it matches the shell:

```markdown
**听：** 发现页顶部分段 **推荐 | 搜索**（默认推荐）。推荐：主 CTA + 榜单/歌单墙；搜索：吸顶搜框 + 结果；识曲 / 粘贴链接在「更多」内。均 → 同一底栏在线播放。识曲成功后可点「播放」或「下一首播放」，仍留在发现；双击结果行同样可听。无可播放源时按钮禁用，并提示改关键词搜索。统一源失败条以搜索态为主。这是核心 B 的合格线。
```

- [ ] **Step 2: Commit (only if user asked)**

---

### Task 6: Regression tests + publish smoke

**Files:** none new (run existing)

- [ ] **Step 1: Run Discover-related unit tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~Discover"
```

Expected: `DiscoverBrowseHubStateTests`, `DiscoverPrimaryCtaTests`, `DiscoverSourceFailureBannerTests` all PASS. Fix any compile breaks in smoke tests that construct Search page if they assert old Hero/browse coupling.

- [ ] **Step 2: Publish and restart**

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] **Step 3: Manual checklist (spec §8)**

1. Cold open Discover → **推荐** with CTA + charts/playlists; no big Hero search; no login capsule matrix.
2. Tap **搜索** → Compact search visible; type + search → results play in bottom bar.
3. Tap **推荐** then **搜索** again → prior query/results still there (if not cleared).
4. On Recommend, Primary CTA “搜索歌曲” → lands on Search with focus in song box.
5. 识曲 / 粘贴 not visible until **更多** (or mode switch under More) on Search shell.
6. Failure banner appears on Search when sources unhealthy; Recommend shows at most the light status caption.

---

## Self-review (plan vs spec)

| Spec requirement | Task |
|------------------|------|
| Top 推荐 \| 搜索 equal segments | T2 + T4 |
| Default Recommend | T2 default `_discoverShellIndex = 0` |
| Recommend: CTA + charts + playlists, no Hero | T1 UseHeroSearch=false, T4 |
| Search: sticky + results; 识曲/粘贴 in More | T2 ShowDiscoverAdvanced, T4 |
| Failure banner mainly Search | T1 + T2 |
| CTA FocusSearch → switch + focus | T3 |
| Switch shell does not clear session | T2 Important note |
| Clear does not have to leave Search | T2 (session clear only via Clear) |
| No new platforms / recognize rewrite / transfers column | Out of scope |
| PRODUCT_DESIGN §7.4 | T5 |
| publish -Restart | T6 |

No TBD placeholders. Types: `DiscoverShellIndex` int 0/1; constants on `DiscoverBrowseHubState`; existing `DiscoverPrimaryCtaKind.FocusSearch` retained.
