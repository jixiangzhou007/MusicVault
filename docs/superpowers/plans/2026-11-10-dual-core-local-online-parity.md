# Dual-core local + online parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship four publishable slices so local browsing feels like a library and Discover feels like a listening shell, with the same bottom player.

**Architecture:** Pure helpers for folder path math, Discover CTA/failure merge, and missing-metadata predicates; wire into existing `TracksPageViewModel` / `VirtualTrackCollection` / `SearchPageViewModel` without new top-nav items. Reuse `GetByLibraryScopeAsync` pattern (folder absolute path as scope root) + non-recursive split; reuse `OnlineTrackViewModel.PreviewCommand` + `OnlinePreviewQueue` for recognize play/next.

**Tech Stack:** .NET 8, Avalonia, CommunityToolkit.Mvvm, SQLite `TrackRepository`, xUnit, existing Discover browse hub.

**Spec:** `docs/superpowers/specs/2026-11-10-dual-core-local-online-parity-design.md`

**Commits:** Only if the user explicitly asks; otherwise skip commit steps.

---

## File map

| File | Role |
|------|------|
| Create `src/MusicVault.App/Services/LibraryFolderNavigator.cs` | Relative child folders + direct (non-recursive) tracks under a folder |
| Create `tests/MusicVault.Tests/App/LibraryFolderNavigatorTests.cs` | Pure path/split tests |
| Modify `TrackRepository.cs` | Optional: `GetPathsUnderFolderPrefixAsync` if navigator needs cheap path list; else reuse scope query |
| Modify `VirtualTrackCollection.cs` | `SetFolderFilter(libraryId, folderAbsolutePath)` — scope = that folder (for **recursive** playback queue of “this folder and below” OR direct-only list — see Task 2) |
| Modify `TracksPageViewModel.cs` + `TracksPageView.axaml` | Browse mode Songs \| Folders; breadcrumb; folder list; enter/up |
| Modify `LibraryLocalizationResources.cs` | Folder browse strings (zh/en) |
| Create `src/MusicVault.App/Services/DiscoverPrimaryCta.cs` | Pure: which primary CTA kind given recent play / browse readiness |
| Create `tests/MusicVault.Tests/App/DiscoverPrimaryCtaTests.cs` | CTA priority tests |
| Create `src/MusicVault.App/Services/DiscoverSourceFailureBanner.cs` | Merge health + partial search failure into one banner model |
| Modify `SearchPageViewModel.cs` + `SearchPageView.axaml` | Primary CTA chrome; single failure banner; recognize play strip |
| Create `src/MusicVault.Services/Metadata/TrackMissingMetadata.cs` | `HasCoverSignal` / `HasLyricsSignal` predicates |
| Modify `TrackMetadataHelper.cs` | Write `CoverHash` when embedded pictures exist (scan path) |
| Modify `TrackRepository` + `VirtualTrackCollection` | Missing-cover / missing-lyrics filters |
| Modify `TrackItemViewModel` + `SharedDataTemplates.axaml` | Row tags for missing cover/lyrics |
| Modify `OnlinePreviewQueue.cs` + `PlayerBarViewModel.cs` | `WithPlayNext(OnlineTrack)` insert-after-current |
| Modify `docs/PRODUCT_DESIGN.md` | Mark four capabilities current after M4 |

---

## Milestone M1 — Slice 1: Local folder browse (核 A)

### Task 1: LibraryFolderNavigator (TDD)

**Files:**
- Create: `src/MusicVault.App/Services/LibraryFolderNavigator.cs`
- Create: `tests/MusicVault.Tests/App/LibraryFolderNavigatorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using MusicVault.App.Services;
using MusicVault.Core.Models;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class LibraryFolderNavigatorTests
{
    [Fact]
    public void Split_lists_immediate_child_folders_and_direct_tracks_only()
    {
        var root = Normalize(@"D:\Music");
        var tracks = new[]
        {
            T(root, @"流行\a.mp3"),
            T(root, @"流行\周杰伦\b.mp3"),
            T(root, @"古典\c.mp3"),
            T(root, @"root-song.mp3"),
        };

        var split = LibraryFolderNavigator.Split(root, currentFolderAbsolute: root, tracks);

        Assert.Equal(new[] { "流行", "古典" }.OrderBy(x => x), split.ChildFolders.OrderBy(x => x));
        Assert.Single(split.DirectTracks);
        Assert.EndsWith("root-song.mp3", split.DirectTracks[0].FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Split_inside_child_is_non_recursive()
    {
        var root = Normalize(@"D:\Music");
        var pop = LibraryFolderNavigator.Combine(root, "流行");
        var tracks = new[]
        {
            T(root, @"流行\a.mp3"),
            T(root, @"流行\周杰伦\b.mp3"),
        };

        var split = LibraryFolderNavigator.Split(root, pop, tracks);

        Assert.Equal(new[] { "周杰伦" }, split.ChildFolders);
        Assert.Single(split.DirectTracks);
        Assert.EndsWith("a.mp3", split.DirectTracks[0].FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Breadcrumb_segments_from_root()
    {
        var root = Normalize(@"D:\Music");
        var deep = LibraryFolderNavigator.Combine(root, "流行", "周杰伦");
        var crumbs = LibraryFolderNavigator.Breadcrumb(root, deep, libraryDisplayName: "Music");
        Assert.Equal(3, crumbs.Count);
        Assert.Equal("Music", crumbs[0].Label);
        Assert.Equal("流行", crumbs[1].Label);
        Assert.Equal("周杰伦", crumbs[2].Label);
        Assert.Equal(root, crumbs[0].FolderAbsolutePath);
    }

    private static string Normalize(string p) =>
        Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static Track T(string root, string relative) =>
        new() { FilePath = Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar)) };
}
```

- [ ] **Step 2: Run test — expect FAIL (type missing)**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~LibraryFolderNavigatorTests" --no-restore
```

- [ ] **Step 3: Implement navigator**

```csharp
namespace MusicVault.App.Services;

public readonly record struct FolderBreadcrumb(string Label, string FolderAbsolutePath);

public sealed record FolderSplitResult(
    IReadOnlyList<string> ChildFolders,
    IReadOnlyList<Track> DirectTracks);

public static class LibraryFolderNavigator
{
    public static string Combine(string libraryRoot, params string[] segments)
    {
        var path = Normalize(libraryRoot);
        foreach (var s in segments)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            path = Normalize(Path.Combine(path, s.Trim()));
        }
        return path;
    }

    public static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static FolderSplitResult Split(
        string libraryRoot,
        string currentFolderAbsolute,
        IReadOnlyList<Track> tracksUnderLibrary)
    {
        var root = Normalize(libraryRoot);
        var current = Normalize(currentFolderAbsolute);
        if (!IsUnderOrEqual(current, root))
            return new FolderSplitResult([], []);

        var childSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var direct = new List<Track>();

        foreach (var t in tracksUnderLibrary)
        {
            var file = Normalize(t.FilePath);
            if (!IsUnderOrEqual(file, current) &&
                !file.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !file.StartsWith(current + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;

            var parent = Normalize(Path.GetDirectoryName(file) ?? "");
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                direct.Add(t);
                continue;
            }

            var rel = Path.GetRelativePath(current, parent);
            if (rel is "." or "" || rel.StartsWith("..", StringComparison.Ordinal))
                continue;
            var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (!string.IsNullOrWhiteSpace(first))
                childSet.Add(first);
        }

        return new FolderSplitResult(
            childSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            direct);
    }

    public static IReadOnlyList<FolderBreadcrumb> Breadcrumb(
        string libraryRoot, string currentFolderAbsolute, string libraryDisplayName)
    {
        var root = Normalize(libraryRoot);
        var current = Normalize(currentFolderAbsolute);
        var list = new List<FolderBreadcrumb>
        {
            new(string.IsNullOrWhiteSpace(libraryDisplayName) ? Path.GetFileName(root) : libraryDisplayName, root)
        };
        if (string.Equals(root, current, StringComparison.OrdinalIgnoreCase))
            return list;

        var rel = Path.GetRelativePath(root, current);
        if (rel.StartsWith("..", StringComparison.Ordinal))
            return list;

        var acc = root;
        foreach (var seg in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(seg) || seg == ".") continue;
            acc = Combine(acc, seg);
            list.Add(new FolderBreadcrumb(seg, acc));
        }
        return list;
    }

    private static bool IsUnderOrEqual(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Re-run tests — expect PASS**

---

### Task 2: Tracks page folder browse UI + VM

**Files:**
- Modify: `src/MusicVault.App/ViewModels/Pages/TracksPageViewModel.cs`
- Modify: `src/MusicVault.App/Views/Pages/TracksPageView.axaml`
- Modify: `src/MusicVault.App/Localization/LibraryLocalizationResources.cs`
- Modify: `src/MusicVault.App/ViewModels/VirtualTrackCollection.cs` (only if folder mode lists tracks via collection; preferred: VM-owned `FolderChildItems` + `FolderTrackItems` for non-recursive, keep `Tracks` for Songs mode)

**Behavior contract (lock):**
- Toolbar segmented / two `ToggleButton`: **歌曲** (`BrowseModeIndex=0`) | **文件夹** (`=1`).
- Folders mode requires a **single selected library** with non-empty `RootPath`. If「全部」or favorites: show empty hint「请选择一个音乐库再按文件夹浏览」+ CTA `OpenLibrarySources` / select first library.
- Load: `GetAllUnderLibraryScopeAsync(libraryId, root)` once per library enter (cache in `_folderTracksCache`); then `LibraryFolderNavigator.Split` for current folder.
- UI: breadcrumb buttons; list of folders (double-click / EnterFolder); list of direct tracks below (same `LibraryTrackRowTemplate` via a small `FolderTrackRow` VM or reuse `TrackItemViewModel`).
- Double-tap track → existing `PlayTrackCommand`. Playback queue when in folder mode: **current folder recursive** (all tracks under current path from cache, ordered) so「下一首」不跳出子树——实现 `ResolveVirtualListQueueAsync` 旁路：`TracksPageViewModel.GetFolderPlaybackScope()`。
- Empty folder: short copy + link to scan.

- [ ] **Step 1: Add localization keys**

```csharp
["Library.Tracks.BrowseMode.Songs"] = "歌曲",
["Library.Tracks.BrowseMode.Folders"] = "文件夹",
["Library.Tracks.Folder.PickLibrary"] = "请选择一个音乐库，再按文件夹浏览",
["Library.Tracks.Folder.Empty"] = "此文件夹没有歌曲。可扫描音乐库或换一层目录。",
["Library.Tracks.Folder.Up"] = "上级",
```

(and English counterparts in the same file’s EN section)

- [ ] **Step 2: VM state**

```csharp
public ObservableCollection<FolderBrowseItemViewModel> FolderChildren { get; } = [];
public ObservableCollection<TrackItemViewModel> FolderDirectTracks { get; } = [];
public ObservableCollection<FolderBreadcrumbItemViewModel> FolderBreadcrumbs { get; } = [];

[ObservableProperty] private int _browseModeIndex; // 0 songs, 1 folders
[ObservableProperty] private string? _currentFolderAbsolute;

public bool ShowSongsList => BrowseModeIndex == 0;
public bool ShowFolderBrowser => BrowseModeIndex == 1;
```

`FolderBrowseItemViewModel`: `Name`, `AbsolutePath`, command enter.  
On `BrowseModeIndex` / library change → `RefreshFolderBrowserAsync`.

- [ ] **Step 3: Wire axaml**

In right-pane toolbar (near favorites toggle), add mode toggles. Below header when `ShowFolderBrowser`: `ItemsControl` breadcrumbs + `ListBox` folders + `ListBox` direct tracks (`IsVisible` bindings). Hide main `TrackList` when folders mode.

- [ ] **Step 4: Playback scope**

In `MainWindowViewModel.ResolveCurrentPlaybackQueueAsync` for `AllTracks`, if `AllTracks.ShowFolderBrowser && AllTracks.TryGetFolderPlaybackTracks(out var list)` return that list; else existing virtual collection path.

- [ ] **Step 5: Manual / headless smoke**

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~LibraryFolderNavigatorTests"
dotnet build src/MusicVault.App/MusicVault.App.csproj -p:WindowsPackageType=None
```

- [ ] **Step 6: Publish M1**

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

Smoke: add library with two subfolders → 文件夹 mode → enter each → play.

---

## Milestone M2 — Slice 2: Discover primary CTA + unified failure (核 B)

### Task 3: DiscoverPrimaryCta + tests

**Files:**
- Create: `src/MusicVault.App/Services/DiscoverPrimaryCta.cs`
- Create: `tests/MusicVault.Tests/App/DiscoverPrimaryCtaTests.cs`

```csharp
public enum DiscoverPrimaryCtaKind { Resume, BrowseFeatured, FocusSearch }

public static class DiscoverPrimaryCta
{
    public static DiscoverPrimaryCtaKind Resolve(bool hasRecentPlayback, bool browseHubReady) =>
        hasRecentPlayback ? DiscoverPrimaryCtaKind.Resume
        : browseHubReady ? DiscoverPrimaryCtaKind.BrowseFeatured
        : DiscoverPrimaryCtaKind.FocusSearch;
}
```

Tests: three facts covering priority order.

### Task 4: Unified source failure banner

**Files:**
- Create: `src/MusicVault.App/Services/DiscoverSourceFailureBanner.cs`
- Create: `tests/MusicVault.Tests/App/DiscoverSourceFailureBannerTests.cs`
- Modify: `SearchPageViewModel.cs`, `SearchPageView.axaml`

**Contract:**
- Single visible banner when either API health bad OR partial search failure.
- Model: `string Message`, `bool ShowRetry`, `bool ShowOpenSettings`.
- Merge helper prefers concrete search partial text when present; else health summary (`FormatDualApiHealthSummary` logic move or call).
- XAML: replace dual banners (`ShowApiHealthBanner` + `ShowSearchPartialFailureBanner`) with one `ShowUnifiedSourceFailureBanner` block: message + Retry (`RefreshApiHealth` / re-search) + Open online sources.
- Keep login capsules; do not expand platform matrix.

### Task 5: Primary CTA on browse hub

**Files:**
- Modify: `SearchPageView.axaml` (browse hub top), `SearchPageViewModel.cs`, `DiscoverLocalizationResources.cs`

**Contract:**
- When `ShowBrowseHub`, show **one** primary button above charts:
  - Resume → navigate/play last (reuse existing recent if any; if none at runtime, fall through)
  - BrowseFeatured → scroll/focus charts or `RefreshBrowseHubAsync`
  - FocusSearch → focus search box (`UseHeroSearch` already)
- Demote advanced modes (parse/recognize) visually (already in advanced disclosure).

- [ ] Publish M2: `.\scripts\publish-with-api.ps1 -Restart`

---

## Milestone M3 — Slice 3: Library filters + missing markers (核 A)

### Task 6: CoverHash on scan + TrackMissingMetadata

**Files:**
- Modify: `src/MusicVault.Services/Metadata/TrackMetadataHelper.cs` `MapFromTagLib`
- Create: `src/MusicVault.Services/Metadata/TrackMissingMetadata.cs`
- Create: `tests/MusicVault.Tests/TrackMissingMetadataTests.cs`

```csharp
public static class TrackMissingMetadata
{
    public static bool HasCoverSignal(Track t) =>
        !string.IsNullOrWhiteSpace(t.CoverHash);

    public static bool HasLyricsSignal(Track t) =>
        !string.IsNullOrWhiteSpace(t.LyricsPath);

    public static bool IsMissingCover(Track t) => !HasCoverSignal(t);
    public static bool IsMissingLyrics(Track t) => !HasLyricsSignal(t);
}
```

In `MapFromTagLib`, after reading tags:

```csharp
var hasPic = file.Tag.Pictures is { Length: > 0 };
coverHash: hasPic ? "embedded" : null, // stable sentinel; real hash optional later
```

Document in PRODUCT_DESIGN: 「缺歌词」= 无侧车 `LyricsPath`（内嵌歌词不计入有歌词，与扫描写入一致）；「缺封面」= 无 `CoverHash`。既有曲目需重扫后标记才准。

### Task 7: Repository + VirtualTrackCollection filters

**Files:**
- Modify: `TrackRepository.cs` — add optional flags to scoped/list queries OR post-filter page (prefer SQL):

```sql
-- when missingCover: AND (CoverHash IS NULL OR CoverHash = '')
-- when missingLyrics: AND (LyricsPath IS NULL OR LyricsPath = '')
```

Wire `VirtualTrackCollection.SetMissingFilters(bool? missingCover, bool? missingLyrics)` and pass through Reload/LoadMore/LoadPlaybackScope.

### Task 8: Tracks toolbar chips + row tags

**Files:**
- Modify: `TracksPageViewModel.cs`, `TracksPageView.axaml`
- Modify: `TrackItemViewModel.cs` — `ShowMissingCover`, `ShowMissingLyrics` from track signals
- Modify: `SharedDataTemplates.axaml` `LibraryTrackRowTemplate` — small tags next to title (same pattern as NAS cache tag)
- Localization keys for chips

Filters are session-only (clear on leave optional; default discard when leaving page is OK).

Works in Songs mode; in Folders mode apply same predicates to `FolderDirectTracks` client-side from cache (small) or re-split after filter.

- [ ] Publish M3

---

## Milestone M4 — Slice 4: Recognize → play / next (核 B)

### Task 9: OnlinePreviewQueue play-next

**Files:**
- Modify: `src/MusicVault.App/Services/OnlinePreviewQueue.cs`
- Create/Modify: `tests/MusicVault.Tests/App/OnlinePreviewQueueTests.cs`
- Modify: `PlayerBarViewModel.cs`

```csharp
public OnlinePreviewQueue WithInsertedAfterCurrent(OnlineTrack track)
{
    if (track is null || IsEmpty) return this;
    if (IndexOf(Tracks, track) >= 0)
        return AlignToCurrent(Current!);
    var list = Tracks.ToList();
    list.Insert(CurrentIndex + 1, track);
    return new OnlinePreviewQueue(list, CurrentIndex);
}
```

`PlayerBarViewModel.QueueOnlinePlayNext(OnlineTrack track)`:
- If currently playing online preview with non-empty queue → `_onlinePreviewQueue = _onlinePreviewQueue.WithInsertedAfterCurrent(track)`
- Else if nothing playing → same as Preview (play now)
- Else if playing local → start online preview of `track` and set queue to `[track]` only (honest: cannot splice into local queue without dual-queue; document)

### Task 10: Recognize success action strip

**Files:**
- Modify: `SearchPageViewModel.cs` `RecognizeAudioAsync` success path
- Modify: `SearchPageView.axaml`

**Contract (lock):**
- Keep fill query + `SearchAsync()` (do not rewrite recognize capture UI).
- After `SearchAsync`, if `HasResults`:
  - Set `RecognizePlayTarget` = first `OnlineTrackViewModel` in `Results` (best title match if cheap; else `Results[0]`).
  - `ShowRecognizePlayActions = true`.
- Actions:
  - **播放** → `RecognizePlayTarget.PreviewCommand`；stay on Discover (`DiscoverModeIndex` already 0).
  - **下一首播放** → `_player.QueueOnlinePlayNext(RecognizePlayTarget.Model)` (expose model from VM).
- If no results / no streamable: hide actions; status already explains; buttons disabled with caption「暂无可播放源，可改关键词搜索」.

- [ ] Publish M4
- [ ] Update `docs/PRODUCT_DESIGN.md` capability matrix for folder browse, missing filters, Discover CTA, recognize play; leave「识曲交互重写」frozen.

---

## Self-review (spec coverage)

| Spec section | Tasks |
|--------------|-------|
| §4 Folder browse | T1–T2 |
| §5 Discover CTA + failure | T3–T5 |
| §6 Filters + markers | T6–T8 |
| §7 Recognize play/next | T9–T10 |
| §2 Non-goals | No skins/AI/nav collapse/recognize rewrite |
| Publish per slice | Steps on T2/T5/T8/T10 |
| PRODUCT_DESIGN | T10 footer |

**Placeholder scan:** none intentional.  
**Type names:** `LibraryFolderNavigator`, `DiscoverPrimaryCta`, `DiscoverSourceFailureBanner`, `TrackMissingMetadata`, `WithInsertedAfterCurrent` — used consistently above.

---

## Risk notes for implementers

1. Large libraries: folder mode caches `GetAllUnderLibraryScopeAsync` per selected library; if >50k tracks, follow-up may stream paths-only — out of M1 scope unless smoke fails.
2. Pre-M3 libraries lack `CoverHash` until rescan — empty「缺封面」until then; UX caption OK.
3. Play-next across local↔online is limited; prefer honest disable when player is local-only.
