# Discover for-you multicolumn Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Discover dual rails (热门|猜你喜欢) with one QQ-style「为你精选」multicolumn song grid: hover play + favorite, right-edge hover prev/next page, no page scroll; keep Hero promo.

**Architecture:** Pure layout helper chooses columns (2/3) and page size (~10–12) from width. `SearchPageViewModel` owns one `ForYouTracks` collection + page offset; load Guess pool first, pad with Hot via existing `LoadRailAsync` + `DiscoverTrackBatchSlicer`. XAML: single panel under Hero with `UniformGrid` (or ItemsControl + bound Columns), hover chrome for pager and row actions.

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, existing online favorite/`PreviewCommand`, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-12-discover-for-you-multicolumn-design.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

**Publish:** After UI task, `.\scripts\publish-with-api.ps1 -Restart`. Tests: `dotnet test ... -r win-x64`.

---

## File map

| File | Role |
|------|------|
| Create `src/MusicVault.App/Services/DiscoverForYouLayout.cs` | Columns + page size from width |
| Create `tests/MusicVault.Tests/App/DiscoverForYouLayoutTests.cs` | TDD layout |
| Modify `SearchPageViewModel.cs` | `ForYouTracks`, load/page commands; stop dual-rail hub load (delete or stop wiring Hot/Guess UI props) |
| Modify `SearchPageView.axaml` | Replace dual-rail Grid with for-you section |
| Modify `SearchPageView.axaml.cs` | SizeChanged → update `ForYouColumns` / reload page size if needed |
| Modify `DiscoverDownloads.axaml` | for-you row + pager styles |
| Modify `DiscoverLocalizationResources.cs` | ForYou strings |
| Modify `docs/PRODUCT_DESIGN.md` §7.4 | One sentence update |
| Smoke tests if bindings break |

---

### Task 1: Layout helper (TDD)

**Files:**
- Create: `src/MusicVault.App/Services/DiscoverForYouLayout.cs`
- Create: `tests/MusicVault.Tests/App/DiscoverForYouLayoutTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverForYouLayoutTests
{
    [Theory]
    [InlineData(500, 2)]
    [InlineData(799, 2)]
    [InlineData(800, 3)]
    [InlineData(1200, 3)]
    public void Columns_from_width(double width, int expected) =>
        Assert.Equal(expected, DiscoverForYouLayout.ColumnsForWidth(width));

    [Theory]
    [InlineData(2, 10)] // 2×5
    [InlineData(3, 12)] // 3×4
    public void PageSize_from_columns(int columns, int expected) =>
        Assert.Equal(expected, DiscoverForYouLayout.PageSizeForColumns(columns));
}
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverForYouLayoutTests" -r win-x64
```

- [ ] **Step 3: Implement**

```csharp
namespace MusicVault.App.Services;

public static class DiscoverForYouLayout
{
    public const double ThreeColumnMinWidth = 800;

    public static int ColumnsForWidth(double width) =>
        width >= ThreeColumnMinWidth ? 3 : 2;

    public static int PageSizeForColumns(int columns) =>
        columns >= 3 ? 12 : 10;

    public static int PageSizeForWidth(double width) =>
        PageSizeForColumns(ColumnsForWidth(width));
}
```

- [ ] **Step 4: Run — expect PASS**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverForYouLayoutTests" -r win-x64
```

- [ ] **Step 5: Commit only if user asked**

---

### Task 2: ViewModel — ForYou page + load

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs`
- Modify: `src/MusicVault.App/Localization/DiscoverLocalizationResources.cs`

- [ ] **Step 1: l10n (zh + en)**

```csharp
["Discover.ForYou.Title"] = "为你精选",
["Discover.ForYou.Subtitle"] = "根据口味挑选",
["Discover.ForYou.PrevPage"] = "上一页",
["Discover.ForYou.NextPage"] = "下一页",
["Discover.ForYou.MixedHotHint"] = "已混入热门",
["Discover.ForYou.Empty"] = "暂无曲目",
["Discover.ForYou.Retry"] = "重试",
// EN:
["Discover.ForYou.Title"] = "For you",
["Discover.ForYou.Subtitle"] = "Picked for your taste",
["Discover.ForYou.PrevPage"] = "Previous",
["Discover.ForYou.NextPage"] = "Next",
["Discover.ForYou.MixedHotHint"] = "Includes hot songs",
["Discover.ForYou.Empty"] = "No tracks yet",
["Discover.ForYou.Retry"] = "Retry",
```

- [ ] **Step 2: Add ForYou state (near former rail fields)**

```csharp
public ObservableCollection<OnlineTrackViewModel> ForYouTracks { get; } = [];

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanForYouPrevPage))]
[NotifyPropertyChangedFor(nameof(CanForYouNextPage))]
[NotifyPropertyChangedFor(nameof(ShowForYouSkeleton))]
[NotifyPropertyChangedFor(nameof(ShowForYouError))]
private bool _isLoadingForYou;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowForYouError))]
[NotifyPropertyChangedFor(nameof(ForYouErrorText))]
private string? _forYouError;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowForYouMixedHotHint))]
private bool _forYouUsedHotPad;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ForYouPageSize))]
private int _forYouColumns = 3;

private int _forYouOffset;
private CancellationTokenSource? _forYouLoadCts;

public int ForYouPageSize => DiscoverForYouLayout.PageSizeForColumns(ForYouColumns);

public string ForYouTitle => _l10n.Get("Discover.ForYou.Title");
public string ForYouSubtitle => _l10n.Get("Discover.ForYou.Subtitle");
public string ForYouPrevPageText => _l10n.Get("Discover.ForYou.PrevPage");
public string ForYouNextPageText => _l10n.Get("Discover.ForYou.NextPage");
public string ForYouMixedHotHintText => _l10n.Get("Discover.ForYou.MixedHotHint");
public string ForYouEmptyText => _l10n.Get("Discover.ForYou.Empty");
public string ForYouRetryText => _l10n.Get("Discover.ForYou.Retry");

public bool HasForYouTracks => ForYouTracks.Count > 0;
public bool ShowForYouSkeleton => ShowBrowseHub && IsLoadingForYou;
public bool ShowForYouError =>
    ShowBrowseHub && !IsLoadingForYou && !HasForYouTracks && !string.IsNullOrWhiteSpace(ForYouError);
public string ForYouErrorText => /* same LocalizeRailStatus pattern or ServiceMessageLocalizer */;
public bool ShowForYouMixedHotHint => ForYouUsedHotPad && HasForYouTracks;
public bool CanForYouPrevPage => !IsLoadingForYou && _forYouOffset > 0;
public bool CanForYouNextPage => !IsLoadingForYou && HasForYouTracks;
```

- [ ] **Step 3: LoadForYouAsync**

```csharp
public void SetForYouLayoutWidth(double width)
{
    var cols = DiscoverForYouLayout.ColumnsForWidth(width);
    if (cols == ForYouColumns)
        return;
    ForYouColumns = cols;
    // Reload current page with new page size (non-advance)
    if (ShowBrowseHub && !TestSessionProbe.IsActive)
        _ = LoadForYouAsync(advanceBatch: false);
}

private async Task LoadForYouAsync(bool advanceBatch, bool backward = false)
{
    if (TestSessionProbe.IsActive)
        return;

    _forYouLoadCts?.Cancel();
    _forYouLoadCts = new CancellationTokenSource();
    var ct = _forYouLoadCts.Token;
    IsLoadingForYou = true;
    ForYouError = null;
    ForYouPrevPageCommand.NotifyCanExecuteChanged();
    ForYouNextPageCommand.NotifyCanExecuteChanged();
    try
    {
        var page = ForYouPageSize;
        var exclude = advanceBatch || backward
            ? ForYouTracks.Select(t => t.Model.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray()
            : null;

        // Also prefer excluding current Hero batch when available
        var heroIds = HeroPromoTracks.Select(t => t.Model.Id).Where(id => !string.IsNullOrWhiteSpace(id));
        exclude = exclude == null
            ? heroIds.Distinct().ToArray()
            : exclude.Concat(heroIds).Distinct().ToArray();

        if (advanceBatch)
            _forYouOffset += page;
        else if (backward)
            _forYouOffset = Math.Max(0, _forYouOffset - page);
        else
            _forYouOffset = 0;

        var guess = await DiscoverHeroPromoLoader.LoadRailAsync(
            DiscoverRailSource.GuessYouLike, page, _forYouOffset, exclude, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested) return;

        ForYouUsedHotPad = false;
        var tracks = guess.ToList();
        if (tracks.Count < page)
        {
            var need = page - tracks.Count;
            var have = new HashSet<string>(tracks.Select(t => t.Id), StringComparer.Ordinal);
            var hot = await DiscoverHeroPromoLoader.LoadRailAsync(
                DiscoverRailSource.HotSongs,
                need + 5,
                _forYouOffset + DiscoverHeroPromoLoader.PoolTake,
                have.Concat(exclude ?? []).ToArray(),
                ct).ConfigureAwait(true);
            foreach (var t in hot)
            {
                if (have.Contains(t.Id)) continue;
                tracks.Add(t);
                have.Add(t.Id);
                ForYouUsedHotPad = true;
                if (tracks.Count >= page) break;
            }
        }

        if (tracks.Count == 0 && _forYouOffset > 0)
        {
            _forYouOffset = 0;
            // one retry from start without advance
            guess = await DiscoverHeroPromoLoader.LoadRailAsync(
                DiscoverRailSource.GuessYouLike, page, 0, null, ct).ConfigureAwait(true);
            tracks = guess.ToList();
        }

        ForYouTracks.Clear();
        foreach (var t in tracks.Take(page))
        {
            var vm = CreateHeroPromoTrackVm(t);
            if (vm == null) continue;
            ForYouTracks.Add(vm);
            _ = vm.EnsureCoverLoadedAsync(ct);
        }

        if (ForYouTracks.Count == 0)
            ForYouError = "Discover.ForYou.Empty";
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        ForYouError = ex.Message;
        ForYouTracks.Clear();
    }
    finally
    {
        IsLoadingForYou = false;
        OnPropertyChanged(nameof(HasForYouTracks));
        OnPropertyChanged(nameof(ShowForYouMixedHotHint));
        OnPropertyChanged(nameof(CanForYouPrevPage));
        OnPropertyChanged(nameof(CanForYouNextPage));
        ForYouPrevPageCommand.NotifyCanExecuteChanged();
        ForYouNextPageCommand.NotifyCanExecuteChanged();
    }
}

[RelayCommand(CanExecute = nameof(CanForYouNextPage))]
private async Task ForYouNextPageAsync() =>
    await LoadForYouAsync(advanceBatch: true).ConfigureAwait(true);

[RelayCommand(CanExecute = nameof(CanForYouPrevPage))]
private async Task ForYouPrevPageAsync() =>
    await LoadForYouAsync(advanceBatch: false, backward: true).ConfigureAwait(true);

[RelayCommand]
private async Task RetryForYouAsync() =>
    await LoadForYouAsync(advanceBatch: false).ConfigureAwait(true);
```

- [ ] **Step 4: Wire RefreshBrowseHubAsync**

```csharp
await Task.WhenAll(
    LoadHeroPromoAsync(forceChartsRefresh),
    LoadForYouAsync(advanceBatch: false)).ConfigureAwait(true);
```

Stop calling `LoadHotRailAsync` / `LoadGuessRailAsync` from hub refresh. Prefer **removing** Hot/Guess rail collections and commands from VM in this task if unused (cleaner); if risky, leave dead code and delete in Task 3 after XAML swap — **preferred: delete dual-rail UI bindings in Task 3 and remove unused VM rail API in same or this task**.

- [ ] **Step 5: Build**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64
```

Expected: 0 errors (XAML may still bind old HotRail until Task 3 — if build compiles XAML and fails, do Task 2+3 together or temporarily keep stub props).

**If Avalonia compile binds missing props:** keep thin stub properties until Task 3 deletes bindings, OR implement Task 3 in the same agent session immediately after Step 4.

- [ ] **Step 6: Commit only if user asked**

---

### Task 3: XAML + styles + width hook

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml`
- Modify: `src/MusicVault.App/Views/Pages/SearchPageView.axaml.cs`
- Modify: `src/MusicVault.App/Styles/Components/DiscoverDownloads.axaml`

- [ ] **Step 1: Replace dual-rail `Grid Grid.Row="1"` block** with:

```xml
<Panel Grid.Row="1"
       Margin="{DynamicResource PageContentHorizontalMargin}"
       Classes="discover-foryou-host"
       x:Name="ForYouHost"
       PointerEntered="OnForYouHostPointerEntered"
       PointerExited="OnForYouHostPointerExited">
  <DockPanel LastChildFill="True" Margin="0,12,0,12">
    <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8" LastChildFill="True">
      <StackPanel>
        <TextBlock Text="{Binding ForYouTitle}" Classes="discover-browse-section-title"/>
        <TextBlock Text="{Binding ForYouSubtitle}" Classes="text-caption"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   Margin="0,2,0,0"/>
        <TextBlock Text="{Binding ForYouMixedHotHintText}" Classes="text-caption"
                   IsVisible="{Binding ShowForYouMixedHotHint}"
                   Opacity="0.75" Margin="0,2,0,0"/>
      </StackPanel>
    </DockPanel>

    <Panel ClipToBounds="True">
      <ItemsControl ItemsSource="{Binding ForYouTracks}"
                    Classes="discover-foryou-list"
                    IsVisible="{Binding HasForYouTracks}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <!-- Columns bound: use code-behind to set UniformGrid.Columns on layout, or:
                 Wrap a UniformGrid named ForYouGrid and set Columns in SizeChanged -->
            <UniformGrid x:Name="ForYouGrid" Columns="3" Rows="4"/>
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:OnlineTrackViewModel">
            <Border Classes="discover-foryou-cell">
              <Grid ColumnDefinitions="Auto,*" Margin="8,6">
                <Panel Width="48" Height="48" Margin="0,0,10,0">
                  <Border CornerRadius="{DynamicResource RadiusControl}" ClipToBounds="True">
                    <Image Source="{Binding CoverBitmap}" Stretch="UniformToFill"
                           IsVisible="{Binding HasCoverThumb}"/>
                  </Border>
                  <Button Classes="discover-foryou-play"
                          Command="{Binding PreviewCommand}"
                          IsVisible="{Binding $parent[Border].IsPointerOver}"
                          HorizontalAlignment="Center" VerticalAlignment="Center"
                          automation:AutomationProperties.Name="{Binding Title}">
                    <TextBlock Text="{StaticResource IconGlyphPlay}" Classes="icon-sm"/>
                  </Button>
                </Panel>
                <DockPanel Grid.Column="1" LastChildFill="True">
                  <Button DockPanel.Dock="Right"
                          Classes="flat-btn discover-foryou-fav"
                          Command="{Binding ToggleOnlineFavoriteCommand}"
                          ToolTip.Tip="{Binding OnlineFavoriteToolTip}"
                          IsVisible="{Binding $parent[Border].IsPointerOver}"
                          Margin="6,0,0,0">
                    <Panel>
                      <TextBlock Text="{StaticResource IconGlyphHeart}" Classes="icon-xs"
                                 IsVisible="{Binding !IsOnlineFavorite}"/>
                      <TextBlock Text="{StaticResource IconGlyphHeartFill}" Classes="icon-xs"
                                 IsVisible="{Binding IsOnlineFavorite}"/>
                    </Panel>
                  </Button>
                  <Button Classes="flat-btn discover-foryou-rowhit"
                          Command="{Binding PreviewCommand}"
                          HorizontalAlignment="Stretch"
                          HorizontalContentAlignment="Left">
                    <StackPanel Spacing="2">
                      <TextBlock Text="{Binding Title}" Classes="text-body"
                                 TextTrimming="CharacterEllipsis"/>
                      <TextBlock Text="{Binding Artist}" Classes="text-caption"
                                 Foreground="{DynamicResource TextSecondaryBrush}"
                                 TextTrimming="CharacterEllipsis"/>
                    </StackPanel>
                  </Button>
                </DockPanel>
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <!-- skeleton / error / retry using ShowForYou* -->

      <!-- Right-edge pager: visible when ForYouPagerVisible (VM or code-behind) -->
      <StackPanel Classes="discover-foryou-pager"
                  HorizontalAlignment="Right"
                  VerticalAlignment="Center"
                  IsVisible="{Binding ForYouPagerVisible}"
                  Spacing="8"
                  Margin="0,0,4,0">
        <Button Classes="discover-foryou-page-btn"
                Command="{Binding ForYouPrevPageCommand}"
                ToolTip.Tip="{Binding ForYouPrevPageText}"
                Content="{StaticResource IconGlyphChevronUp}"/>
        <Button Classes="discover-foryou-page-btn"
                Command="{Binding ForYouNextPageCommand}"
                ToolTip.Tip="{Binding ForYouNextPageText}"
                Content="{StaticResource IconGlyphChevronDown}"/>
      </StackPanel>
    </Panel>
  </DockPanel>
</Panel>
```

**Pointer / IsPointerOver note:** Avalonia DataTemplate `$parent[Border].IsPointerOver` may not work — prefer `Classes` with style `Border.discover-foryou-cell:pointerover Button.discover-foryou-play { IsVisible=True }` default hidden. Use styles:

```xml
<Style Selector="Border.discover-foryou-cell Button.discover-foryou-play">
  <Setter Property="IsVisible" Value="False"/>
  <Setter Property="Opacity" Value="0.92"/>
</Style>
<Style Selector="Border.discover-foryou-cell:pointerover Button.discover-foryou-play">
  <Setter Property="IsVisible" Value="True"/>
</Style>
<!-- same for discover-foryou-fav; optional: always show filled heart when IsOnlineFavorite via second style -->
```

- [ ] **Step 2: Code-behind**

```csharp
private void OnForYouHostPointerEntered(object? sender, PointerEventArgs e)
{
    if (DataContext is SearchPageViewModel vm)
        vm.ForYouPagerVisible = true;
}

private void OnForYouHostPointerExited(object? sender, PointerEventArgs e)
{
    if (DataContext is SearchPageViewModel vm)
        vm.ForYouPagerVisible = false;
}

// In constructor or OnAttachedToVisualTree: ForYouHost.SizeChanged += ...
private void OnForYouHostSizeChanged(object? sender, SizeChangedEventArgs e)
{
    if (DataContext is not SearchPageViewModel vm) return;
    vm.SetForYouLayoutWidth(e.NewSize.Width);
    if (this.FindControl<UniformGrid>("ForYouGrid") is { } grid)
        grid.Columns = vm.ForYouColumns;
}
```

Add `[ObservableProperty] private bool _forYouPagerVisible;` on VM.

If `FindControl` on template child fails, set Columns via binding with an `IntToColumns` hack or recreate panel when `ForYouColumns` changes — simplest reliable approach: **don't nest UniformGrid in ItemsPanelTemplate with x:Name**; use:

```xml
<ItemsControl.ItemsPanel>
  <ItemsPanelTemplate>
    <UniformGrid Columns="{Binding $parent[UserControl].((pvm:SearchPageViewModel)DataContext).ForYouColumns}"/>
  </ItemsPanelTemplate>
</ItemsControl.ItemsPanel>
```

Discover root is `DiscoverRoot` UserControl — bind:

```xml
Columns="{Binding #DiscoverRoot.((pvm:SearchPageViewModel)DataContext).ForYouColumns}"
```

Rows: set `Rows="{Binding #DiscoverRoot.((pvm:SearchPageViewModel)DataContext).ForYouRows}"` where `ForYouRows => ForYouColumns >= 3 ? 4 : 5`.

- [ ] **Step 3: Styles** (pager buttons opaque, circular, SurfaceElevated; cell hover background soft)

- [ ] **Step 4: Delete** HotRail / GuessRail XAML blocks entirely.

- [ ] **Step 5: Build + publish**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64
.\scripts\publish-with-api.ps1 -Restart
```

Manual: 为你精选多列；悬停行播放+收藏；区悬停右侧翻页；无双栏；无整页滚动；Hero 仍在。

- [ ] **Step 6: Commit only if user asked**

---

### Task 4: Docs + tests + cleanup

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` §7.4
- Remove unused Hot/Guess rail members from VM if still present
- Run tests

- [ ] **Step 1: PRODUCT_DESIGN** — browse hub = 广告轮播 + 为你精选多列（悬停翻页/收藏）；无页面滚动。

- [ ] **Step 2: Tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~DiscoverForYouLayoutTests|FullyQualifiedName~DiscoverTrackBatchSlicerTests|FullyQualifiedName~DiscoverBrowseHubStateTests|FullyQualifiedName~MainWindowHeadlessSmokeTests" -r win-x64
```

Expected: PASS.

- [ ] **Step 3: Final publish**

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] **Step 4: Commit only if user asked**

---

## Spec coverage

| Spec | Task |
|------|------|
| Keep Hero | Task 3 (don't remove) |
| Single 为你精选 multicolumn | Tasks 2–3 |
| 2/3 columns by width | Tasks 1–3 |
| ~10–12 per page | Task 1–2 |
| Hover play + favorite | Task 3 |
| Right hover pager | Task 3 |
| No page/column scroll | Task 3 |
| Guess + hot pad | Task 2 |
| Exclude hero ids | Task 2 |
| Search session hide hub | unchanged |
| PRODUCT_DESIGN | Task 4 |

## Consistency notes

- Commands: `ForYouNextPage` / `ForYouPrevPage` / `RetryForYou`  
- Collection: `ForYouTracks`  
- Layout: `DiscoverForYouLayout.ColumnsForWidth` / `PageSizeForColumns`  
- Dual-rail UI removed; prefer VM cleanup in Task 2 or 4
