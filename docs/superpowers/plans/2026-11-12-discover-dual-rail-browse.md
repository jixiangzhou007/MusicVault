# Discover dual-rail browse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Discover browse hub’s playlist/chart cover rails with a fixed one-screen layout: Hero promo + left「热门歌曲」+ right「猜你喜欢」, each showing 10 tracks with「换一换」, no page scroll.

**Architecture:** Keep session-driven `ShowBrowseHub`. Extract pure batch slicing for TDD. Extend `DiscoverHeroPromoLoader` (or thin wrapper) with a HotSongs pool. `SearchPageViewModel` owns two `ObservableCollection<OnlineTrackViewModel>` rails + refresh offsets. XAML: drop outer `ScrollViewer` / chart / playlist sections; `Grid` Hero `Auto` + dual rails `*`.

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, existing NetEase chart/recommend APIs, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-12-discover-dual-rail-browse-design.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

**Publish:** After UI tasks, `.\scripts\publish-with-api.ps1 -Restart`. Tests: `dotnet test ... -r win-x64`.

---

## File map

| File | Role |
|------|------|
| Create `src/MusicVault.App/Services/DiscoverTrackBatchSlicer.cs` | Pure slice/exclude/wrap for 10-track batches |
| Modify `src/MusicVault.App/Services/DiscoverHeroPromoLoader.cs` | Use slicer; add HotSongs pool load; public load helpers for rails |
| Create `tests/MusicVault.Tests/App/DiscoverTrackBatchSlicerTests.cs` | TDD slicer |
| Modify `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs` | Dual-rail collections, load/refresh, stop chart/playlist hub load in refresh path |
| Modify `src/MusicVault.App/Views/Pages/SearchPageView.axaml` | No page scroll; remove chart/playlist UI; dual-rail lists |
| Modify `src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml` | Rail row / header styles |
| Modify `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs` | Rail titles / refresh / fallback hint |
| Modify `docs/PRODUCT_DESIGN.md` §7.4 | One sentence: dual-rail browse |
| Modify smoke if needed `tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs` | Still asserts hub↔session; optionally assert new props exist |

---

### Task 1: Pure batch slicer (TDD)

**Files:**
- Create: `src/MusicVault.App/Services/DiscoverTrackBatchSlicer.cs`
- Create: `tests/MusicVault.Tests/App/DiscoverTrackBatchSlicerTests.cs`
- Modify: `src/MusicVault.App/Services/DiscoverHeroPromoLoader.cs` (delegate `SliceBatch` → slicer)

- [ ] **Step 1: Write failing tests**

```csharp
using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverTrackBatchSlicerTests
{
    private static OnlineTrack T(string id) => new()
    {
        Id = id,
        Title = id,
        Artist = "a",
        Platform = MusicPlatform.NetEase
    };

    [Fact]
    public void Slice_empty_pool_returns_empty() =>
        Assert.Empty(DiscoverTrackBatchSlicer.Slice([], take: 10, offset: 0, excludeIds: null));

    [Fact]
    public void Slice_wraps_and_takes_exact_count()
    {
        var pool = Enumerable.Range(0, 15).Select(i => T(i.ToString())).ToList();
        var batch = DiscoverTrackBatchSlicer.Slice(pool, take: 10, offset: 10, excludeIds: null);
        Assert.Equal(10, batch.Count);
        Assert.Equal("10", batch[0].Id);
        Assert.Equal("4", batch[9].Id); // wrap: 10..14,0..4
    }

    [Fact]
    public void Slice_excludes_ids_then_wraps()
    {
        var pool = Enumerable.Range(0, 12).Select(i => T(i.ToString())).ToList();
        var exclude = Enumerable.Range(0, 10).Select(i => i.ToString()).ToArray();
        var batch = DiscoverTrackBatchSlicer.Slice(pool, take: 10, offset: 0, excludeIds: exclude);
        Assert.Equal(10, batch.Count);
        Assert.DoesNotContain(batch, t => t.Id is "0" or "1"); // first excluded batch gone; uses 10,11 then wraps remaining
        Assert.Contains(batch, t => t.Id == "10");
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (type missing)**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverTrackBatchSlicerTests" -r win-x64
```

- [ ] **Step 3: Implement slicer**

```csharp
namespace MusicVault.App.Services;

public static class DiscoverTrackBatchSlicer
{
    public static IReadOnlyList<OnlineTrack> Slice(
        IReadOnlyList<OnlineTrack> pool,
        int take,
        int offset,
        IReadOnlyCollection<string>? excludeIds)
    {
        if (pool.Count == 0)
            return [];

        take = Math.Clamp(take, 1, 20);
        offset = Math.Max(0, offset);

        var exclude = excludeIds is { Count: > 0 }
            ? new HashSet<string>(excludeIds, StringComparer.Ordinal)
            : null;

        var filtered = exclude == null
            ? pool.ToList()
            : pool.Where(t => string.IsNullOrEmpty(t.Id) || !exclude.Contains(t.Id)).ToList();

        if (filtered.Count == 0)
            filtered = pool.ToList();

        if (filtered.Count <= take)
            return filtered;

        var start = offset % filtered.Count;
        var batch = new List<OnlineTrack>(take);
        for (var i = 0; i < take; i++)
            batch.Add(filtered[(start + i) % filtered.Count]);
        return batch;
    }
}
```

In `DiscoverHeroPromoLoader`, replace private `SliceBatch` body with `return DiscoverTrackBatchSlicer.Slice(pool, take, offset, excludeIds);`.

- [ ] **Step 4: Run tests — expect PASS**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverTrackBatchSlicerTests" -r win-x64
```

- [ ] **Step 5: Commit only if user asked**

---

### Task 2: Loader — HotSongs + rail load API

**Files:**
- Modify: `src/MusicVault.App/Services/DiscoverHeroPromoLoader.cs`

- [ ] **Step 1: Add rail source enum + LoadRailAsync**

```csharp
public enum DiscoverRailSource
{
    HotSongs = 0,
    GuessYouLike = 1
}

// Inside DiscoverHeroPromoLoader:
public static async Task<IReadOnlyList<OnlineTrack>> LoadRailAsync(
    DiscoverRailSource rail,
    int take = DefaultTake,
    int offset = 0,
    IReadOnlyCollection<string>? excludeIds = null,
    CancellationToken ct = default)
{
    take = Math.Clamp(take, 1, 20);
    offset = Math.Max(0, offset);
    await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

    IReadOnlyList<OnlineTrack> pool = rail switch
    {
        DiscoverRailSource.GuessYouLike => await LoadGuessYouLikePoolAsync(ct).ConfigureAwait(false),
        _ => await LoadHotSongsPoolAsync(ct).ConfigureAwait(false)
    };

    return DiscoverTrackBatchSlicer.Slice(pool, take, offset, excludeIds);
}

private static async Task<IReadOnlyList<OnlineTrack>> LoadHotSongsPoolAsync(CancellationToken ct)
{
    var hot = await TryLoadFromNamedChartAsync(["热歌", "飙升"], PoolTake, ct).ConfigureAwait(false);
    if (hot.Count > 0)
        return hot;
    return await LoadNewSongsPoolAsync(ct).ConfigureAwait(false);
}
```

Keep existing `LoadAsync(DiscoverHeroPromoSource, ...)` for Hero unchanged (still NewSongs / GuessYouLike).

- [ ] **Step 2: Build App project**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64
```

Expected: 0 errors.

- [ ] **Step 3: Commit only if user asked**

---

### Task 3: ViewModel dual rails

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Modify: `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs`

- [ ] **Step 1: Add localization keys (zh + en)**

```csharp
["Discover.Rail.HotTitle"] = "热门歌曲",
["Discover.Rail.GuessTitle"] = "猜你喜欢",
["Discover.Rail.Refresh"] = "换一换",
["Discover.Rail.GuessFallbackHint"] = "暂用热门",
["Discover.Rail.Empty"] = "暂无曲目",
["Discover.Rail.Retry"] = "重试",
// EN:
["Discover.Rail.HotTitle"] = "Hot songs",
["Discover.Rail.GuessTitle"] = "For you",
["Discover.Rail.Refresh"] = "Refresh",
["Discover.Rail.GuessFallbackHint"] = "Showing hot songs",
["Discover.Rail.Empty"] = "No tracks yet",
["Discover.Rail.Retry"] = "Retry",
```

- [ ] **Step 2: Add VM state (near Hero promo fields)**

```csharp
public ObservableCollection<OnlineTrackViewModel> HotRailTracks { get; } = [];
public ObservableCollection<OnlineTrackViewModel> GuessRailTracks { get; } = [];

[ObservableProperty] private bool _isLoadingHotRail;
[ObservableProperty] private bool _isLoadingGuessRail;
[ObservableProperty] private string? _hotRailError;
[ObservableProperty] private string? _guessRailError;
[ObservableProperty] private bool _guessRailUsedHotFallback;

private int _hotRailOffset;
private int _guessRailOffset;
private CancellationTokenSource? _hotRailLoadCts;
private CancellationTokenSource? _guessRailLoadCts;

public string HotRailTitle => _l10n.Get("Discover.Rail.HotTitle");
public string GuessRailTitle => _l10n.Get("Discover.Rail.GuessTitle");
public string RailRefreshText => _l10n.Get("Discover.Rail.Refresh");
public string RailEmptyText => _l10n.Get("Discover.Rail.Empty");
public string RailRetryText => _l10n.Get("Discover.Rail.Retry");
public string GuessRailFallbackHintText => _l10n.Get("Discover.Rail.GuessFallbackHint");

public bool CanRefreshHotRail => !IsLoadingHotRail;
public bool CanRefreshGuessRail => !IsLoadingGuessRail;
public bool ShowGuessRailFallbackHint => GuessRailUsedHotFallback && GuessRailTracks.Count > 0;
```

Reuse `CreateHeroPromoTrackVm` (rename later only if needed) for rail rows — same `OnlineTrackViewModel` + `EnsureCoverLoadedAsync`.

- [ ] **Step 3: Load/refresh methods**

```csharp
private async Task LoadHotRailAsync(bool advanceBatch)
{
    if (TestSessionProbe.IsActive) return;
    _hotRailLoadCts?.Cancel();
    _hotRailLoadCts = new CancellationTokenSource();
    var ct = _hotRailLoadCts.Token;
    IsLoadingHotRail = true;
    HotRailError = null;
    RefreshHotRailCommand.NotifyCanExecuteChanged();
    try
    {
        var exclude = advanceBatch
            ? HotRailTracks.Select(t => t.Model.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray()
            : null;
        if (advanceBatch) _hotRailOffset += DiscoverHeroPromoLoader.DefaultTake;
        else _hotRailOffset = 0;

        var tracks = await DiscoverHeroPromoLoader.LoadRailAsync(
            DiscoverRailSource.HotSongs, DiscoverHeroPromoLoader.DefaultTake, _hotRailOffset, exclude, ct)
            .ConfigureAwait(true);
        if (ct.IsCancellationRequested) return;
        if (tracks.Count == 0 && _hotRailOffset > 0)
        {
            _hotRailOffset = 0;
            tracks = await DiscoverHeroPromoLoader.LoadRailAsync(
                DiscoverRailSource.HotSongs, DiscoverHeroPromoLoader.DefaultTake, 0, null, ct)
                .ConfigureAwait(true);
        }

        HotRailTracks.Clear();
        foreach (var t in tracks)
        {
            var vm = CreateHeroPromoTrackVm(t);
            if (vm == null) continue;
            HotRailTracks.Add(vm);
            _ = vm.EnsureCoverLoadedAsync(ct);
        }
        if (HotRailTracks.Count == 0)
            HotRailError = "Discover.Rail.Empty";
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        HotRailError = ex.Message;
        HotRailTracks.Clear();
    }
    finally
    {
        IsLoadingHotRail = false;
        RefreshHotRailCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRefreshHotRail));
    }
}

// LoadGuessRailAsync: same shape with DiscoverRailSource.GuessYouLike.
// If result empty, retry HotSongs with offset = _guessRailOffset + 100 (or PoolTake) and set GuessRailUsedHotFallback = true.
// On success from Guess pool: GuessRailUsedHotFallback = false.

[RelayCommand(CanExecute = nameof(CanRefreshHotRail))]
private async Task RefreshHotRailAsync() =>
    await LoadHotRailAsync(advanceBatch: true).ConfigureAwait(true);

[RelayCommand(CanExecute = nameof(CanRefreshGuessRail))]
private async Task RefreshGuessRailAsync() =>
    await LoadGuessRailAsync(advanceBatch: true).ConfigureAwait(true);

[RelayCommand]
private async Task RetryHotRailAsync() =>
    await LoadHotRailAsync(advanceBatch: false).ConfigureAwait(true);

[RelayCommand]
private async Task RetryGuessRailAsync() =>
    await LoadGuessRailAsync(advanceBatch: false).ConfigureAwait(true);
```

- [ ] **Step 4: Wire `RefreshBrowseHubAsync`**

Replace chart/playlist loads with rails (keep Hero):

```csharp
await Task.WhenAll(
    LoadHeroPromoAsync(forceChartsRefresh),
    LoadHotRailAsync(advanceBatch: false),
    LoadGuessRailAsync(advanceBatch: false)).ConfigureAwait(true);
```

Leave `LoadBrowseChartsAsync` / `LoadBrowsePlaylistsAsync` in codebase unused by hub refresh for now (dead path OK this round; do not delete deep playlist navigation helpers still used elsewhere). Remove XAML bindings to chart/playlist sections in Task 4.

- [ ] **Step 5: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64
```

- [ ] **Step 6: Commit only if user asked**

---

### Task 4: XAML + styles — one-screen dual rail

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml`
- Modify: `src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml`

- [ ] **Step 1: Replace browse hub root**

Remove `ScrollViewer` named `BrowseHubScroll`. Use:

```xml
<Grid x:Name="BrowseHubRoot" Grid.Row="2"
      Classes="discover-browse-hub"
      IsVisible="{Binding ShowBrowseHub}"
      Margin="{DynamicResource MarginTopSm}"
      RowDefinitions="Auto,*">
  <!-- Row 0: existing Hero Border (unchanged markup) -->
  <!-- Row 1: dual rails -->
  <Grid Grid.Row="1"
        ColumnDefinitions="*,*"
        Margin="{DynamicResource PageContentHorizontalMargin}">
    <!-- Left Hot -->
    <DockPanel Grid.Column="0" Margin="0,0,8,0" LastChildFill="True">
      <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8" LastChildFill="True">
        <Button DockPanel.Dock="Right" Classes="flat-btn discover-hero-refresh"
                Content="{Binding RailRefreshText}"
                Command="{Binding RefreshHotRailCommand}"
                automation:AutomationProperties.Name="{Binding RailRefreshText}"/>
        <TextBlock Text="{Binding HotRailTitle}" Classes="discover-browse-section-title"
                   VerticalAlignment="Center"/>
      </DockPanel>
      <ItemsControl ItemsSource="{Binding HotRailTracks}" Classes="discover-rail-list">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <UniformGrid Columns="1" Rows="10"/>
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:OnlineTrackViewModel">
            <Button Classes="flat-btn discover-rail-row"
                    Command="{Binding PreviewCommand}"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Stretch">
              <Grid ColumnDefinitions="Auto,*" Margin="0,2">
                <Border Width="40" Height="40" CornerRadius="{DynamicResource RadiusControl}"
                        ClipToBounds="True" Margin="0,0,10,0">
                  <Image Source="{Binding CoverBitmap}" Stretch="UniformToFill"
                         IsVisible="{Binding HasCoverThumb}"/>
                </Border>
                <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
                  <TextBlock Text="{Binding Title}" Classes="text-body"
                             TextTrimming="CharacterEllipsis"/>
                  <TextBlock Text="{Binding Artist}" Classes="text-caption"
                             Foreground="{DynamicResource TextSecondaryBrush}"
                             TextTrimming="CharacterEllipsis"/>
                </StackPanel>
              </Grid>
            </Button>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <!-- skeleton / error / empty: IsVisible bindings on HotRail; mirror Guess column -->
    </DockPanel>
    <!-- Right Guess: same with GuessRail* + ShowGuessRailFallbackHint TextBlock under header -->
  </Grid>
</Grid>
```

Delete entire「ChartsRail」and「PlaylistWall」`StackPanel` blocks from this page.

Ensure `xmlns:vm` already maps to `OnlineTrackViewModel` namespace used by results (`vm:OnlineTrackViewModel`).

- [ ] **Step 2: Styles**

```xml
<Style Selector="ItemsControl.discover-rail-list">
  <Setter Property="HorizontalAlignment" Value="Stretch"/>
  <Setter Property="VerticalAlignment" Value="Stretch"/>
</Style>
<Style Selector="Button.discover-rail-row">
  <Setter Property="Padding" Value="8,4"/>
  <Setter Property="MinHeight" Value="0"/>
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
  <Setter Property="Cursor" Value="Hand"/>
</Style>
<Style Selector="Button.discover-rail-row:pointerover /template/ ContentPresenter">
  <Setter Property="Background" Value="{DynamicResource SurfaceElevatedBrush}"/>
</Style>
```

- [ ] **Step 3: Build + publish restart**

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

Manual: 发现页无整页滚动；无推荐歌单/横滑榜单；双栏各 ≤10 行；换一换换批；点行试听；搜索后双栏隐藏。

- [ ] **Step 4: Commit only if user asked**

---

### Task 5: Docs + smoke

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` §7.4 discover sentence
- Modify: `tests/MusicVault.Tests/App/MainWindowHeadlessSmokeTests.cs` (only if hub assertions break)

- [ ] **Step 1: Update PRODUCT_DESIGN §7.4**

Replace browse-hub wording roughly with:  
「无搜索会话时：广告轮播 + 左热门歌曲 / 右猜你喜欢定长列表（各 10 首、换一换）；无页面滚动。」

- [ ] **Step 2: Run hub-related tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverBrowseHubStateTests|FullyQualifiedName~DiscoverTrackBatchSlicerTests|FullyQualifiedName~MainWindowHeadlessSmokeTests" -r win-x64
```

Expected: PASS.

- [ ] **Step 3: Final publish**

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] **Step 4: Commit only if user asked**

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Remove 推荐歌单 + 横滑榜单 | Task 4 |
| Hero kept | Task 4 (no Hero regress); Task 3 keeps LoadHeroPromo |
| Dual rails Hot / Guess | Tasks 2–4 |
| Fixed 10 + 换一换 | Tasks 1–3 |
| No page / column scroll | Task 4 Grid + UniformGrid Rows=10 |
| Data split Hot vs Guess + fallback | Task 2–3 |
| Search session hides hub | unchanged; Task 5 smoke |
| l10n keys | Task 3 |
| Publish verify | Tasks 4–5 |

## Placeholder / consistency self-check

- No TBD steps; slicer name `DiscoverTrackBatchSlicer.Slice` used in Tasks 1–2.  
- Rail enum `DiscoverRailSource`; commands `RefreshHotRail` / `RefreshGuessRail`.  
- Take constant `DiscoverHeroPromoLoader.DefaultTake` (=10) everywhere.
