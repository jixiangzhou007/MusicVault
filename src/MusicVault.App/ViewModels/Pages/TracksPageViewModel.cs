using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Services;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;
using MusicVault.Services.Playlist;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>全部歌曲：左侧音乐库 + 统一曲目表（原首页曲库区）。</summary>
public partial class TracksPageViewModel : PageViewModelBase
{
    private const int MaxOrganizeEnqueueCount = 5000;

    private readonly AppServices _services;
    private readonly AppModalHostViewModel _appModal;
    private readonly PlayerBarViewModel _player;
    private readonly ILocalizationService _l10n;
    private readonly HashSet<long> _favoriteTrackIds = [];
    private CancellationTokenSource? _tracksLoadCts;
    private CancellationTokenSource? _postDownloadRefreshCts;
    private CancellationTokenSource? _trackSearchDebounceCts;
    private (long? LibraryId, bool FavoritesOnly, string? SearchQuery)? _loadedScope;

    public VirtualTrackCollection Tracks { get; }

    /// <summary>列表 ItemsSource：绑定稳定 ObservableCollection，滚动加载时增量追加。</summary>
    public ObservableCollection<TrackItemViewModel> TrackListItems => Tracks.Items;

    public ObservableCollection<LibraryListItemViewModel> Libraries { get; } = [];
    public ObservableCollection<Playlist> PlaylistsForMenu { get; } = [];

    public override string PageTitle { get; }
    public override string PageSubtitle => TracksListSummary;
    public override bool ShowPageSubtitle => false;
    public override bool ShowStandardPageHeader => false;

    /// <summary>右侧列表上方：当前库名 + 曲目数 + 容量（随左侧选中项变化）。</summary>
    public string TracksListSummary => BuildTracksListSummary();

    [ObservableProperty] private LibraryListItemViewModel? _selectedLibraryItem;
    [ObservableProperty] private bool _isLibraryPaneOpen = true;
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _trackSearchText = string.Empty;

    private int _scanAcoustIdAttempts;
    private int _scanAcoustIdMatches;

    public bool ShowFavoritesFilter { get; }
    public string TrackSearchWatermark => BuildTrackSearchWatermark();
    public string ScanStatusText => IsScanning
        ? FormatScanProgressText(ScanProgress, _scanAcoustIdMatches, _scanAcoustIdAttempts)
        : string.Empty;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string SwitchLibraryLabel => _l10n.Get("Library.SwitchLibrary");
    public string AddSourceToolTip => _l10n.Get("Library.AddSource");
    public string AddLocalFolderLabel => _l10n.Get("Library.AddLocalFolder");
    public string AddRemoteNasLabel => _l10n.Get("Library.AddRemoteNas");
    public string AddRemoteWebDavLabel => _l10n.Get("Library.AddRemoteWebDav");
    public string RefreshListToolTip => _l10n.Get("Library.RefreshList");
    public string MoreActionsToolTip => _l10n.Get("Library.MoreActions");
    public string ScanSelectedLabel => _l10n.Get("Library.ScanSelected");
    public string ScanAllLabel => _l10n.Get("Library.ScanAll");
    public string DeleteLibraryMenuLabel => _l10n.Get("Library.DeleteLibraryMenu");
    public string ManageSourcesLabel => _l10n.Get("Library.ManageSources");
    public string ManageSourcesToolTip => _l10n.Get("Library.ManageSourcesToolTip");
    public string SearchButtonLabel => _l10n.Get("Common.Search");
    public string OrganizeLabel => _l10n.Get("Library.Organize");
    public string OrganizeToolTip => _l10n.Get("Library.OrganizeToolTip");
    public string OrganizeAddCurrentListLabel => _l10n.Get("Library.OrganizeAddCurrentList");
    public string OrganizeOpenToolsLabel => _l10n.Get("Library.OrganizeOpenTools");
    public string FavoritesLabel => _l10n.Get("Library.Favorites");
    public string FavoritesFilterToolTip => _l10n.Get("Library.FavoritesFilterToolTip");
    public string ReconnectNasLabel => _l10n.Get("Nas.Reconnect");
    public string ReconnectNasToolTip => _l10n.Get("Nas.Reconnect.ToolTip");
    public string DownloadNasToLibraryLabel => _l10n.Get("Nas.DownloadToLibrary");
    public string DownloadNasLibraryLabel => _l10n.Get("Nas.DownloadLibraryToLocal");

    public bool CanDownloadSelectedNasLibrary =>
        SelectedLibraryItem is { IsNetworkSource: true, Library: not null }
        && _hasLocalDownloadTarget;

    private bool _hasLocalDownloadTarget;

    public static bool IsNasTrack(Track track) => NasPathHelper.IsRemoteLibraryPath(track.FilePath);

    public bool CanReconnectSelectedNas =>
        SelectedLibraryItem is { IsNetworkSource: true, Library: not null } item
        && NasLibraryUiHelper.GetNetworkLibraryStatus(_services.Library, item.Library!)?.Reachability
            != NasReachability.Online;

    public string DeletePopconfirmText => _l10n.Get("Common.Delete");
    public string RemoveTrackDeleteWithFileText => _l10n.Get("Library.RemoveTrack.DeleteWithFile");

    public bool ShowTracksSkeleton => Tracks.IsLoading && Tracks.Items.Count == 0;
    public bool ShowTracksLoadMoreSkeleton => Tracks.IsLoading && Tracks.Items.Count > 0;
    public bool ShowTracksEmpty => !Tracks.IsLoading && Tracks.TotalCount == 0;
    public bool ShowTracksList => !ShowTracksEmpty && !ShowTracksSkeleton;
    public bool HasUserLibraries => Libraries.Any(l => !l.IsAllLibraries);
    public bool ShowTracksEmptyAction => ShowTracksEmpty;

    private enum TracksEmptyKind
    {
        Search,
        Favorites,
        SingleLibrary,
        NoSources,
        Default
    }

    private TracksEmptyKind ResolveTracksEmptyKind()
    {
        // 搜索优先于收藏过滤，避免「收藏+搜索无匹配」误显示「暂无收藏」
        if (Tracks.IsSearchMode)
            return TracksEmptyKind.Search;
        if (FavoritesOnly)
            return TracksEmptyKind.Favorites;
        if (SelectedLibraryItem is { IsAllLibraries: false })
            return TracksEmptyKind.SingleLibrary;
        if (!HasUserLibraries)
            return TracksEmptyKind.NoSources;
        return TracksEmptyKind.Default;
    }

    public string TracksEmptyTitle => ResolveTracksEmptyKind() switch
    {
        TracksEmptyKind.Search => _l10n.Get("Library.Empty.SearchTitle"),
        TracksEmptyKind.Favorites => _l10n.Get("Library.Empty.FavoritesTitle"),
        TracksEmptyKind.NoSources => _l10n.Get("Library.Empty.NoSourcesTitle"),
        _ => _l10n.Get("Library.Empty.DefaultTitle")
    };

    public string TracksEmptyDescription => ResolveTracksEmptyKind() switch
    {
        TracksEmptyKind.Search => _l10n.Get("Library.Empty.SearchDesc"),
        TracksEmptyKind.Favorites => _l10n.Get("Library.Empty.FavoritesDesc"),
        TracksEmptyKind.SingleLibrary => _l10n.Get("Library.Empty.LibraryDesc"),
        TracksEmptyKind.NoSources => _l10n.Get("Library.Empty.NoSourcesDesc"),
        _ => _l10n.Get("Library.Empty.DefaultDesc")
    };

    public string TracksEmptyActionText => ResolveTracksEmptyKind() switch
    {
        TracksEmptyKind.Search => _l10n.Get("Library.Empty.Action.ClearSearch"),
        TracksEmptyKind.Favorites => _l10n.Get("Library.Empty.Action.ShowAll"),
        TracksEmptyKind.SingleLibrary => _l10n.Get("Library.Empty.Action.ScanLibrary"),
        TracksEmptyKind.NoSources => _l10n.Get("Library.Empty.Action.AddLibrary"),
        _ => _l10n.Get("Library.Empty.Action.ScanAll")
    };

    public bool CanDeleteSelectedLibrary => SelectedLibraryItem is { IsAllLibraries: false, Library: not null };

    public string DeleteLibraryPopconfirmTitle =>
        SelectedLibraryItem is { IsAllLibraries: false, DisplayName: { } name }
            ? string.Format(_l10n.Get("Library.DeleteLibrary.Confirm"), name)
            : _l10n.Get("Library.DeleteLibrary.Title");

    public string DeleteLibraryPopconfirmDescription =>
        SelectedLibraryItem is { IsAllLibraries: false, RootPath: { } path } && !string.IsNullOrWhiteSpace(path)
            ? string.Format(_l10n.Get("Library.DeleteLibrary.DescWithPath"), path)
            : _l10n.Get("Library.DeleteLibrary.Desc");

    [ObservableProperty] private TrackItemViewModel? _pendingRemoveTrack;

    public string RemoveTrackPopconfirmTitle =>
        PendingRemoveTrack == null
            ? _l10n.Get("Library.RemoveTrack.Title")
            : RemoveTrackConfirmText.Title(_l10n, PendingRemoveTrack);

    public string RemoveTrackPopconfirmDescription =>
        RemoveTrackConfirmText.Description(
            _l10n,
            PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack));

    public string RemoveTrackIndexOnlyText =>
        RemoveTrackConfirmText.IndexOnlyConfirmText(
            _l10n,
            PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack));

    public bool ShowRemoveTrackAlternateConfirm =>
        PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack);

    public string LibraryPaneToggleGlyph => IsLibraryPaneOpen ? "\uE76B" : "\uE76C";
    public string LibraryPaneToggleToolTip => IsLibraryPaneOpen
        ? _l10n.Get("Library.PaneCollapse")
        : _l10n.Get("Library.PaneExpand");

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsLibraryPaneOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(LibraryPaneToggleGlyph));
        OnPropertyChanged(nameof(LibraryPaneToggleToolTip));
    }

    partial void OnFavoritesOnlyChanged(bool value)
    {
        NotifyTracksListSummary();
        OnPropertyChanged(nameof(TrackSearchWatermark));
        InvalidateLoadedScope();
        _ = ApplySourceFilterAsync();
    }

    partial void OnSelectedLibraryItemChanged(LibraryListItemViewModel? value)
    {
        NotifyTracksListSummary();
        OnPropertyChanged(nameof(TrackSearchWatermark));
        OnPropertyChanged(nameof(CanDeleteSelectedLibrary));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmTitle));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmDescription));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        OnPropertyChanged(nameof(CanDownloadSelectedNasLibrary));
    }

    partial void OnPendingRemoveTrackChanged(TrackItemViewModel? value)
    {
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        OnPropertyChanged(nameof(ShowRemoveTrackAlternateConfirm));
    }

    public void PrepareRemoveTrackConfirm(TrackItemViewModel item) => PendingRemoveTrack = item;

    partial void OnTrackSearchTextChanged(string value)
    {
        _trackSearchDebounceCts?.Cancel();
        _trackSearchDebounceCts?.Dispose();
        _trackSearchDebounceCts = new CancellationTokenSource();
        var token = _trackSearchDebounceCts.Token;
        _ = DebouncedTrackSearchAsync(token);
    }

    public TracksPageViewModel(
        AppServices services,
        AppModalHostViewModel appModal,
        PlayerBarViewModel player,
        ILocalizationService l10n,
        string title,
        bool showFavoritesFilter = false)
    {
        _services = services;
        _appModal = appModal;
        _player = player;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        PageTitle = title;
        ShowFavoritesFilter = showFavoritesFilter;
        Tracks = new VirtualTrackCollection(services);
        Tracks.PropertyChanged += OnTracksCollectionPropertyChanged;
        Tracks.CollectionChanged += (_, _) => NotifyTracksListSummary();
        _services.Favorites.FavoritesChanged += (_, _) => _ = RefreshFavoriteFlagsAsync();
        if (showFavoritesFilter)
            _services.Favorites.FavoritesChanged += (_, _) => _ = OnFavoritesChangedAsync();
        _services.NasLibraries.StatusChanged += OnNasLibraryStatusChanged;
        _services.WebDavLibraries.StatusChanged += OnNasLibraryStatusChanged;
        _services.NasTrackCache.Changed += OnNasPlaybackCacheChanged;
        RefreshLocalizedStrings();
    }

    private void OnNasPlaybackCacheChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(ApplyNasPlaybackCacheFlags);
    }

    private void ApplyNasPlaybackCacheFlags()
    {
        var cachedLabel = _l10n.Get("Nas.PlaybackCache.Cached");
        var cachedToolTip = _l10n.Get("Nas.PlaybackCache.CachedToolTip");
        var emptyToolTip = string.Empty;

        foreach (var item in Tracks.Items)
        {
            if (!IsNasTrack(item.Track))
            {
                item.SetNasPlaybackCacheState(false, string.Empty, emptyToolTip);
                continue;
            }

            var isCached = _services.NasTrackCache.IsCached(item.Track);
            item.SetNasPlaybackCacheState(
                isCached,
                isCached ? cachedLabel : string.Empty,
                isCached ? cachedToolTip : emptyToolTip);
        }
    }

    private void RequestOpenTransfersPage() => OpenTransfersPageRequested?.Invoke(this, EventArgs.Empty);

    private void OnNasLibraryStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            NasLibraryUiHelper.ApplyNetworkLibraryStatuses(Libraries, _services.Library, _l10n);
            OnPropertyChanged(nameof(CanReconnectSelectedNas));
        });
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(SwitchLibraryLabel));
        OnPropertyChanged(nameof(AddSourceToolTip));
        OnPropertyChanged(nameof(AddLocalFolderLabel));
        OnPropertyChanged(nameof(AddRemoteNasLabel));
        OnPropertyChanged(nameof(AddRemoteWebDavLabel));
        OnPropertyChanged(nameof(RefreshListToolTip));
        OnPropertyChanged(nameof(MoreActionsToolTip));
        OnPropertyChanged(nameof(ScanSelectedLabel));
        OnPropertyChanged(nameof(ScanAllLabel));
        OnPropertyChanged(nameof(DeleteLibraryMenuLabel));
        OnPropertyChanged(nameof(ManageSourcesLabel));
        OnPropertyChanged(nameof(ManageSourcesToolTip));
        OnPropertyChanged(nameof(SearchButtonLabel));
        OnPropertyChanged(nameof(OrganizeLabel));
        OnPropertyChanged(nameof(OrganizeToolTip));
        OnPropertyChanged(nameof(OrganizeAddCurrentListLabel));
        OnPropertyChanged(nameof(OrganizeOpenToolsLabel));
        OnPropertyChanged(nameof(FavoritesLabel));
        OnPropertyChanged(nameof(FavoritesFilterToolTip));
        OnPropertyChanged(nameof(DeletePopconfirmText));
        OnPropertyChanged(nameof(RemoveTrackDeleteWithFileText));
        OnPropertyChanged(nameof(LibraryPaneToggleToolTip));
        OnPropertyChanged(nameof(ScanStatusText));
        OnPropertyChanged(nameof(ReconnectNasLabel));
        OnPropertyChanged(nameof(ReconnectNasToolTip));
        OnPropertyChanged(nameof(DownloadNasToLibraryLabel));
        OnPropertyChanged(nameof(DownloadNasLibraryLabel));
        OnPropertyChanged(nameof(CanDownloadSelectedNasLibrary));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        OnPropertyChanged(nameof(TrackSearchWatermark));
        NotifyTracksListSummary();
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmTitle));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        NasLibraryUiHelper.ApplyNetworkLibraryStatuses(Libraries, _services.Library, _l10n);
        ApplyLibraryDisabledLabels();
        ApplyNasPlaybackCacheFlags();
        RefreshLibraryListItems();
    }

    private void ApplyLibraryDisabledLabels()
    {
        var label = _l10n.Get("Library.Disabled");
        foreach (var lib in Libraries)
            lib.SetDisabledLabel(label);
    }

    private void RefreshLibraryListItems()
    {
        foreach (var lib in Libraries)
            lib.RefreshLocalizedStrings(_l10n);
    }

    public event EventHandler<Track>? TrackRemoved;

    public event EventHandler<OrganizeEnqueueEventArgs>? AddToOrganizeRequested;

    public event EventHandler<NavigationSection>? NavigateRequested;

    public event EventHandler? OpenTransfersPageRequested;

    private void OnTracksCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VirtualTrackCollection.TotalCount)
            or nameof(VirtualTrackCollection.IsLoading))
        {
            OnPropertyChanged(nameof(ShowTracksSkeleton));
            OnPropertyChanged(nameof(ShowTracksLoadMoreSkeleton));
            OnPropertyChanged(nameof(ShowTracksList));
            NotifyTracksListSummary();
        }
    }

    private void NotifyTracksListSummary()
    {
        OnPropertyChanged(nameof(TracksListSummary));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ShowTracksSkeleton));
        OnPropertyChanged(nameof(ShowTracksLoadMoreSkeleton));
        OnPropertyChanged(nameof(ShowTracksList));
        OnPropertyChanged(nameof(ShowTracksEmpty));
        OnPropertyChanged(nameof(TracksEmptyTitle));
        OnPropertyChanged(nameof(TracksEmptyDescription));
        OnPropertyChanged(nameof(TracksEmptyActionText));
        OnPropertyChanged(nameof(ShowTracksEmptyAction));
        OnPropertyChanged(nameof(HasUserLibraries));
    }

    private string BuildTracksListSummary()
    {
        var pagingHint = Tracks.HasMore && Tracks.Items.Count > 0
            ? string.Format(_l10n.Get("Library.Summary.PagingHint"), Tracks.Items.Count)
            : string.Empty;

        if (Tracks.IsSearchMode)
        {
            var q = Tracks.SearchQuery ?? string.Empty;
            if (FavoritesOnly)
                return string.Format(_l10n.Get("Library.Summary.FavoritesSearch"), q, Tracks.TotalCount, pagingHint);

            var lib = SelectedLibraryItem;
            if (lib != null && !lib.IsAllLibraries)
                return string.Format(_l10n.Get("Library.Summary.LibrarySearch"), lib.DisplayName, q, Tracks.TotalCount, pagingHint);

            return string.Format(_l10n.Get("Library.Summary.AllTracksSearch"), q, Tracks.TotalCount, pagingHint);
        }

        if (FavoritesOnly)
            return string.Format(_l10n.Get("Library.Summary.Favorites"), Tracks.TotalCount, pagingHint);

        var item = SelectedLibraryItem;
        if (item == null)
            return string.Format(_l10n.Get("Library.Summary.CountOnly"), Tracks.TotalCount, pagingHint);

        if (item.IsAllLibraries)
            return string.Format(_l10n.Get("Library.Summary.AllTracks"), item.TrackCount, item.SizeGbText, pagingHint);

        return string.Format(_l10n.Get("Library.Summary.Library"), item.DisplayName, Tracks.TotalCount, item.SizeGbText, pagingHint);
    }

    private string BuildTrackSearchWatermark()
    {
        if (FavoritesOnly)
            return _l10n.Get("Library.SearchWatermark.Favorites");

        var lib = SelectedLibraryItem;
        if (lib != null && !lib.IsAllLibraries)
            return string.Format(_l10n.Get("Library.SearchWatermark.Library"), lib.DisplayName);

        return _l10n.Get("Library.SearchWatermark.All");
    }

    private void InvalidateLoadedScope() => _loadedScope = null;

    private (long? LibraryId, bool FavoritesOnly, string? SearchQuery) CurrentLoadScope() =>
        (SelectedLibraryItem?.LibraryId, FavoritesOnly, Tracks.SearchQuery);

    [RelayCommand]
    private void ApplyTrackSearch()
    {
        Tracks.SetSearchQuery(TrackSearchText);
        InvalidateLoadedScope();
        ScheduleTracksReload();
    }

    [RelayCommand]
    private void ClearTrackSearch()
    {
        if (string.IsNullOrEmpty(TrackSearchText) && !Tracks.IsSearchMode)
            return;
        TrackSearchText = string.Empty;
        ApplyTrackSearch();
    }

    [RelayCommand]
    private async Task TracksEmptyPrimaryActionAsync()
    {
        switch (ResolveTracksEmptyKind())
        {
            case TracksEmptyKind.Search:
                ClearTrackSearch();
                break;
            case TracksEmptyKind.Favorites:
                FavoritesOnly = false;
                break;
            case TracksEmptyKind.SingleLibrary:
                await ScanSelectedAsync().ConfigureAwait(true);
                break;
            case TracksEmptyKind.NoSources:
                await PickFolderAndAddAsync().ConfigureAwait(true);
                break;
            default:
                await ScanAllAsync().ConfigureAwait(true);
                break;
        }
    }

    private async Task DebouncedTrackSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct).ConfigureAwait(true);
            ApplyTrackSearch();
        }
        catch (OperationCanceledException)
        {
            /* 用户继续输入 */
        }
    }

    public void ApplyLibrarySearch(string query)
    {
        TrackSearchText = query;
        ApplyTrackSearch();
    }

    [RelayCommand]
    private void ToggleLibraryPane() => IsLibraryPaneOpen = !IsLibraryPaneOpen;

    public async Task InitializeAsync()
    {
        await RefreshFavoriteFlagsAsync();
        await ReloadPlaylistsMenuAsync();
        await RepairOrphanTracksUnderDownloadRootAsync();
        await ReloadLibrariesAsync();
        RestoreSelectedLibraryAfterReload();
        await ApplySourceFilterAsync();
        NotifyTracksListSummary();
    }

    public void SelectLibraryFromView(LibraryListItemViewModel? item)
    {
        if (ReferenceEquals(SelectedLibraryItem, item))
        {
            ScheduleTracksReload();
            return;
        }

        SelectedLibraryItem = item;
        InvalidateLoadedScope();
        ScheduleTracksReload();
    }

    private void ScheduleTracksReload()
    {
        _tracksLoadCts?.Cancel();
        _tracksLoadCts?.Dispose();
        _tracksLoadCts = new CancellationTokenSource();
        var ct = _tracksLoadCts.Token;
        Dispatcher.UIThread.Post(
            () => _ = LoadTracksForSelectionAsync(ct),
            DispatcherPriority.ApplicationIdle);
    }

    private async Task LoadTracksForSelectionAsync(CancellationToken ct)
    {
        if (FavoritesOnly)
        {
            var playlistId = await _services.Favorites.GetPlaylistIdAsync();
            Tracks.SetPlaylistFilter(playlistId);
        }
        else
        {
            var libraryId = SelectedLibraryItem?.LibraryId;
            Tracks.SetLibraryFilter(libraryId, SelectedLibraryItem?.RootPath);
        }

        var scope = CurrentLoadScope();
        if (_loadedScope == scope && Tracks.Items.Count > 0)
            return;

        try
        {
            await Tracks.ReloadAsync(ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;
                ApplyFavoriteFlags();
                ApplyNasPlaybackCacheFlags();
                _loadedScope = scope;
                OnPropertyChanged(nameof(Tracks));
                NotifyTracksListSummary();
            });
        }
        catch (OperationCanceledException) { }
    }

    private async Task OnFavoritesChangedAsync()
    {
        if (!FavoritesOnly) return;
        await ApplySourceFilterAsync();
    }

    private async Task ApplySourceFilterAsync()
    {
        _loadedScope = null;
        Tracks.SetSearchQuery(TrackSearchText);
        Tracks.ClearForReload();
        if (FavoritesOnly)
        {
            var playlistId = await _services.Favorites.GetPlaylistIdAsync();
            Tracks.SetPlaylistFilter(playlistId);
        }
        else
            Tracks.SetLibraryFilter(SelectedLibraryItem?.LibraryId, SelectedLibraryItem?.RootPath);

        await Tracks.ReloadAsync();
        ApplyFavoriteFlags();
        _loadedScope = CurrentLoadScope();
        NotifyTracksListSummary();
        OnPropertyChanged(nameof(Tracks));
    }

    public async Task ReloadAfterMetadataSaveAsync()
    {
        _loadedScope = null;
        await Tracks.ReloadAsync().ConfigureAwait(false);
        await RefreshFavoriteFlagsAsync().ConfigureAwait(false);
        _loadedScope = CurrentLoadScope();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var item in Tracks.Items)
                item.SyncDisplayFromTrack();

            NotifyTracksListSummary();
            Tracks.NotifyItemsContentChanged();
            OnPropertyChanged(nameof(Tracks));
        });
    }

    /// <summary>保存元数据后，在重载曲库前先用已写入的字段刷新当前已加载行。</summary>
    public void RefreshTrackListItemFromSaved(Track saved)
    {
        foreach (var item in Tracks.Items)
            item.ApplyMetadataFrom(saved);

        Tracks.NotifyItemsContentChanged();
    }

    [RelayCommand]
    private async Task LoadMoreTracksAsync() => await Tracks.LoadMoreAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshLibraryPanelAsync();

    [RelayCommand]
    private async Task ToggleFavoriteAsync(TrackItemViewModel? item)
    {
        if (item == null || item.Track.Id <= 0) return;
        try
        {
            var added = await _services.Favorites.ToggleAsync(item.Track.Id);
            if (added) _favoriteTrackIds.Add(item.Track.Id);
            else _favoriteTrackIds.Remove(item.Track.Id);
            item.IsFavorite = added;
            StatusMessage = string.Empty;
            MessageService.Instance.ShowInfo(added ? _l10n.Get("Library.Tracks.FavoriteAdded") : _l10n.Get("Library.Tracks.FavoriteRemoved"));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.FavoriteFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveTrackIndexOnlyAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item ?? PendingRemoveTrack, deleteFile: false).ConfigureAwait(true);

    [RelayCommand]
    private async Task RemoveTrackWithFileAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item ?? PendingRemoveTrack, deleteFile: true).ConfigureAwait(true);

    private async Task ExecuteRemoveTrackAsync(TrackItemViewModel? item, bool deleteFile)
    {
        if (item == null || item.Track.Id <= 0) return;

        var hasFile = RemoveTrackConfirmText.CanDeleteFile(item);
        if (deleteFile && !hasFile)
            deleteFile = false;

        try
        {
            var result = await _services.LibraryManager.RemoveTrackAsync(item.Track, deleteFile).ConfigureAwait(true);
            if (!result.IndexRemoved)
            {
                StatusMessage = _l10n.Get("Library.Tracks.RemoveFailed");
                return;
            }

            _player.RemoveDeletedTrack(item.Track);
            _favoriteTrackIds.Remove(item.Track.Id);
            Tracks.RemoveLocalItem(item.Track.Id);
            NotifyTracksListSummary();
            await RefreshLibraryPanelAsync(SelectedLibraryItem?.LibraryId);

            TrackRemoved?.Invoke(this, item.Track);

            StatusMessage = deleteFile switch
            {
                true when result.FileDeleteFailed => _l10n.Get("Library.Tracks.RemoveIndexFileDeleteFailed"),
                true when result.FileDeleted => _l10n.Get("Library.Tracks.RemoveIndexAndFile"),
                true => _l10n.Get("Library.Tracks.RemoveIndexNoFile"),
                _ => _l10n.Get("Library.Tracks.RemoveIndexKeepFile")
            };
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.DeleteFailed", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(PendingRemoveTrack, item))
                PendingRemoveTrack = null;
        }
    }

    [RelayCommand]
    private async Task RemoveTrackAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item, deleteFile: false).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedLibrary))]
    private async Task DeleteSelectedLibraryAsync()
    {
        var item = SelectedLibraryItem;
        if (item is not { IsAllLibraries: false, Library: { } lib })
            return;

        try
        {
            await _services.LibraryManager.DeleteLibraryAsync(lib.Id);
            InvalidateLoadedScope();
            await ReloadLibrariesAsync();
            RestoreSelectedLibraryAfterReload();
            ScheduleTracksReload();
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Library.Tracks.LibraryRemoved"), item.DisplayName));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.LibraryRemoveFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddToPlaylistAsync((TrackItemViewModel Track, long PlaylistId) args)
    {
        var (trackVm, playlistId) = args;
        var trackId = trackVm?.Track.Id ?? 0;
        var trackTitle = trackVm?.Track.Title ?? _l10n.Get("Library.Tracks.CurrentTrack");
        if (trackId <= 0 || playlistId <= 0)
        {
            StatusMessage = _l10n.Get("Library.Tracks.PlaylistInvalid");
            MessageService.Instance.ShowError(StatusMessage);
            return;
        }

        try
        {
            var playlist = PlaylistsForMenu.FirstOrDefault(p => p.Id == playlistId);
            var playlistName = playlist?.Name ?? _l10n.Get("Library.Tracks.TargetPlaylist");
            var alreadyExists = await _services.PlaylistService
                .IsTrackInPlaylistAsync(playlistId, trackId)
                .ConfigureAwait(true);
            if (alreadyExists)
            {
                StatusMessage = string.Format(_l10n.Get("Library.Tracks.PlaylistDuplicate"), trackTitle, playlistName);
                MessageService.Instance.ShowInfo(StatusMessage);
                return;
            }

            await _services.PlaylistService.AddTrackAsync(playlistId, trackId).ConfigureAwait(true);
            StatusMessage = string.Format(_l10n.Get("Library.Tracks.PlaylistAdded"), trackTitle, playlistName);
            MessageService.Instance.ShowSuccess(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.PlaylistAddFailed", ex.Message);
            MessageService.Instance.ShowError(StatusMessage);
        }
    }

    public async Task OnDownloadCompletedAsync(DownloadItem item)
    {
        var focusLibraryId = await EnsureDownloadedTrackLibraryAsync(item);

        _postDownloadRefreshCts?.Cancel();
        _postDownloadRefreshCts?.Dispose();
        _postDownloadRefreshCts = new CancellationTokenSource();
        var debounce = _postDownloadRefreshCts.Token;

        try
        {
            await Task.Delay(700, debounce);
            if (focusLibraryId.HasValue)
                await _services.LibraryManager.ScanLibraryAsync(focusLibraryId.Value, incremental: true, debounce);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await RefreshLibraryPanelAsync(focusLibraryId);

        if (focusLibraryId.HasValue)
        {
            var libName = Libraries.FirstOrDefault(l => l.LibraryId == focusLibraryId)?.DisplayName;
            StatusMessage = string.IsNullOrEmpty(libName)
                ? string.Format(_l10n.Get("Library.Tracks.Ingested"), item.DisplayTitle)
                : string.Format(_l10n.Get("Library.Tracks.IngestedToLibrary"), libName, item.DisplayTitle);
        }
        else
            StatusMessage = string.Format(_l10n.Get("Library.Tracks.Ingested"), item.DisplayTitle);
    }

    public async Task RefreshLibraryPanelAsync(long? focusLibraryId = null)
    {
        await RefreshFavoriteFlagsAsync();
        await ReloadPlaylistsMenuAsync();
        await RepairOrphanTracksUnderDownloadRootAsync();
        await ReloadLibrariesAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ApplyLibrarySelectionAfterReload(focusLibraryId);
            NotifyTracksListSummary();
        });

        _tracksLoadCts?.Cancel();
        InvalidateLoadedScope();
        await ApplySourceFilterAsync();
    }

    private async Task<long?> EnsureDownloadedTrackLibraryAsync(DownloadItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath) || !File.Exists(item.TargetPath))
            return item.LibraryId;

        await RepairOrphanTracksUnderDownloadRootAsync();

        var libs = await _services.LibraryManager.GetAllAsync();
        var libId = LibraryPathMatcher.ResolveLibraryId(item.TargetPath, libs) ?? item.LibraryId;
        if (libId.HasValue)
            await _services.Tracks.SetLibraryIdByFilePathAsync(item.TargetPath, libId.Value);

        return libId;
    }

    private async Task ReloadLibrariesAsync()
    {
        var enabledLibs = (await _services.LibraryManager.GetAllAsync())
            .Where(l => l.IsEnabled)
            .ToList();

        var allCount = await _services.Tracks.CountAsync();
        var allSize = await NasLibraryUiHelper.SumEnabledLibrariesSizeAsync(_services.Library, enabledLibs);

        var allItem = Libraries.FirstOrDefault(l => l.IsAllLibraries);
        if (allItem == null)
        {
            Libraries.Insert(0, new LibraryListItemViewModel(
                null, _l10n.Get("Library.AllTracks"), _l10n.Get("Library.AllLibrariesSummary"), allCount, allSize));
        }
        else
            allItem.UpdateStats(allCount, allSize);

        foreach (var stale in Libraries.Where(l => !l.IsAllLibraries && enabledLibs.All(x => x.Id != l.LibraryId)).ToList())
            Libraries.Remove(stale);

        foreach (var lib in enabledLibs)
        {
            var count = await _services.Tracks.CountByLibraryScopeAsync(lib.Id, lib.RootPath);
            var size = await NasLibraryUiHelper.ResolveLibrarySizeBytesAsync(_services.Library, lib);
            var item = Libraries.FirstOrDefault(l => l.LibraryId == lib.Id);
            if (item != null)
                item.UpdateStats(count, size);
            else
                Libraries.Add(new LibraryListItemViewModel(lib, lib.Name, lib.RootPath, count, size));
        }

        NasLibraryUiHelper.ApplyNetworkLibraryStatuses(Libraries, _services.Library, _l10n);
        _hasLocalDownloadTarget = (await _services.NasDownloads.GetLocalTargetLibrariesAsync()).Count > 0;
        OnPropertyChanged(nameof(CanDownloadSelectedNasLibrary));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        ApplyLibraryDisabledLabels();
        RefreshLibraryListItems();
    }

    private void ApplyFavoriteFlags()
    {
        foreach (var item in Tracks.Items)
            item.IsFavorite = _favoriteTrackIds.Contains(item.Track.Id);
    }

    private async Task RefreshFavoriteFlagsAsync()
    {
        _favoriteTrackIds.Clear();
        foreach (var id in await _services.Favorites.GetFavoriteTrackIdsAsync())
            _favoriteTrackIds.Add(id);
        ApplyFavoriteFlags();
    }

    private async Task ReloadPlaylistsMenuAsync()
    {
        PlaylistsForMenu.Clear();
        foreach (var p in await _services.PlaylistService.GetAllAsync())
        {
            if (p.Name is FavoritesService.PlaylistName or FavoritesService.LegacyPlaylistName)
                continue;
            PlaylistsForMenu.Add(p);
        }
    }

    private void ApplyLibrarySelectionAfterReload(long? focusLibraryId)
    {
        if (!focusLibraryId.HasValue)
        {
            RestoreSelectedLibraryAfterReload();
            return;
        }

        var visibleInCurrent = SelectedLibraryItem?.IsAllLibraries == true
                               || SelectedLibraryItem?.LibraryId == focusLibraryId;
        if (visibleInCurrent)
            RestoreSelectedLibraryAfterReload();
        else
            SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == focusLibraryId)
                                  ?? Libraries.FirstOrDefault();
    }

    private void RestoreSelectedLibraryAfterReload()
    {
        var selectedId = SelectedLibraryItem?.LibraryId;
        SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == selectedId)
                              ?? Libraries.FirstOrDefault();
    }

    [RelayCommand]
    private async Task PickFolderAndAddAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        try
        {
            var folders = await desktop.MainWindow!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = _l10n.Get("Library.Tracks.PickFolderTitle"),
                AllowMultiple = false
            });
            var folder = folders.FirstOrDefault();
            if (folder == null) return;

            var path = LibraryRepository.NormalizeRootPath(folder.Path.LocalPath);
            var (lib, created) = await _services.LibraryManager.AddOrGetLibraryAsync(
                Path.GetFileName(path), path);

            await ReloadLibrariesAsync();
            InvalidateLoadedScope();
            SelectedLibraryItem = Libraries.FirstOrDefault(l => l.Library?.Id == lib.Id)
                                  ?? Libraries.FirstOrDefault();
            ScheduleTracksReload();

            StatusMessage = string.Empty;
            if (created)
                MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Library.Tracks.LibraryAdded"), path));
            else
                MessageService.Instance.ShowInfo(_l10n.Get("Library.Tracks.LibraryAlreadyExists"));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.LibraryAddFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ConnectNasLibraryAsync()
    {
        var choice = await _appModal.ShowConnectNasLibraryAsync(_services.NasLibraries, _l10n).ConfigureAwait(true);
        if (choice != ConnectNasLibraryDialogChoice.Connected)
            return;

        await ReloadLibrariesAsync();
        InvalidateLoadedScope();
        var nasItem = Libraries.LastOrDefault(l => l.IsNetworkSource)
                      ?? Libraries.FirstOrDefault(l => !l.IsAllLibraries);
        if (nasItem != null)
            SelectedLibraryItem = nasItem;
        ScheduleTracksReload();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.NasAdded"));
    }

    [RelayCommand]
    private async Task ConnectWebDavLibraryAsync()
    {
        var choice = await _appModal.ShowConnectWebDavLibraryAsync(_services.WebDavLibraries, _l10n)
            .ConfigureAwait(true);
        if (choice != ConnectNasLibraryDialogChoice.Connected)
            return;

        await ReloadLibrariesAsync();
        InvalidateLoadedScope();
        var remoteItem = Libraries.LastOrDefault(l => l.IsNetworkSource)
                         ?? Libraries.FirstOrDefault(l => !l.IsAllLibraries);
        if (remoteItem != null)
            SelectedLibraryItem = remoteItem;
        ScheduleTracksReload();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.WebDavAdded"));
    }

    [RelayCommand]
    private async Task ReconnectNasLibraryAsync()
    {
        if (SelectedLibraryItem?.Library is not { } lib)
            return;

        var libName = SelectedLibraryItem.DisplayName;
        NasConnectionTestResult? result = lib.SourceType switch
        {
            LibrarySourceType.NetworkWebDav =>
                await _services.WebDavLibraries.ReconnectLibraryAsync(lib.Id).ConfigureAwait(true),
            LibrarySourceType.NetworkNas =>
                await _services.NasLibraries.ReconnectLibraryAsync(lib.Id).ConfigureAwait(true),
            _ => null
        };

        if (result == null)
            return;

        await ReloadLibrariesAsync();
        OnPropertyChanged(nameof(CanReconnectSelectedNas));

        if (result.Success)
        {
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Nas.Reconnect.Success"), libName));
        }
        else
        {
            var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
            StatusMessage = message;
            MessageService.Instance.ShowError(string.Format(_l10n.Get("Nas.Reconnect.Failed"), message));
        }
    }

    [RelayCommand]
    private async Task DownloadNasLibraryToLocalAsync()
    {
        if (SelectedLibraryItem?.Library is not { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav } remoteLib)
            return;

        try
        {
            var targetId = App.Settings.Current.NasDownloadTargetLibraryId;
            var result = await _services.NasDownloads
                .EnqueueLibraryDownloadAsync(remoteLib, targetId)
                .ConfigureAwait(true);

            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(NasLibraryUiHelper.FormatBatchDownloadMessage(result, _l10n));
            if (result.Enqueued > 0)
                RequestOpenTransfersPage();
        }
        catch (Exception ex)
        {
            var message = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            StatusMessage = message;
            MessageService.Instance.ShowError(string.Format(_l10n.Get("Nas.Download.Failed"), message));
        }
    }

    [RelayCommand]
    private async Task DownloadNasTrackToLibraryAsync(TrackItemViewModel? item)
    {
        if (item == null || !IsNasTrack(item.Track))
            return;

        try
        {
            var targetId = App.Settings.Current.NasDownloadTargetLibraryId;
            var (library, enqueued) = await _services.NasDownloads
                .EnqueueDownloadToLocalLibraryAsync(item.Track, targetId)
                .ConfigureAwait(true);

            if (!enqueued)
            {
                MessageService.Instance.ShowInfo(_l10n.Get("Nas.Download.AlreadyExists"));
                return;
            }

            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Nas.Download.Enqueued"), library.Name));
            RequestOpenTransfersPage();
        }
        catch (Exception ex)
        {
            var message = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            StatusMessage = message;
            MessageService.Instance.ShowError(string.Format(_l10n.Get("Nas.Download.Failed"), message));
        }
    }

    [RelayCommand]
    private async Task ScanSelectedAsync()
    {
        if (SelectedLibraryItem?.Library == null)
        {
            await ScanAllAsync();
            return;
        }

        IsScanning = true;
        ResetScanProgressCounters();
        _services.Scanner.ProgressChanged += OnScanProgress;
        try
        {
            var lib = SelectedLibraryItem.Library;
            var result = await _services.LibraryManager.ScanLibraryAsync(lib.Id, incremental: false);
            InvalidateLoadedScope();
            await RefreshLibraryPanelAsync(lib.Id);
            StatusMessage = string.Empty;
            if (result.RootMissing)
                MessageService.Instance.ShowError(NasLibraryUiHelper.FormatScanSummary(result, _l10n));
            else
                MessageService.Instance.ShowSuccess($"{NasLibraryUiHelper.FormatScanSummary(result, _l10n)}（{SelectedLibraryItem.DisplayName}）");
        }
        finally
        {
            _services.Scanner.ProgressChanged -= OnScanProgress;
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        IsScanning = true;
        ResetScanProgressCounters();
        _services.Scanner.ProgressChanged += OnScanProgress;
        try
        {
            await _services.Scanner.ScanAllEnabledAsync(incremental: false);
            InvalidateLoadedScope();
            await RefreshLibraryPanelAsync(SelectedLibraryItem?.LibraryId);
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.ScanAllDone"));
        }
        finally
        {
            _services.Scanner.ProgressChanged -= OnScanProgress;
            IsScanning = false;
        }
    }

    private void OnScanProgress(object? sender, ScanProgressEventArgs e)
    {
        ScanProgress = e.Percent;
        _scanAcoustIdAttempts = e.AcoustIdLookupAttempts;
        _scanAcoustIdMatches = e.AcoustIdLookupMatches;
        OnPropertyChanged(nameof(ScanStatusText));
    }

    private void ResetScanProgressCounters()
    {
        ScanProgress = 0;
        _scanAcoustIdAttempts = 0;
        _scanAcoustIdMatches = 0;
        OnPropertyChanged(nameof(ScanStatusText));
    }

    private string FormatScanProgressText(double percent, int matches, int attempts) =>
        attempts > 0
            ? string.Format(_l10n.Get("Library.ScanProgressWithAcoustId"), percent, matches, attempts)
            : string.Format(_l10n.Get("Library.ScanProgress"), percent);

    private async Task RepairOrphanTracksUnderDownloadRootAsync() =>
        await RepairAllEnabledLibraryAssignmentsAsync();

    private async Task RepairAllEnabledLibraryAssignmentsAsync()
    {
        var libs = await _services.LibraryManager.GetAllAsync();
        foreach (var lib in libs.Where(l => l.IsEnabled))
        {
            var root = LibraryRepository.NormalizeRootPath(lib.RootPath);
            if (!Directory.Exists(root)) continue;
            await _services.Tracks.AssignOrphansUnderRootAsync(lib.Id, root);
            await _services.Tracks.ClaimTracksUnderRootForScanAsync(lib.Id, root, libs);
            await _services.Tracks.RemoveDuplicateNormalizedPathsAsync(lib.Id);
            await RepairZeroDurationsUnderRootAsync(lib.Id, root);
        }
    }

    private async Task RepairZeroDurationsUnderRootAsync(long libraryId, string rootPath)
    {
        var tracks = await _services.Tracks.GetAllUnderLibraryScopeAsync(libraryId, rootPath);
        foreach (var track in tracks.Where(t => t.DurationMs <= 500))
        {
            TrackMetadataHelper.EnsureDuration(track);
            if (track.DurationMs > 0)
                await _services.Tracks.UpsertAsync(track);
        }
    }

    public void RequestAddTrackToOrganize(TrackItemViewModel item)
    {
        if (item.Track.Id <= 0)
            return;

        var label = string.Format(_l10n.Get("Library.Tracks.OrganizeFromTrack"), item.Title);
        AddToOrganizeRequested?.Invoke(
            this,
            new OrganizeEnqueueEventArgs([item.Track], label, append: true, organizeCategoryIndex: 0));
    }

    [RelayCommand]
    private async Task AddCurrentListToOrganizeAsync()
    {
        var total = Tracks.TotalCount;
        if (total == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Library.Tracks.ListEmpty"));
            return;
        }

        if (total > MaxOrganizeEnqueueCount)
        {
            var ok = await _appModal.ShowSimpleConfirmAsync(
                _l10n.Get("Library.Tracks.OrganizeEnqueueTitle"),
                string.Format(_l10n.Get("Library.Tracks.OrganizeEnqueueTruncated"), total, MaxOrganizeEnqueueCount),
                _l10n.Get("Common.Continue"),
                _l10n.Get("Common.Cancel")).ConfigureAwait(true);
            if (!ok)
                return;
        }
        else if (total > 500)
        {
            var ok = await _appModal.ShowSimpleConfirmAsync(
                _l10n.Get("Library.Tracks.OrganizeEnqueueTitle"),
                string.Format(_l10n.Get("Library.Tracks.OrganizeEnqueueAll"), total),
                _l10n.Get("Common.Continue"),
                _l10n.Get("Common.Cancel")).ConfigureAwait(true);
            if (!ok)
                return;
        }

        var tracks = await ResolveTracksForOrganizeEnqueueAsync().ConfigureAwait(true);
        if (tracks.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Library.Tracks.OrganizeNothingToAdd"));
            return;
        }

        var sourceLabel = BuildOrganizeSourceLabel();
        AddToOrganizeRequested?.Invoke(
            this,
            new OrganizeEnqueueEventArgs(tracks, sourceLabel, append: true, organizeCategoryIndex: 0));
    }

    [RelayCommand]
    private void OpenOrganizeTools()
    {
        AddToOrganizeRequested?.Invoke(
            this,
            new OrganizeEnqueueEventArgs([], _l10n.Get("Library.Tracks.OpenOrganizeTools"), append: true, organizeCategoryIndex: 0));
    }

    [RelayCommand]
    private void OpenLibrarySources() =>
        NavigateRequested?.Invoke(this, NavigationSection.Libraries);

    private string BuildOrganizeSourceLabel()
    {
        if (FavoritesOnly)
            return Tracks.IsSearchMode
                ? string.Format(_l10n.Get("Library.Tracks.OrganizeSource.FavoritesSearch"), Tracks.SearchQuery)
                : _l10n.Get("Library.Tracks.OrganizeSource.Favorites");
        var lib = SelectedLibraryItem?.DisplayName ?? _l10n.Get("Library.AllTracks");
        return Tracks.IsSearchMode
            ? string.Format(_l10n.Get("Library.Tracks.OrganizeSource.LibrarySearch"), lib, Tracks.SearchQuery)
            : string.Format(_l10n.Get("Library.Tracks.OrganizeSource.Library"), lib);
    }

    private async Task<IReadOnlyList<Track>> ResolveTracksForOrganizeEnqueueAsync()
    {
        var take = Math.Min(Tracks.TotalCount, MaxOrganizeEnqueueCount);
        if (FavoritesOnly)
        {
            var playlistId = await _services.Favorites.GetPlaylistIdAsync().ConfigureAwait(true);
            if (Tracks.IsSearchMode)
                return await _services.Tracks.SearchScopedAsync(
                    Tracks.SearchQuery!, null, playlistId, 0, take).ConfigureAwait(true);

            return await _services.Tracks.GetByPlaylistAsync(playlistId, 0, take).ConfigureAwait(true);
        }

        var libraryId = SelectedLibraryItem?.LibraryId;
        var root = SelectedLibraryItem?.RootPath;
        if (Tracks.IsSearchMode)
            return await _services.Tracks.SearchScopedAsync(
                Tracks.SearchQuery!, libraryId, null, 0, take).ConfigureAwait(true);

        if (libraryId.HasValue && !string.IsNullOrWhiteSpace(root)
            && SelectedLibraryItem is { IsAllLibraries: false })
        {
            return await _services.Tracks.GetByLibraryScopeAsync(libraryId.Value, root, 0, take)
                .ConfigureAwait(true);
        }

        return await _services.Tracks.GetByLibraryAsync(libraryId, 0, take).ConfigureAwait(true);
    }
}
