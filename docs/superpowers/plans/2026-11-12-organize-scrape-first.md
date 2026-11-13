# Organize scrape-first bottom-dock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild 整理工具 so metadata scrape is the primary path: slim top load row, full-width queue, bottom dock for fields/source/preset/scope + Start scrape; rename/dedupe under More.

**Architecture:** Mostly XAML + VM chrome: keep `BatchOnlineScrapeService` and existing scrape commands. Introduce a tiny pure helper for “basic vs extended” field grouping so the dock stays compact. Empty state gets a real Load CTA. Progress UI: keep confirm + one progress surface (modal) and remove duplicate page-level indeterminate bar while scrape modal is shown (or hide page bar when modal progress is active—pick one in Task 3).

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-12-organize-scrape-first-design.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

**Publish:** After UI tasks, `.\scripts\publish-with-api.ps1 -Restart`. Tests: `dotnet test ... -r win-x64`.

---

## File map

| File | Role |
|------|------|
| Create `src/MusicVault.App/Services/OrganizeScrapeFieldGroups.cs` | Basic vs extended scrape field keys |
| Create `tests/MusicVault.Tests/App/OrganizeScrapeFieldGroupsTests.cs` | TDD |
| Modify `AudioToolsPageViewModel.cs` | Empty CTA; More labels; `ShowExtendedScrapeFields`; dock visibility helpers |
| Modify `AudioToolsPageView.axaml` | Top slim + bottom dock; More flyout |
| Modify `Styles/Components/Organize.axaml` | Bottom dock styles |
| Modify `LocalizationResources.cs` (Organize.* keys) | Empty copy, More, Extended fields |
| Modify `docs/PRODUCT_DESIGN.md` §7.6 / matrix | Align with scrape-first |
| Modify `docs/product/organize-tools-redesign.md` | Note defer four-step; point to new spec |

---

### Task 1: Field grouping helper (TDD)

**Files:**
- Create: `src/MusicVault.App/Services/OrganizeScrapeFieldGroups.cs`
- Create: `tests/MusicVault.Tests/App/OrganizeScrapeFieldGroupsTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class OrganizeScrapeFieldGroupsTests
{
    [Fact]
    public void Basic_fields_are_title_artist_album_year_cover_lyrics()
    {
        var basic = OrganizeScrapeFieldGroups.Basic;
        Assert.Equal(
            new[] { "title", "artist", "album", "year", "cover", "lyrics" },
            basic);
    }

    [Fact]
    public void Extended_fields_are_albumArtist_genre_track_composer()
    {
        Assert.Equal(
            new[] { "albumArtist", "genre", "trackNumber", "composer" },
            OrganizeScrapeFieldGroups.Extended);
    }

    [Fact]
    public void Basic_and_extended_are_disjoint_and_cover_all()
    {
        var all = OrganizeScrapeFieldGroups.Basic
            .Concat(OrganizeScrapeFieldGroups.Extended)
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(all.Distinct().Count(), all.Length);
        Assert.Equal(10, all.Length);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~OrganizeScrapeFieldGroupsTests" -r win-x64
```

Expected: compile error — type missing.

- [ ] **Step 3: Implement**

```csharp
namespace MusicVault.App.Services;

/// <summary>刮削底栏：默认展示基础字段；扩展字段折叠。</summary>
public static class OrganizeScrapeFieldGroups
{
    public static IReadOnlyList<string> Basic { get; } =
        ["title", "artist", "album", "year", "cover", "lyrics"];

    public static IReadOnlyList<string> Extended { get; } =
        ["albumArtist", "genre", "trackNumber", "composer"];
}
```

- [ ] **Step 4: Run — expect PASS**

Same `dotnet test` filter; Expected: Passed 3.

- [ ] **Step 5: Commit** — skip unless user asks.

---

### Task 2: ViewModel — empty CTA + More + extended toggle

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/AudioToolsPageViewModel.cs`
- Modify: `src/MusicVault.App/Localization/LocalizationResources.cs` (zh + en Organize keys)

- [ ] **Step 1: Add localization keys** (both language dictionaries)

```csharp
["Organize.More"] = "更多",
["Organize.MoreFields"] = "更多字段",
["Organize.Empty.Action"] = "载入到队列",
["Organize.Empty.Description"] = "先载入整理队列，再在底部选择字段并开始刮削。也可从曲库勾选歌曲后加入整理。",
// EN:
["Organize.More"] = "More",
["Organize.MoreFields"] = "More fields",
["Organize.Empty.Action"] = "Load queue",
["Organize.Empty.Description"] = "Load a queue first, then pick fields in the bottom dock and start scraping. You can also add tracks from the library.",
```

Update existing `Organize.Empty.Description` values to the new strings above (replace old “列表上方进行补标签…” copy).

- [ ] **Step 2: VM properties**

In `AudioToolsPageViewModel`:

```csharp
public string MoreMenuLabel => _l10n.Get("Organize.More");
public string MoreFieldsLabel => _l10n.Get("Organize.MoreFields");
public string EmptyActionLabel => _l10n.Get("Organize.Empty.Action");

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowExtendedScrapeFieldsPanel))]
private bool _showExtendedScrapeFields;

public bool ShowExtendedScrapeFieldsPanel =>
    HasWorkingSet && ShowExtendedScrapeFields && !IsDuplicateReviewActive;

public bool ShowScrapeDock =>
    HasWorkingSet && !IsDuplicateReviewActive;

// Change:
public bool ShowWorkSetEmptyAction => true; // was false
```

Wire `EmptyActionLabel` / `MoreMenuLabel` / `MoreFieldsLabel` into `RefreshLocalizedStrings` `OnPropertyChanged` list.

Ensure `NotifyWorkSetChrome` (or equivalent after load/clear) also raises `ShowScrapeDock` and `ShowExtendedScrapeFieldsPanel`.

Keep `RenameFilesCommand`, `OpenSongDeduplicateCommand`, `CanRenameFilesFromToolbar`, `CanOperateOnWorkSet` as-is for the More flyout.

Empty state action: bind `ActionCommand="{Binding LoadWorkingSetCommand}"` and `ActionText="{Binding EmptyActionLabel}"` in Task 3 XAML.

- [ ] **Step 3: Build App project**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64 --no-self-contained
```

Expected: success (warnings OK).

- [ ] **Step 4: Commit** — skip unless user asks.

---

### Task 3: XAML — slim top + bottom dock

**Files:**
- Modify: `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml`
- Modify: `src/MusicVault.App/Styles/Components/Organize.axaml`

- [ ] **Step 1: Replace top `organize-toolbar` StackPanel**

Remove the three stacked rows (rename/dedupe inline, scrape row, full field WrapPanel, page-level scrape ProgressBar block).

New top dock content (structure):

```xml
<DockPanel DockPanel.Dock="Top" Classes="organize-topbar">
  <StackPanel Orientation="Horizontal" Spacing="8" Classes="transfers-toolbar-flow">
    <!-- Library ComboBox + Load (existing bindings) -->
    <Button Content="{Binding LoadButtonLabel}" Classes="primary btn-toolbar"
            Command="{Binding LoadWorkingSetCommand}"/>
  </StackPanel>
  <TextBlock Text="{Binding WorkingSetSummary}" Classes="discover-results-summary"
             VerticalAlignment="Center" Margin="12,0,0,0"
             IsVisible="{Binding HasWorkingSet}"/>
  <Button Classes="flat-btn btn-toolbar" HorizontalAlignment="Right"
          Content="{Binding MoreMenuLabel}"
          automation:AutomationProperties.Name="{Binding MoreMenuLabel}">
    <Button.Flyout>
      <MenuFlyout>
        <MenuItem Header="{Binding FormatLabel}" IsEnabled="False"/>
        <!-- optional: nest template Combo via separate small panel; or keep template Combo inside MenuItem header area -->
        <MenuItem Header="{Binding RenameFilesButtonLabel}"
                  Command="{Binding RenameFilesCommand}"
                  IsEnabled="{Binding CanRenameFilesFromToolbar}"/>
        <MenuItem Header="{Binding DedupeButtonLabel}"
                  Command="{Binding OpenSongDeduplicateCommand}"
                  IsEnabled="{Binding CanOperateOnWorkSet}"/>
      </MenuFlyout>
    </Button.Flyout>
  </Button>
</DockPanel>
```

**Rename template:** Put `FileNameTemplateOptions` ComboBox **inside** the More flyout as a second row above Rename (Avalonia MenuFlyout can host a custom `MenuItem` with ComboBox in Header, or add a small modal later). Minimal acceptable: More → Rename uses **currently selected** template; add a compact ComboBox next to Rename inside a `StackPanel` hosted by:

```xml
<MenuItem>
  <MenuItem.Header>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <ComboBox ItemsSource="{Binding FileNameTemplateOptions}"
                SelectedItem="{Binding SelectedFileNameTemplateOption}"
                Width="160" Classes="field-select"/>
      <TextBlock Text="{Binding RenameFilesButtonLabel}" VerticalAlignment="Center"/>
    </StackPanel>
  </MenuItem.Header>
  <MenuItem.Command>
    <Binding Path="RenameFilesCommand"/>
  </MenuItem.Command>
</MenuItem>
```

If Header+Command on same MenuItem is awkward in Avalonia, use two MenuItems: one disabled header “模板” with Combo in submenu content panel, then Rename. Engineer verifies click-to-rename still works.

Keep existing queue chrome (summary + select all / clear), table header, list, duplicate review UI **unchanged**.

- [ ] **Step 2: Add bottom scrape dock**

After the list `Grid` (sibling inside outer `DockPanel`), before closing `DockPanel`:

```xml
<Border DockPanel.Dock="Bottom" Classes="organize-scrape-dock"
        IsVisible="{Binding ShowScrapeDock}">
  <StackPanel Spacing="8">
    <WrapPanel Classes="organize-scrape-fields-wrap">
      <!-- Basic six CheckBoxes only: Title Artist Album Year Cover Lyrics -->
      <CheckBox Content="{Binding FieldTitleLabel}" IsChecked="{Binding ScrapeUpdateTitle}" Classes="choice-check"/>
      <!-- ... artist album year cover lyrics ... -->
      <ToggleButton Content="{Binding MoreFieldsLabel}" Classes="browse-filter"
                    IsChecked="{Binding ShowExtendedScrapeFields}"/>
    </WrapPanel>
    <WrapPanel Classes="organize-scrape-fields-wrap"
               IsVisible="{Binding ShowExtendedScrapeFieldsPanel}">
      <!-- albumArtist genre track composer -->
    </WrapPanel>
    <DockPanel LastChildFill="False">
      <StackPanel Orientation="Horizontal" Spacing="8" Classes="transfers-toolbar-flow">
        <TextBlock Text="{Binding DataSourceLabel}" Classes="organize-field-label" VerticalAlignment="Center"/>
        <ComboBox ... ScrapePlatformLabels / ScrapePlatformIndex .../>
        <controls:IntSegmentedBar SegmentLabels="{Binding ScrapePresetSegmentLabels}"
                                  SelectedIndex="{Binding ScrapePresetIndex}"/>
        <TextBlock Text="{Binding ScopeLabel}" Classes="organize-field-label" VerticalAlignment="Center"/>
        <controls:IntSegmentedBar SegmentLabels="{Binding WorkSetScopeSegmentLabels}"
                                  SelectedIndex="{Binding WorkSetScopeIndex}"/>
        <Button Classes="flat-btn" Command="{Binding CancelBatchScrapeCommand}"
                IsVisible="{Binding IsBatchScrapeRunning}"
                Content="{Binding CancelScrapeLabel}"/>
      </StackPanel>
      <Button DockPanel.Dock="Right" Classes="primary btn-toolbar"
              Command="{Binding StartBatchScrapeCommand}"
              IsEnabled="{Binding CanStartBatchScrape}"
              Content="{Binding StartScrapeLabel}"
              MinWidth="{DynamicResource DialogButtonMinWidth}"/>
    </DockPanel>
  </StackPanel>
</Border>
```

**Progress:** Delete the old top `IsBatchScrapeRunning` ProgressBar+caption block. Rely on existing batch scrape progress modal + per-row `IsRunning` bars. If modal is missing in some path, keep a single indeterminate bar **inside** the bottom dock only (`IsVisible="{Binding IsBatchScrapeRunning}"`).

- [ ] **Step 3: Empty state CTA**

```xml
<controls:EmptyStateView IsVisible="{Binding ShowWorkSetEmptyState}"
                         Title="{Binding WorkSetEmptyTitle}"
                         Description="{Binding WorkSetEmptyDescription}"
                         ShowAction="{Binding ShowWorkSetEmptyAction}"
                         ActionText="{Binding EmptyActionLabel}"
                         ActionCommand="{Binding LoadWorkingSetCommand}"
                         IconGlyph="{StaticResource IconGlyphMusic}"/>
```

- [ ] **Step 4: Styles in `Organize.axaml`**

```xml
<Style Selector="Border.organize-scrape-dock">
  <Setter Property="Padding" Value="12,10"/>
  <Setter Property="Margin" Value="0,8,0,0"/>
  <Setter Property="Background" Value="{DynamicResource SurfaceElevatedBrush}"/>
  <Setter Property="CornerRadius" Value="{DynamicResource RadiusControl}"/>
  <Setter Property="BorderBrush" Value="{DynamicResource BorderSubtleBrush}"/>
  <Setter Property="BorderThickness" Value="1"/>
</Style>
```

Use existing brushes/tokens if names differ—match nearest Surface/Border already used on organize page.

- [ ] **Step 5: Build + smoke**

```powershell
dotnet build src/MusicVault.App/MusicVault.App.csproj -c Release -r win-x64 --no-self-contained
.\scripts\publish-with-api.ps1 -Restart
```

Manual: 整理工具 → empty shows Load → Load → bottom dock appears → More has rename/dedupe → Start scrape still opens confirm/progress/result.

- [ ] **Step 6: Commit** — skip unless user asks.

---

### Task 4: Docs sync

**Files:**
- Modify: `docs/PRODUCT_DESIGN.md` (shell map §7.6 + capability matrix row for 整理工具 / 元数据)
- Modify: `docs/product/organize-tools-redesign.md` (top note)

- [ ] **Step 1: PRODUCT_DESIGN**

Update AudioTools blurb to: 载入队列 → 底栏刮削为主；重命名/去重在「更多」。  
Matrix: 批量刮削 = 完整（底栏）；整理文件/去重 = 完整（更多入口）.

- [ ] **Step 2: organize-tools-redesign.md**

Add at top:

```markdown
> **2026-11-12：** 完整四步流水线延后。现行实现以  
> `docs/superpowers/specs/2026-11-12-organize-scrape-first-design.md` 为准（刮削优先 + 底栏）。
```

- [ ] **Step 3: Spec status**

In `docs/superpowers/specs/2026-11-12-organize-scrape-first-design.md`, set `**状态：** 实现中` → after publish `已落地` if done in same PR session.

- [ ] **Step 4: Commit** — skip unless user asks.

---

### Task 5: Final verify

- [ ] **Step 1: Tests**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~OrganizeScrapeFieldGroupsTests" -r win-x64
```

- [ ] **Step 2: Publish restart** (if not done in Task 3)

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

- [ ] **Step 3: Checklist vs spec §7**

1. No dual scrap/rename/dedupe tool strips on top.  
2. Dock appears after load.  
3. Scrape path works.  
4. More → rename/dedupe.  
5. Empty CTA works.  
6. Docs updated.

---

## Spec coverage

| Spec § | Task |
|--------|------|
| §1 scrape-first goals | 3 |
| §3 More secondary | 3 |
| §4.1 top row | 3 |
| §4.2 queue | 3 (keep) |
| §4.3 bottom dock + chips | 1+2+3 |
| §5 progress consolidate | 3 |
| §6 empty CTA | 2+3 |
| §7 acceptance | 5 |
| §8 docs | 4 |

## Self-review notes

- No TBD placeholders.  
- Commit steps explicitly skippable.  
- Field keys in helper are documentation for grouping only; XAML still binds concrete `ScrapeUpdate*` booleans (helper not required at runtime unless VM later maps by key—acceptable YAGNI: helper documents the split for tests + future).
