using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Pages;

public partial class TracksPageViewModel
{
    private int _folderRefreshVersion;
    private FolderTrackCache? _folderTracksCache;
    private bool _folderHasUnfilteredDirectTracks;
    private string? _folderLoadError;
    private bool _folderLoadErrorActive;

    public ObservableCollection<FolderBrowseItemViewModel> FolderChildren { get; } = [];
    public ObservableCollection<TrackItemViewModel> FolderDirectTracks { get; } = [];
    public ObservableCollection<FolderBreadcrumbItemViewModel> FolderBreadcrumbs { get; } = [];

    /// <summary>0 歌曲列表，1 文件夹浏览。</summary>
    [ObservableProperty] private int _browseModeIndex;

    [ObservableProperty] private string? _currentFolderAbsolute;
    [ObservableProperty] private bool _isFolderBrowserLoading;

    public bool IsSongsBrowseMode
    {
        get => BrowseModeIndex == 0;
        set
        {
            if (value)
                BrowseModeIndex = 0;
            else
                OnPropertyChanged(nameof(IsSongsBrowseMode));
        }
    }

    public bool IsFoldersBrowseMode
    {
        get => BrowseModeIndex == 1;
        set
        {
            if (value)
                BrowseModeIndex = 1;
            else
                OnPropertyChanged(nameof(IsFoldersBrowseMode));
        }
    }

    public bool ShowSongsList => BrowseModeIndex == 0;
    public bool ShowFolderBrowser => BrowseModeIndex == 1;

    public bool ShowFolderLoading => ShowFolderBrowser && IsFolderBrowserLoading;
    public bool ShowFolderChrome => ShowFolderBrowser && !IsFolderBrowserLoading && TryGetFolderScope() != null;
    public bool ShowFolderPickLibrary => ShowFolderBrowser && !IsFolderBrowserLoading && TryGetFolderScope() == null;
    public bool ShowFolderEmpty =>
        ShowFolderBrowser
        && !IsFolderBrowserLoading
        && TryGetFolderScope() != null
        && FolderChildren.Count == 0
        && FolderDirectTracks.Count == 0;
    public bool ShowFolderContent => ShowFolderBrowser && !IsFolderBrowserLoading && !ShowFolderPickLibrary && !ShowFolderEmpty;
    public bool ShowFolderChildren => ShowFolderContent && FolderChildren.Count > 0;
    public bool ShowFolderDirectTracks => ShowFolderContent && FolderDirectTracks.Count > 0;
    public bool ShowFolderUp => ShowFolderChrome && !IsAtLibraryRoot();

    public string BrowseSongsLabel => _l10n.Get("Library.Tracks.BrowseMode.Songs");
    public string BrowseFoldersLabel => _l10n.Get("Library.Tracks.BrowseMode.Folders");
    public string FolderPickLibraryText => _l10n.Get("Library.Tracks.Folder.PickLibrary");
    public string? FolderLoadErrorText => _folderLoadError;
    private bool FolderEmptyIsFiltered =>
        HasMissingMetadataFilter
        && _folderHasUnfilteredDirectTracks
        && string.IsNullOrWhiteSpace(_folderLoadError);

    public string FolderEmptyText =>
        !string.IsNullOrWhiteSpace(_folderLoadError)
            ? _folderLoadError
            : FolderEmptyIsFiltered
                ? _l10n.Get("Library.Tracks.Folder.EmptyFiltered")
                : _l10n.Get("Library.Tracks.Folder.Empty");

    public string FolderEmptyActionText =>
        FolderEmptyIsFiltered
            ? _l10n.Get("Library.Empty.Action.ClearMissingFilters")
            : ScanSelectedLabel;

    public string FolderUpLabel => _l10n.Get("Library.Tracks.Folder.Up");

    public string FolderPickActionText
    {
        get
        {
            if (FavoritesOnly && SelectedLibraryItem is { IsAllLibraries: false, Library.RootPath: { Length: > 0 } })
                return _l10n.Get("Library.Empty.Action.ShowAll");

            var first = FirstConcreteLibrary();
            return first?.DisplayName ?? ManageSourcesLabel;
        }
    }

    partial void OnBrowseModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSongsBrowseMode));
        OnPropertyChanged(nameof(IsFoldersBrowseMode));
        NotifyFolderChrome();
        _ = RefreshFolderBrowserAsync();
    }

    partial void OnCurrentFolderAbsoluteChanged(string? value) => ApplyFolderSplitFromCache();

    partial void OnIsFolderBrowserLoadingChanged(bool value) => NotifyFolderChrome();

    /// <summary>当前文件夹（含子目录）内的曲目，供连续播放不出子树。</summary>
    public bool TryGetFolderPlaybackTracks(out IReadOnlyList<Track> list)
    {
        list = [];
        if (!ShowFolderBrowser || _folderTracksCache is not { } cache)
            return false;

        var scope = TryGetFolderScope();
        if (scope == null || cache.LibraryId != scope.Value.LibraryId)
            return false;
        if (string.IsNullOrWhiteSpace(CurrentFolderAbsolute))
            return false;

        var current = TryNormalizePath(CurrentFolderAbsolute);
        if (string.IsNullOrWhiteSpace(current))
            return false;

        var tracks = new List<Track>();
        foreach (var track in cache.Tracks)
        {
            if (IsTrackInsideFolder(track, current) && TrackMatchesMissingFilters(track))
                tracks.Add(track);
        }

        if (tracks.Count == 0)
            return false;

        list = tracks;
        return true;
    }

    public string? TryGetFolderPlaybackSourceLabel()
    {
        if (!ShowFolderBrowser || TryGetFolderScope() == null)
            return null;
        if (FolderBreadcrumbs.Count == 0)
            return SelectedLibraryItem?.DisplayName;
        return string.Join(" / ", FolderBreadcrumbs.Select(static crumb => crumb.Label));
    }

    [RelayCommand]
    private void EnterFolder(FolderBrowseItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.AbsolutePath))
            return;
        NavigateToFolder(item.AbsolutePath);
    }

    [RelayCommand]
    private void FolderUp()
    {
        var scope = TryGetFolderScope();
        if (scope == null || string.IsNullOrWhiteSpace(CurrentFolderAbsolute))
            return;

        var current = TryNormalizePath(CurrentFolderAbsolute);
        if (string.IsNullOrWhiteSpace(current)
            || string.Equals(current, scope.Value.Root, StringComparison.OrdinalIgnoreCase))
            return;

        var parent = TryNormalizePath(Path.GetDirectoryName(current));
        if (string.IsNullOrWhiteSpace(parent) || !IsPathUnderOrEqual(parent, scope.Value.Root))
            return;

        NavigateToFolder(parent);
    }

    [RelayCommand]
    private void ResolveFolderLibrary()
    {
        if (FavoritesOnly && SelectedLibraryItem is { IsAllLibraries: false, Library.RootPath: { Length: > 0 } })
        {
            FavoritesOnly = false;
            return;
        }

        if (FavoritesOnly)
            FavoritesOnly = false;

        var first = FirstConcreteLibrary();
        if (first != null)
        {
            SelectLibraryFromView(first);
            return;
        }

        OpenLibrarySources();
    }

    internal void NotifyFolderLocalizedText()
    {
        OnPropertyChanged(nameof(BrowseSongsLabel));
        OnPropertyChanged(nameof(BrowseFoldersLabel));
        OnPropertyChanged(nameof(FolderPickLibraryText));
        if (_folderLoadErrorActive)
        {
            _folderLoadError = _l10n.Get("Library.Tracks.Folder.LoadFailed");
            StatusMessage = _folderLoadError;
        }
        OnPropertyChanged(nameof(FolderLoadErrorText));
        OnPropertyChanged(nameof(FolderEmptyText));
        OnPropertyChanged(nameof(FolderEmptyActionText));
        OnPropertyChanged(nameof(FolderUpLabel));
        OnPropertyChanged(nameof(FolderPickActionText));
    }

    internal void InvalidateFolderTracksCache() => _folderTracksCache = null;

    internal async Task RefreshFolderBrowserAsync()
    {
        var version = Interlocked.Increment(ref _folderRefreshVersion);
        FolderScope? scope = null;
        var browse = false;
        var cacheReady = false;

        await OnUiAsync(() =>
        {
            if (version != _folderRefreshVersion)
                return;

            browse = ShowFolderBrowser;
            if (!browse)
            {
                IsFolderBrowserLoading = false;
                ClearFolderLoadError();
                NotifyFolderChrome();
                return;
            }

            scope = TryGetFolderScope();
            if (scope == null)
            {
                IsFolderBrowserLoading = false;
                ClearFolderCollections();
                ClearFolderLoadError();
                NotifyFolderChrome();
                return;
            }

            cacheReady = CacheMatches(scope.Value);
            if (cacheReady)
            {
                ApplyFolderSplitFromCache();
                IsFolderBrowserLoading = false;
                return;
            }

            IsFolderBrowserLoading = true;
        });

        if (!browse || scope == null || cacheReady || version != _folderRefreshVersion)
            return;

        IReadOnlyList<Track> tracks;
        Exception? loadError = null;
        try
        {
            tracks = await _services.Tracks
                .GetAllUnderLibraryScopeAsync(scope.Value.LibraryId, scope.Value.Root)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            tracks = [];
            loadError = ex;
            Debug.WriteLine($"[FolderBrowse] load failed: {ex}");
        }

        if (version != _folderRefreshVersion)
            return;

        if (loadError == null)
            _folderTracksCache = new FolderTrackCache(scope.Value.LibraryId, scope.Value.Root, tracks);

        await OnUiAsync(() =>
        {
            if (version != _folderRefreshVersion)
                return;
            if (loadError != null)
            {
                _folderTracksCache = null;
                ClearFolderCollections();
                SetFolderLoadError(loadError);
                IsFolderBrowserLoading = false;
                return;
            }

            ApplyFolderSplitFromCache();
            IsFolderBrowserLoading = false;
        });
    }

    private void ApplyFolderSplitFromCache()
    {
        if (!ShowFolderBrowser)
        {
            NotifyFolderChrome();
            return;
        }

        var scope = TryGetFolderScope();
        if (scope == null || _folderTracksCache is not { } cache || !CacheMatches(scope.Value))
        {
            if (scope == null)
                ClearFolderCollections();
            NotifyFolderChrome();
            return;
        }

        var current = TryNormalizePath(CurrentFolderAbsolute) ?? scope.Value.Root;
        if (!IsPathUnderOrEqual(current, scope.Value.Root))
            current = scope.Value.Root;

        if (!string.Equals(CurrentFolderAbsolute, current, StringComparison.OrdinalIgnoreCase))
        {
            CurrentFolderAbsolute = current;
            return;
        }

        FolderSplitResult split;
        var splitFailed = false;
        try
        {
            split = LibraryFolderNavigator.Split(scope.Value.Root, current, cache.Tracks);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FolderBrowse] split failed: {ex}");
            split = new FolderSplitResult([], []);
            splitFailed = true;
            SetFolderLoadError(ex);
        }

        if (!splitFailed)
            ClearFolderLoadError();
        else
        {
            FolderChildren.Clear();
            FolderDirectTracks.Clear();
            FolderBreadcrumbs.Clear();
            _folderHasUnfilteredDirectTracks = false;
            NotifyFolderChrome();
            return;
        }

        FolderChildren.Clear();
        foreach (var name in split.ChildFolders)
            FolderChildren.Add(new FolderBrowseItemViewModel(name, LibraryFolderNavigator.Combine(current, name)));

        FolderDirectTracks.Clear();
        _folderHasUnfilteredDirectTracks = split.DirectTracks.Count > 0;
        var row = 1;
        foreach (var track in split.DirectTracks)
        {
            if (!TrackMatchesMissingFilters(track))
                continue;
            FolderDirectTracks.Add(CreateFolderTrackRow(track, row++));
        }

        var crumbs = LibraryFolderNavigator.Breadcrumb(scope.Value.Root, current, scope.Value.DisplayName);
        FolderBreadcrumbs.Clear();
        for (var i = 0; i < crumbs.Count; i++)
        {
            var crumb = crumbs[i];
            FolderBreadcrumbs.Add(new FolderBreadcrumbItemViewModel(
                crumb.Label,
                crumb.FolderAbsolutePath,
                isCurrent: i == crumbs.Count - 1,
                showSeparator: i > 0,
                NavigateToFolder));
        }

        NotifyFolderChrome();
    }

    private void NavigateToFolder(string absolutePath)
    {
        var normalized = TryNormalizePath(absolutePath);
        if (string.IsNullOrWhiteSpace(normalized))
            return;
        if (string.Equals(CurrentFolderAbsolute, normalized, StringComparison.OrdinalIgnoreCase))
        {
            ApplyFolderSplitFromCache();
            return;
        }

        CurrentFolderAbsolute = normalized;
    }

    private TrackItemViewModel CreateFolderTrackRow(Track track, int rowIndex)
    {
        var item = new TrackItemViewModel(track) { RowIndex = rowIndex };
        item.IsFavorite = _favoriteTrackIds.Contains(track.Id);
        ApplyNasPlaybackCacheFlag(item);
        return item;
    }

    private void ApplyNasPlaybackCacheFlag(TrackItemViewModel item)
    {
        if (!IsNasTrack(item.Track))
        {
            item.SetNasPlaybackCacheState(false, string.Empty, string.Empty);
            return;
        }

        var isCached = _services.NasTrackCache.IsCached(item.Track);
        item.SetNasPlaybackCacheState(
            isCached,
            isCached ? _l10n.Get("Nas.PlaybackCache.Cached") : string.Empty,
            isCached ? _l10n.Get("Nas.PlaybackCache.CachedToolTip") : string.Empty);
    }

    private void NotifyFolderChrome()
    {
        OnPropertyChanged(nameof(ShowSongsList));
        OnPropertyChanged(nameof(ShowFolderBrowser));
        OnPropertyChanged(nameof(ShowFolderLoading));
        OnPropertyChanged(nameof(ShowFolderChrome));
        OnPropertyChanged(nameof(ShowFolderPickLibrary));
        OnPropertyChanged(nameof(ShowFolderEmpty));
        OnPropertyChanged(nameof(ShowFolderContent));
        OnPropertyChanged(nameof(ShowFolderChildren));
        OnPropertyChanged(nameof(ShowFolderDirectTracks));
        OnPropertyChanged(nameof(ShowFolderUp));
        OnPropertyChanged(nameof(FolderPickActionText));
        OnPropertyChanged(nameof(FolderEmptyText));
        OnPropertyChanged(nameof(FolderEmptyActionText));
    }

    [RelayCommand]
    private async Task FolderEmptyPrimaryAsync()
    {
        if (FolderEmptyIsFiltered)
        {
            ClearMissingMetadataFilters();
            return;
        }

        await ScanSelectedAsync().ConfigureAwait(true);
    }

    private bool TrackMatchesMissingFilters(Track track)
    {
        if (MissingCoverOnly && !TrackMissingMetadata.IsMissingCover(track))
            return false;
        if (MissingLyricsOnly && !TrackMissingMetadata.IsMissingLyrics(track))
            return false;
        return true;
    }

    private void ClearFolderCollections()
    {
        FolderChildren.Clear();
        FolderDirectTracks.Clear();
        FolderBreadcrumbs.Clear();
        _folderHasUnfilteredDirectTracks = false;
    }

    private bool CacheMatches(FolderScope scope) =>
        _folderTracksCache is { } cache
        && cache.LibraryId == scope.LibraryId
        && string.Equals(cache.Root, scope.Root, StringComparison.OrdinalIgnoreCase);

    private FolderScope? TryGetFolderScope()
    {
        if (FavoritesOnly)
            return null;

        var item = SelectedLibraryItem;
        if (item is not { IsAllLibraries: false, Library: { } lib })
            return null;
        if (string.IsNullOrWhiteSpace(lib.RootPath))
            return null;

        var root = TryNormalizePath(lib.RootPath);
        if (string.IsNullOrWhiteSpace(root))
            return null;

        return new FolderScope(lib.Id, root, item.DisplayName);
    }

    private LibraryListItemViewModel? FirstConcreteLibrary() =>
        Libraries.FirstOrDefault(l => l is { IsAllLibraries: false, Library.RootPath: { Length: > 0 } });

    private bool IsAtLibraryRoot()
    {
        var scope = TryGetFolderScope();
        if (scope == null || string.IsNullOrWhiteSpace(CurrentFolderAbsolute))
            return true;

        var current = TryNormalizePath(CurrentFolderAbsolute);
        return string.IsNullOrWhiteSpace(current)
               || string.Equals(current, scope.Value.Root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrackInsideFolder(Track track, string folder)
    {
        if (string.IsNullOrWhiteSpace(track.FilePath))
            return false;

        var file = TryNormalizePath(track.FilePath);
        if (string.IsNullOrWhiteSpace(file))
            return false;

        return file.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || file.StartsWith(folder + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathUnderOrEqual(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string? TryNormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return LibraryFolderNavigator.Normalize(path);
        }
        catch (Exception)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private void SetFolderLoadError(Exception ex)
    {
        Debug.WriteLine($"[FolderBrowse] {ex}");
        _folderLoadError = _l10n.Get("Library.Tracks.Folder.LoadFailed");
        _folderLoadErrorActive = true;
        StatusMessage = _folderLoadError;
        OnPropertyChanged(nameof(FolderLoadErrorText));
        OnPropertyChanged(nameof(FolderEmptyText));
        NotifyFolderChrome();
    }

    private void ClearFolderLoadError()
    {
        if (!_folderLoadErrorActive && _folderLoadError == null)
            return;

        var failedText = _l10n.Get("Library.Tracks.Folder.LoadFailed");
        var clearStatus = _folderLoadErrorActive && StatusMessage == failedText;
        _folderLoadError = null;
        _folderLoadErrorActive = false;
        if (clearStatus)
            StatusMessage = string.Empty;
        OnPropertyChanged(nameof(FolderLoadErrorText));
        OnPropertyChanged(nameof(FolderEmptyText));
    }

    private static async Task OnUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    private readonly record struct FolderScope(long LibraryId, string Root, string DisplayName);

    private sealed class FolderTrackCache
    {
        public FolderTrackCache(long libraryId, string root, IReadOnlyList<Track> tracks)
        {
            LibraryId = libraryId;
            Root = root;
            Tracks = tracks;
        }

        public long LibraryId { get; }
        public string Root { get; }
        public IReadOnlyList<Track> Tracks { get; }
    }
}
