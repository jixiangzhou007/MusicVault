# Discover Browse Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Discover page into a browse hub (charts + playlist wall when idle) with hero→compact search, while keeping playlist detail on the Playlists page.

**Architecture:** Add a pure visibility helper for browse vs search session; extend `SearchPageViewModel` to load chart/playlist preview cards via existing `NetEaseChartsService` + `PlaylistService`; deep-link card clicks through `MainWindowViewModel` into a new `PlaylistsPageViewModel.OpenBrowseTargetAsync`; restyle `SearchPageView` into browse/search layouts using existing radius tokens (no pill corners).

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, existing MusicVault services/tests (`dotnet test`)

**Spec:** `docs/superpowers/specs/2026-07-31-discover-browse-hub-design.md`

---

## File map

| File | Responsibility |
|------|----------------|
| `src/MusicVault.App/Services/DiscoverBrowseHubState.cs` | Pure rules: when to show browse hub / hero vs compact search |
| `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs` | Unit tests for those rules |
| `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs` | `HasActiveSearchSession`, browse collections, load/refresh, clear/search wiring |
| `src/MusicVault.App/Views/Pages/SearchPageView.axaml` | Hero/compact search + ChartsRail + PlaylistWall + results visibility |
| `src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml` | Browse hub styles; keep `RadiusLg`/`RadiusCard` (no pill) |
| `src/MusicVault.App/ViewModels/Pages/PlaylistsPageViewModel.cs` | Public `OpenBrowseTargetAsync` for chart id / playlist id |
| `src/MusicVault.App/ViewModels/MainWindowViewModel.cs` | `OpenDiscoverBrowseTarget` / navigate bridge |
| `src/MusicVault.App/Localization/*` (+ Discover resources if used) | Section titles: 热门榜单 / 推荐歌单 / 全部 / 更多 |
| `tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs` | Smoke: browse visible when idle; search session hides browse |

**Reuse (do not duplicate types):** `PlaylistBrowseCardViewModel` in `PlaylistsPageViewModel.cs`; `NetEaseChartsService`; `PlaylistService.GetAllAsync`.

**Known product limit (document in UI copy if needed):** Charts API is NetEase-backed today (`NetEaseChartsService`). Playlist wall shows the user’s playlists (same pool as Playlists page). No new recommendation algorithm.

---

### Task 1: DiscoverBrowseHubState + tests

**Files:**
- Create: `src/MusicVault.App/Services/DiscoverBrowseHubState.cs`
- Create: `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverBrowseHubStateTests
{
    [Theory]
    [InlineData(true, false, false, true)]   // search mode, idle
    [InlineData(true, true, false, false)]  // active search session
    [InlineData(true, false, true, false)]  // searching busy
    [InlineData(false, false, false, false)] // parse/recognize mode
    public void ShowBrowseHub_matches_spec(
        bool isSearchMode, bool hasActiveSearchSession, bool isBusy, bool expected)
    {
        Assert.Equal(expected,
            DiscoverBrowseHubState.ShowBrowseHub(isSearchMode, hasActiveSearchSession, isBusy));
    }

    [Theory]
    [InlineData(true, true)]   // browse → hero
    [InlineData(false, false)] // search session → compact
    public void UseHeroSearch_follows_browse_hub(bool showBrowseHub, bool expectedHero)
    {
        Assert.Equal(expectedHero, DiscoverBrowseHubState.UseHeroSearch(showBrowseHub));
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (type missing)**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests" --no-restore 2>&1
```

If restore needed: `dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests"`

Expected: compile error `DiscoverBrowseHubState` not found.

- [ ] **Step 3: Implement helper**

```csharp
namespace MusicVault.App.Services;

/// <summary>Discover browse-hub vs search-session visibility (spec 2026-07-31).</summary>
public static class DiscoverBrowseHubState
{
    /// <param name="isBusy">IsSearching || IsParsingLink || IsRecognizing</param>
    public static bool ShowBrowseHub(
        bool isSearchMode,
        bool hasActiveSearchSession,
        bool isBusy) =>
        isSearchMode && !hasActiveSearchSession && !isBusy;

    public static bool UseHeroSearch(bool showBrowseHub) => showBrowseHub;
}
```

- [ ] **Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests"
```

Expected: all passed.

- [ ] **Step 5: Commit** (only if user asked for commits in this session; otherwise skip)

```powershell
git add src/MusicVault.App/Services/DiscoverBrowseHubState.cs tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs
git commit -m "test: add Discover browse-hub visibility helper"
```

---

### Task 2: Search session flag + Clear/Search wiring

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Test: extend `tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs` only if extracting more pure logic; otherwise cover via headless in Task 7

- [ ] **Step 1: Add session + derived props on `SearchPageViewModel`**

Near other discover UI props (`ShowDiscoverEmpty` ~L1177):

```csharp
[ObservableProperty]
private bool _hasActiveSearchSession;

public bool IsDiscoverBusy => IsSearching || IsParsingLink || IsRecognizing;

public bool ShowBrowseHub =>
    DiscoverBrowseHubState.ShowBrowseHub(IsSearchMode, HasActiveSearchSession, IsDiscoverBusy);

public bool UseHeroSearch => DiscoverBrowseHubState.UseHeroSearch(ShowBrowseHub);

public bool UseCompactSearch => IsSearchMode && !UseHeroSearch;

/// <summary>Results / empty-search UI (not browse hub).</summary>
public bool ShowSearchResultsPane =>
    !ShowBrowseHub && (IsSearchMode || IsParseMode || IsRecognizeMode);
```

Update `ShowDiscoverEmpty` so browse hub does **not** show the old empty-results panel:

```csharp
public bool ShowDiscoverEmpty =>
    !ShowBrowseHub
    && !IsSearching && !IsParsingLink && !IsRecognizing
    && !HasResults;
```

Notify `ShowBrowseHub` / `UseHeroSearch` / `UseCompactSearch` / `ShowSearchResultsPane` / `ShowDiscoverEmpty` wherever `IsSearching`, `Results`, or mode changes (same places that already call `OnPropertyChanged(nameof(ShowDiscoverEmpty))`).

- [ ] **Step 2: Set session on successful search submit**

In `SearchAsync` after validation passes and search actually starts (before/after clearing previous results — mirror existing clear of `Results`):

```csharp
HasActiveSearchSession = true;
```

Also set `HasActiveSearchSession = true` when parse-link / recognize flows produce a results session (same moment they currently flip into result loading).

- [ ] **Step 3: Clear session returns to browse**

Change `ClearSearchCriteria` to also end the session:

```csharp
private void ClearSearchCriteria()
{
    SongNameQuery = string.Empty;
    ArtistQuery = string.Empty;
    // keep LinkInput behavior as today if separate
    ClearResults();
    HasActiveSearchSession = false;
    // notify ShowBrowseHub / UseHeroSearch / ...
}
```

Ensure the clear (✕) button uses this path. If `ClearResultsCommand` is used alone elsewhere, leave it — only ✕ / Esc that means “back to browse” should clear the session. Prefer wiring ✕ to `ClearSearchCriteriaCommand` (already) and make that method session-aware as above.

- [ ] **Step 4: Manual sanity** — build App project

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release
```

Expected: build succeeded.

- [ ] **Step 5: Commit** (if requested)

```powershell
git add src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs
git commit -m "feat: Discover active search session for browse hub"
```

---

### Task 3: Load ChartsRail + PlaylistWall data on Search VM

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Optionally extract cover loading helpers if duplication hurts; first version may call `NetEaseChartsService` + thin local load

- [ ] **Step 1: Add collections + UI state**

```csharp
public ObservableCollection<PlaylistBrowseCardViewModel> BrowseChartCards { get; } = [];
public ObservableCollection<PlaylistBrowseCardViewModel> BrowsePlaylistCards { get; } = [];

[ObservableProperty] private bool _isLoadingBrowseCharts;
[ObservableProperty] private bool _isLoadingBrowsePlaylists;
[ObservableProperty] private string? _browseChartsError;
[ObservableProperty] private string? _browsePlaylistsError;

public bool ShowBrowseChartsSkeleton => ShowBrowseHub && IsLoadingBrowseCharts;
public bool ShowBrowsePlaylistsSkeleton => ShowBrowseHub && IsLoadingBrowsePlaylists;
public bool HasBrowseChartCards => BrowseChartCards.Count > 0;
public bool HasBrowsePlaylistCards => BrowsePlaylistCards.Count > 0;
```

Cap display: charts take first **6**, playlists first **8**.

- [ ] **Step 2: Implement load methods**

```csharp
public async Task RefreshBrowseHubAsync(bool forceChartsRefresh = false)
{
    if (!IsSearchMode) return;
    await Task.WhenAll(
        LoadBrowseChartsAsync(forceChartsRefresh),
        LoadBrowsePlaylistsAsync()).ConfigureAwait(true);
}

private async Task LoadBrowseChartsAsync(bool forceRefresh)
{
    IsLoadingBrowseCharts = true;
    BrowseChartsError = null;
    try
    {
        var (charts, error) = await NetEaseChartsService.FetchChartsAsync(forceRefresh)
            .ConfigureAwait(true);
        BrowseChartCards.Clear();
        foreach (var c in charts.Take(6))
            BrowseChartCards.Add(PlaylistBrowseCardViewModel.FromChart(c, _l10n));
        BrowseChartsError = error;
        // fire-and-forget cover thumbs like PlaylistsPageViewModel.LoadBrowseCardCoversAsync
        // for chart cards only (CoverUrl + AlbumCoverLoader)
    }
    finally
    {
        IsLoadingBrowseCharts = false;
        OnPropertyChanged(nameof(HasBrowseChartCards));
        OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
    }
}

private async Task LoadBrowsePlaylistsAsync()
{
    IsLoadingBrowsePlaylists = true;
    BrowsePlaylistsError = null;
    try
    {
        BrowsePlaylistCards.Clear();
        var all = await _services.PlaylistService.GetAllAsync().ConfigureAwait(true);
        foreach (var p in all
            .OrderByDescending(p => p.UpdatedAt)
            .Take(8))
        {
            // Build PlaylistItemViewModel the same way PlaylistsPage does,
            // or add a FromPlaylist(Core.Models.Playlist, trackCount) factory if needed.
            var count = await _services.Playlists.GetTrackCountAsync(p.Id).ConfigureAwait(true);
            var item = new PlaylistItemViewModel(p, count, _l10n); // match existing ctor
            BrowsePlaylistCards.Add(PlaylistBrowseCardViewModel.FromPlaylist(item));
        }
    }
    catch (Exception ex)
    {
        BrowsePlaylistsError = ex.Message;
    }
    finally
    {
        IsLoadingBrowsePlaylists = false;
        OnPropertyChanged(nameof(HasBrowsePlaylistCards));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
    }
}
```

**Ctor note:** Match the real `PlaylistItemViewModel` constructor signature in the codebase (adjust the snippet to compile — do not invent fields).

Call `RefreshBrowseHubAsync()` from:
- Search page init path already used by `MainWindowViewModel.InitializePageBySectionAsync` for `NavigationSection.Search`
- `OnPlatformIndexChanged` when `ShowBrowseHub` (refresh charts/playlists; charts may still be NetEase-only)

Retry commands:

```csharp
[RelayCommand]
private Task RetryBrowseChartsAsync() => LoadBrowseChartsAsync(forceRefresh: true);

[RelayCommand]
private Task RetryBrowsePlaylistsAsync() => LoadBrowsePlaylistsAsync();
```

- [ ] **Step 3: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release
```

Expected: succeeded. Fix ctor/API mismatches until green.

- [ ] **Step 4: Commit** (if requested)

```powershell
git add src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs
git commit -m "feat: load Discover browse charts and playlist previews"
```

---

### Task 4: Deep-link card → Playlists page

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/PlaylistsPageViewModel.cs`
- Modify: `src/MusicVault.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs` (commands that raise navigate)

- [ ] **Step 1: Add opener on Playlists VM**

```csharp
/// <summary>Open Charts or Playlists category and select a browse card (Discover hub deep link).</summary>
public async Task OpenBrowseTargetAsync(MusicChart? chart = null, long? playlistId = null)
{
    await InitializeAsync().ConfigureAwait(true);

    if (chart != null)
    {
        SelectedCategory = PlaylistNavCategory.Charts;
        if (_charts.Count == 0)
            await LoadChartsAsync(forceRefresh: false).ConfigureAwait(true);
        RebuildBrowseCards();
        var card = BrowseCards.FirstOrDefault(c =>
            c.Chart != null && string.Equals(c.Chart.Id, chart.Id, StringComparison.Ordinal));
        if (card != null)
            await SelectBrowseCardAsync(card).ConfigureAwait(true);
        return;
    }

    if (playlistId is long id)
    {
        SelectedCategory = PlaylistNavCategory.Playlists;
        RebuildBrowseCards();
        var card = BrowseCards.FirstOrDefault(c => c.Playlist?.Playlist.Id == id);
        if (card != null)
            await SelectBrowseCardAsync(card).ConfigureAwait(true);
    }
}
```

Adjust `MusicChart` id property name to whatever the model uses (`Id` / `PlaylistId` — read `MusicVault.Core.Models.MusicChart`).

If `SelectBrowseCardAsync` is private via `[RelayCommand]`, call `SelectBrowseCardCommand.ExecuteAsync(card)` or make a public wrapper.

- [ ] **Step 2: Bridge from MainWindow**

```csharp
public Task OpenPlaylistBrowseTargetAsync(MusicChart? chart = null, long? playlistId = null)
{
    return OpenPlaylistBrowseTargetCoreAsync(chart, playlistId);
}

private async Task OpenPlaylistBrowseTargetCoreAsync(MusicChart? chart, long? playlistId)
{
    await Playlists.OpenBrowseTargetAsync(chart, playlistId).ConfigureAwait(true);
    await NavigateAsync(NavigationSection.Playlists).ConfigureAwait(true);
}
```

- [ ] **Step 3: Search VM commands**

Pass a navigate callback in Search VM ctor (same pattern as other shell callbacks) **or** event:

```csharp
public event EventHandler<DiscoverBrowseOpenRequest>? BrowseOpenRequested;

public readonly record struct DiscoverBrowseOpenRequest(MusicChart? Chart, long? PlaylistId);

[RelayCommand]
private void OpenBrowseChart(PlaylistBrowseCardViewModel? card)
{
    if (card?.Chart is null) return;
    BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(card.Chart, null));
}

[RelayCommand]
private void OpenBrowsePlaylist(PlaylistBrowseCardViewModel? card)
{
    if (card?.Playlist is null) return;
    BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(null, card.Playlist.Playlist.Id));
}

[RelayCommand]
private void OpenBrowseChartsAll() =>
    BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(null, null));
// MainWindow: Navigate Playlists + SelectedCategory = Charts

[RelayCommand]
private void OpenBrowsePlaylistsMore() =>
    BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(null, null));
// MainWindow: Navigate Playlists + SelectedCategory = Playlists
```

For All/More without a card, extend the request:

```csharp
public readonly record struct DiscoverBrowseOpenRequest(
    MusicChart? Chart,
    long? PlaylistId,
    PlaylistNavCategory? CategoryOnly);
```

MainWindow handler:
- If `Chart` or `PlaylistId` set → `OpenPlaylistBrowseTargetAsync`
- Else if `CategoryOnly` set → `Playlists.SelectedCategory = ...`; `NavigateAsync(Playlists)`

Wire subscription next to `Downloads.OpenDiscoverWithSearchRequested`.

- [ ] **Step 4: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release
```

- [ ] **Step 5: Commit** (if requested)

```powershell
git add src/MusicVault.App/ViewModels/Pages/PlaylistsPageViewModel.cs src/MusicVault.App/ViewModels/MainWindowViewModel.cs src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs
git commit -m "feat: deep-link Discover browse cards into Playlists"
```

---

### Task 5: SearchPageView layout (browse + hero/compact)

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml`
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml.cs` only if click handlers needed

- [ ] **Step 1: Split search chrome**

Keep one logical search field bindings; wrap existing `discover-search-shell` twice OR one shell with style classes:

- Container A: `IsVisible="{Binding UseHeroSearch}"` — centered, `Classes="discover-search-shell discover-search-hero"`
- Container B: `IsVisible="{Binding UseCompactSearch}"` — top, `Classes="discover-search-shell discover-search-compact"`

Both bind the same `SongNameQuery` / `PlatformIndex` / commands. Avoid duplicate `x:Name` on TextBoxes — use one visible TextBox at a time (only one `IsVisible=true`) **or** name only the compact box for focus helpers and use hero without `x:Name`.

Advanced panel: still under `ShowDiscoverAdvanced`; in hero mode it can appear below the hero shell.

- [ ] **Step 2: Browse hub panel**

Inside main column, above results, when `ShowBrowseHub`:

```xml
<ScrollViewer IsVisible="{Binding ShowBrowseHub}" ...>
  <!-- section: Charts -->
  <DockPanel Margin="...">
    <TextBlock Text="{Binding BrowseChartsSectionTitle}" Classes="discover-browse-section-title"/>
    <Button DockPanel.Dock="Right" Classes="flat-btn"
            Content="{Binding BrowseChartsAllText}"
            Command="{Binding OpenBrowseChartsAllCommand}"/>
  </DockPanel>
  <ItemsControl ItemsSource="{Binding BrowseChartCards}" ...>
    <!-- card template: cover + name; Command OpenBrowseChartCommand with CommandParameter -->
  </ItemsControl>
  <!-- skeleton / error / retry mirroring Playlists charts empty actions -->

  <!-- section: Playlists wall -->
  <!-- same pattern with BrowsePlaylistCards / OpenBrowsePlaylistCommand / OpenBrowsePlaylistsMoreCommand -->
</ScrollViewer>
```

Reuse visual patterns from `PlaylistsPageView.axaml` charts grid (cover `Image` from `CoverThumb`) but simpler — no merge checkboxes.

- [ ] **Step 3: Gate results**

Wrap existing results header/list/skeleton/empty with `IsVisible="{Binding ShowSearchResultsPane}"` (and keep inner `ShowDiscoverEmpty` etc.).

- [ ] **Step 4: Build + quick UI check after publish** (Task 8)

---

### Task 6: Styles + localization (no pill radius)

**Files:**
- Modify: `src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml`
- Modify: localization JSON / `DiscoverLocalizationResources.cs` as used by the app

- [ ] **Step 1: Styles**

```xml
<Style Selector="Border.discover-search-hero">
  <Setter Property="MaxWidth" Value="720"/>
  <Setter Property="HorizontalAlignment" Value="Center"/>
  <Setter Property="Margin" Value="24,28,24,8"/>
  <!-- CornerRadius MUST stay DynamicResource RadiusLg or RadiusCard — never 16+ pill -->
</Style>

<Style Selector="Border.discover-search-compact">
  <Setter Property="Margin" Value="{DynamicResource PageHeaderMargin}"/>
</Style>

<Style Selector="Border.discover-browse-card">
  <Setter Property="CornerRadius" Value="{DynamicResource RadiusCard}"/>
  <!-- light fill, no heavy elevation -->
</Style>
```

Remove any experimental pill radius if present on `discover-search-shell`. Confirm `ThemeResources.axaml`: `RadiusLg=8`, `RadiusXl=12` only for covers if already used on playlist covers.

Light top atmosphere: optional `Border.discover-browse-atmosphere` with low-opacity gradient brush aligned with `home-empty-hero` colors (no purple neon).

- [ ] **Step 2: Strings**

Add keys (Chinese + existing EN pattern in repo):

- `Discover.Browse.ChartsTitle` → 热门榜单  
- `Discover.Browse.ChartsAll` → 全部  
- `Discover.Browse.PlaylistsTitle` → 推荐歌单  
- `Discover.Browse.PlaylistsMore` → 更多  
- `Discover.Browse.ChartsRetry` / playlists retry (reuse charts retry strings if they exist)

Bind via existing l10n helpers on `SearchPageViewModel`.

- [ ] **Step 3: Commit** (if requested)

```powershell
git add src/MusicVault.App/Views/Pages/SearchPageView.axaml src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml src/MusicVault.App/Localization/**
git commit -m "feat: Discover browse hub layout and styles"
```

---

### Task 7: Headless / regression tests

**Files:**
- Modify: `tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs`

- [ ] **Step 1: Add assertions**

After navigating to Search (pattern already in file):

```csharp
Assert.True(main.Search.ShowBrowseHub);
Assert.False(main.Search.HasActiveSearchSession);

// Simulate session without full network if possible:
main.Search.HasActiveSearchSession = true; // if setter accessible; else call internal test hook
// Prefer: public method for tests only OR set via Clear/Search with mocked services

Assert.False(main.Search.ShowBrowseHub);
Assert.True(main.Search.UseCompactSearch);

main.Search.ClearSearchCriteriaCommand.Execute(null);
Assert.True(main.Search.ShowBrowseHub);
```

If `HasActiveSearchSession` is private-set ObservableProperty, assert through `ClearSearchCriteriaCommand` + toggling by invoking a package-visible test helper, **or** only assert `ShowBrowseHub` true on fresh navigate and `ShowDiscoverEmpty` false when browse hub shown (browse replaces empty).

Minimum bar:

1. Fresh Search navigate → `ShowBrowseHub == true`  
2. After `ClearSearchCriteriaCommand` with empty results → still `ShowBrowseHub == true`  
3. `DiscoverBrowseHubStateTests` still pass  

- [ ] **Step 2: Run tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests|FullyQualifiedName~MainWindowHeadlessSmokeTests"
```

Expected: pass.

- [ ] **Step 3: Commit** (if requested)

```powershell
git add tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs tests/MusicVault.Tests/App/DiscoverBrowseHubStateTests.cs
git commit -m "test: cover Discover browse hub idle state"
```

---

### Task 8: Publish and verify

**Files:** none (scripts)

- [ ] **Step 1: Stop + publish + restart**

```powershell
$env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages"
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] **Step 2: Manual checklist**

1. Open 发现 → see hero search (normal radius) + 热门榜单 + 推荐歌单 (not empty table).  
2. Search a song → browse hides; compact search; results list.  
3. Clear ✕ → browse returns.  
4. Click chart card → Playlists charts detail.  
5. Click playlist card → Playlists detail.  
6. 全部/更多 → Playlists on right category.  
7. No capsule/pill search corners; matches home/playlist card radii.  
8. Transfer pane still default-collapsed.

---

## Spec coverage check

| Spec item | Task |
|-----------|------|
| Browse vs search two states | 1, 2, 5 |
| Hero → compact search (no pill radius) | 5, 6 |
| ChartsRail + PlaylistWall | 3, 5 |
| Click → Playlists detail | 4 |
| All/More → Playlists category | 4, 5 |
| Clear → browse | 2 |
| Platform refresh browse | 3 |
| Transfer pane unchanged | 5 (no change) / 8 verify |
| P0 advanced-in-更多 | 5 (keep existing) |
| Non-goals (识曲/算法/双栏) | not implemented |

---

## Execution notes

- Prefer **not** expanding `DiscoverApplicationService` for charts.  
- `PlaylistBrowseCardViewModel` stays in `PlaylistsPageViewModel.cs` unless a later refactor moves it.  
- Commits in steps are optional unless the user explicitly asks to commit.  
- After implementation, follow workspace rule: publish with restart (`publish-with-api.ps1 -Restart`).
