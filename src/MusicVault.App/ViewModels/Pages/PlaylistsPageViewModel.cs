using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Platform;
using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels.Pages;

public enum PlaylistNavCategory
{
    Charts,
    Playlists,
    OnlineFavorites,
    OnlineListenHistory
}

/// <summary>歌单页：左侧分类，右侧默认封面卡片网格，点击进入曲目列表。</summary>
public partial class PlaylistsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly ILocalizationService _l10n;
    private readonly PlayerBarViewModel? _player;
    private readonly AppModalHostViewModel? _appModal;
    private readonly FormDrawerHostViewModel? _formDrawer;
    private readonly OnlineFavoritesService? _onlineFavorites;
    private readonly OnlinePlaylistTracksService? _onlinePlaylistTracks;
    private readonly HashSet<long> _favoriteTrackIds = [];
    private readonly List<MusicChart> _charts = [];
    private string? _chartsLoadError;
    private CancellationTokenSource? _coverLoadCts;
    private long? _loadedPlaylistId;
    private string? _loadedChartId;
    private readonly OnlineTrackResultsPager _onlineDetailPager = new();
    private readonly ResultsListPager<TrackItemViewModel> _playlistTracksPager = new();

    public override string PageTitle => _l10n.Get("Playlists.PageTitle");
    public override bool ShowStandardPageHeader => false;

    public string AllPlaylistsSectionTitle => _l10n.Get("Playlists.Section.All");
    public string ChartsSectionTitle => _l10n.Get("Playlists.Section.Charts");
    public string OnlineFavoritesSectionTitle => _l10n.Get("Playlists.Section.OnlineFavorites");
    public string OnlineListenHistorySectionTitle => _l10n.Get("Playlists.Section.ListenHistory");
    public string ImportLinkWatermark => _l10n.Get("Playlists.ImportLink.Watermark");
    public string ImportPlatformButton => _l10n.Get("Playlists.ImportPlatform");
    public string CreatePlaylistButton => _l10n.Get("Nav.NewPlaylist");
    public string PreviewChartPlaylistButton => _l10n.Get("Playlists.PreviewChartPlaylist");
    public string PreviewOnlinePlaylistButton => _l10n.Get("Playlists.PreviewOnlinePlaylist");
    public string NewPlaylistWatermark => _l10n.Get("Playlists.NewPlaylist.Watermark");
    public string BrowseColPlaylist => _l10n.Get("Playlists.Browse.Col.Playlist");
    public string BrowseColKind => _l10n.Get("Playlists.Browse.Col.Kind");
    public string BrowseColTracks => _l10n.Get("Playlists.Browse.Col.Tracks");
    public string BrowseColCreated => _l10n.Get("Playlists.Browse.Col.Created");
    public string BrowseColActions => _l10n.Get("Playlists.Browse.Col.Actions");
    public string BrowseColSource => _l10n.Get("Playlists.Browse.Col.Source");
    public string EditPlaylistButton => _l10n.Get("Playlists.EditPlaylist");
    public string DeletePlaylistButton => _l10n.Get("Playlists.DeletePlaylist");
    public string MergePlaylistsButton => _l10n.Get("Playlists.MergePlaylists");
    public string MergePlaylistsToolTip => _l10n.Get("Playlists.MergePlaylists.ToolTip");
    public string MoreActionsToolTip => _l10n.Get("Library.MoreActions");
    public string BrowseColMeta => BrowseColKind;
    public string BrowseIntroText => IsPlaylistsCategory
        ? _l10n.Get("Playlists.Browse.Intro.All")
        : string.Empty;
    public string BrowseEmptyTitle => IsPlaylistsCategory
        ? _l10n.Get("Playlists.Empty.All")
        : !string.IsNullOrWhiteSpace(_chartsLoadError)
            ? _l10n.Get("Playlists.Empty.Charts.Failed")
            : _l10n.Get("Playlists.Empty.Charts");
    public string BrowseEmptyDescription => IsPlaylistsCategory
        ? _l10n.Get("Playlists.Empty.All.Desc")
        : !string.IsNullOrWhiteSpace(_chartsLoadError)
            ? ServiceMessageLocalizer.Resolve(_l10n, _chartsLoadError)
            : _l10n.Get("Playlists.Empty.Charts.Desc");
    public string ChartsRetryButtonText => _l10n.Get("Playlists.Charts.Retry");
    public string ChartsOpenSettingsButtonText => _l10n.Get("Playlists.Charts.OpenSettings");
    public string BackToBrowseLabel => IsChartDetail
        ? _l10n.Get("Playlists.BackToCharts")
        : _l10n.Get("Playlists.BackToBrowse");

    public ObservableCollection<PlaylistNavItemViewModel> NavItems { get; } = [];
    public ObservableCollection<PlaylistBrowseCardViewModel> BrowseCards { get; } = [];
    public ObservableCollection<PlaylistItemViewModel> PlatformPlaylists { get; } = [];
    public ObservableCollection<PlaylistItemViewModel> LocalPlaylists { get; } = [];
    public ObservableCollection<TrackItemViewModel> PlaylistTracks { get; } = [];
    public ObservableCollection<OnlineTrackViewModel> ChartTracks { get; } = [];
    public ObservableCollection<OnlineTrackViewModel> PendingOnlineTracks { get; } = [];

    [ObservableProperty] private PlaylistNavItemViewModel? _selectedNavItem;
    [ObservableProperty] private PlaylistItemViewModel? _selectedPlaylist;
    [ObservableProperty] private bool _showingDetail;
    [ObservableProperty] private bool _isChartDetail;
    [ObservableProperty] private string _detailTitle = string.Empty;
    [ObservableProperty] private string _detailDescription = string.Empty;
    [ObservableProperty] private string _importLink = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private bool _isLoadingChartTracks;
    [ObservableProperty] private bool _isLoadingCharts;
    [ObservableProperty] private bool _isOnlineFavoritesDetail;
    [ObservableProperty] private bool _isOnlineListenHistoryDetail;
    [ObservableProperty] private bool _isLoadingMoreOnlineTracks;
    [ObservableProperty] private bool _isLoadingMorePlaylistTracks;

    public PlaylistNavCategory SelectedCategory
    {
        get => SelectedNavItem?.Category ?? PlaylistNavCategory.Charts;
        set
        {
            var item = NavItems.FirstOrDefault(n => n.Category == value);
            if (item != null)
                SelectedNavItem = item;
        }
    }

    public bool IsPlaylistsCategory => SelectedCategory == PlaylistNavCategory.Playlists;
    public bool IsChartsCategory => SelectedCategory == PlaylistNavCategory.Charts;
    public bool IsOnlineFavoritesCategory => SelectedCategory == PlaylistNavCategory.OnlineFavorites;
    public bool IsOnlineListenHistoryCategory => SelectedCategory == PlaylistNavCategory.OnlineListenHistory;
    public bool IsOnlineCollectionCategory => IsOnlineFavoritesCategory || IsOnlineListenHistoryCategory;

    public bool ShowPlaylistToolbar => !ShowingDetail && IsPlaylistsCategory;
    public bool ShowBackToBrowseButton => !IsOnlineCollectionCategory;

    /// <summary>在线收藏 / 试听记录：设置页式标题区（大标题 + 副标题 + 分割线）。</summary>
    public bool ShowOnlineCollectionPageHeader =>
        ShowingDetail && (IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail);

    public bool ShowOnlineListenHistoryGuide =>
        ShowOnlineCollectionPageHeader && IsOnlineListenHistoryDetail;

    public string OnlineListenHistoryIntro => _l10n.Get("Playlists.ListenHistory.Intro");

    public string LoadMoreButtonText => _l10n.Get("Common.LoadMore");
    public string ColumnSelectHeader => _l10n.Get("Organize.Column.Select");
    public string ColumnSongHeader => _l10n.Get("Discover.Column.Song");
    public string ColumnArtistHeader => _l10n.Get("Discover.Column.Artist");
    public string ColumnSourceHeader => _l10n.Get("Discover.Column.Source");
    public string ColumnDurationHeader => _l10n.Get("Discover.Column.Duration");
    public string ColumnCacheHeader => _l10n.Get("Discover.Column.Cache");
    public string ColumnFavoriteHeader => _l10n.Get("Discover.Column.Favorite");
    public string ColumnPlaylistHeader => _l10n.Get("Discover.Column.Playlist");
    public string ColumnActionsHeader => _l10n.Get("Discover.Column.Actions");
    public string ListenHistorySelectAllToolTip => _l10n.Get("Playlists.ListenHistory.SelectAll.ToolTip");
    public string RemoveFromListenHistoryButton => _l10n.Get("Playlists.RemoveFromListenHistory");
    public string RemoveFromListenHistoryToolTip => _l10n.Get("Playlists.RemoveFromListenHistory.ToolTip");
    public string ClearListenHistoryCacheButton => _l10n.Get("Playlists.ClearListenHistoryCache");
    public string ClearListenHistoryCacheToolTip => _l10n.Get("Playlists.ClearListenHistoryCache.ToolTip");
    public string M3uImportLabel => _l10n.Get("Playlists.M3u.ImportLabel");
    public string M3uImportWatermark => _l10n.Get("Playlists.M3u.Watermark");
    public string M3uImportButton => _l10n.Get("Playlists.M3u.ImportButton");
    public string ExportM3uButton => _l10n.Get("Playlists.M3u.ExportButton");
    public string BatchDownloadButton => _l10n.Get("Playlists.BatchDownload");
    public string RetryFailedButton => _l10n.Get("Playlists.RetryFailed");
    public string AddToPlaylistToolTip => _l10n.Get("Discover.ToolTip.AddToPlaylist");
    public bool IsOnlineDownloadAvailable =>
        OnlineDownloadAvailability.IsAvailable(GoMusicApiClient.IsConfigured);
    public string DownloadToolTip => IsOnlineDownloadAvailable
        ? _l10n.Get("Discover.ToolTip.Download")
        : OnlineTrackRowLabels.DownloadDisabledHint;
    public string BatchDownloadToolTip => DownloadToolTip;
    public string EmptyTracksTitle => _l10n.Get("Playlists.Empty.Tracks.Title");
    public string EmptyTracksDescription => _l10n.Get("Playlists.Empty.Tracks.Desc");

    public bool ShowOnlineCollectionDetailDescription =>
        ShowOnlineCollectionPageHeader
        && !IsOnlineListenHistoryDetail
        && !string.IsNullOrEmpty(DetailDescription);

    public bool ShowStandardDetailHeader => ShowingDetail && !ShowOnlineCollectionPageHeader;

    /// <summary>我的歌单浏览：与在线收藏相同的页头（大标题 + 说明 + 通栏分割线 + 工具栏）。</summary>
    public bool ShowPlaylistsBrowsePageHeader => ShowBrowseGrid && IsPlaylistsCategory;

    public string PlaylistsBrowsePageTitle => AllPlaylistsSectionTitle;

    public string PlaylistsBrowsePageSubtitle => _l10n.Get("Playlists.Browse.Subtitle.All");

    public string PlaylistsBrowseSummaryText
    {
        get
        {
            if (!IsPlaylistsCategory || ShowingDetail)
                return string.Empty;

            var total = BrowseCards.Count;
            var tracks = PlatformPlaylists.Sum(p => p.TrackCount) + LocalPlaylists.Sum(p => p.TrackCount);
            if (total == 0)
                return _l10n.Get("Playlists.Browse.Summary.Empty");

            return string.Format(
                _l10n.Get("Playlists.Browse.Summary"),
                total,
                tracks,
                PlatformPlaylists.Count,
                LocalPlaylists.Count);
        }
    }

    public bool ShowStandardDetailDescription =>
        ShowStandardDetailHeader
        && !string.IsNullOrEmpty(DetailDescription);

    public bool ShowBrowseGrid => !ShowingDetail;
    public bool BrowseAsList => !ShowingDetail && IsPlaylistsCategory;
    public bool BrowseAsGrid => !ShowingDetail && IsChartsCategory;
    public bool HasBrowseCards => BrowseCards.Count > 0;
    public bool ShowChartsBrowseSkeleton => IsChartsCategory && !ShowingDetail && IsLoadingCharts;
    public bool ShowBrowseEmpty => !ShowingDetail && BrowseCards.Count == 0 && !IsLoadingCharts;
    public bool ShowChartsEmptyActions =>
        IsChartsCategory && ShowBrowseEmpty && !string.IsNullOrWhiteSpace(_chartsLoadError);
    public bool ShowChartTracksLoadMoreSkeleton =>
        IsLoadingMoreOnlineTracks && HasMoreOnlineDetailTracks
        && (IsChartDetail || IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail);
    public bool HasPendingOnlineTracks => PendingOnlineTracks.Count > 0;
    public event EventHandler? OpenOnlineSourcesSettingsRequested;

    public int SelectedMergeablePlaylistCount =>
        BrowseCards.Count(c => c is { IsPlaylist: true, IsSelected: true, CanMerge: true });

    public bool CanMergeSelectedPlaylists => SelectedMergeablePlaylistCount >= 2;

    public bool ShowPlaylistBulkActions => BrowseAsList && !ShowingDetail;

    public bool IsPlatformPlaylistDetail =>
        ShowingDetail && !IsChartDetail && SelectedPlaylist?.Playlist.Kind == PlaylistKind.Platform;

    public bool IsLocalPlaylistDetail =>
        ShowingDetail && !IsChartDetail && SelectedPlaylist is { Playlist.Kind: not PlaylistKind.Platform };

    public bool ShowPlaylistManageActions => ShowingDetail && !IsChartDetail && SelectedPlaylist != null;

    public bool CanEditSelectedPlaylist => SelectedPlaylist?.CanEdit == true;

    public bool CanDeleteSelectedPlaylist => SelectedPlaylist?.CanDelete == true;

    public bool ShowImportM3uPanel => IsLocalPlaylistDetail;

    public bool ShowExportM3uAction => IsLocalPlaylistDetail;

    public bool ShowPlaylistDetailOverflow =>
        CanEditSelectedPlaylist
        || CanDeleteSelectedPlaylist
        || ShowExportM3uAction
        || IsChartDetail
        || HasMoreOnlineDetailTracks
        || HasMorePlaylistLocalTracks
        || HasPendingOnlineTracks
        || IsLocalPlaylistDetail;

    public bool ShowListenHistoryOverflow =>
        ShowListenHistoryBulkActions || HasMoreOnlineDetailTracks;

    public bool ShowPlaylistTracksList =>
        ShowingDetail && !IsChartDetail && PlaylistTracks.Count > 0;

    public IReadOnlyList<OnlineTrackViewModel> DetailOnlineTracks =>
        (IsChartDetail || IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail) ? ChartTracks : PendingOnlineTracks;

    public bool ShowChartTracksList =>
        ShowingDetail && DetailOnlineTracks.Count > 0 && !IsLoadingChartTracks;

    public bool ShowDetailTracksEmpty =>
        ShowingDetail && !IsLoadingChartTracks && !ShowPlaylistTracksList && !ShowChartTracksList
        && !HasPendingOnlineTracks;

    public bool HasMoreOnlineDetailTracks => _onlineDetailPager.HasMore;

    public bool HasMorePlaylistLocalTracks => _playlistTracksPager.HasMore;

    public IReadOnlyList<Track> ActivePlaylistLocalTracks =>
        _playlistTracksPager.TotalCount > 0
            ? _playlistTracksPager.All.Select(t => t.Track).ToList()
            : PlaylistTracks.Select(t => t.Track).ToList();

    public string OnlineDetailSummaryText => BuildOnlineDetailSummaryText();

    public bool ShowOnlineDetailSummary => ShowChartTracksList && !string.IsNullOrEmpty(OnlineDetailSummaryText);

    public bool ShowListenHistoryTracksList => ShowChartTracksList && IsOnlineListenHistoryDetail;

    public bool ShowStandardChartTracksList => ShowChartTracksList && !IsOnlineListenHistoryDetail;

    public bool ShowListenHistoryBulkActions => ShowingDetail && IsOnlineListenHistoryDetail;

    public int ListenHistorySelectedCount => ChartTracks.Count(t => t.IsSelected);

    public bool CanClearSelectedListenHistoryCache =>
        ChartTracks.Any(t => t is { IsSelected: true, IsPreviewCached: true });

    public bool CanRemoveSelectedFromListenHistory =>
        ChartTracks.Any(t => t.IsSelected);

    [ObservableProperty] private bool _listenHistorySelectAll;

    private bool _syncingListenHistorySelectAll;

    public string PlaylistTracksSummary
    {
        get
        {
            if (!ShowingDetail) return string.Empty;

            if (IsChartDetail || IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail)
            {
                var total = _onlineDetailPager.TotalCount;
                if (total <= 0) return DetailTitle;
                var chartShown = ChartTracks.Count;
                return chartShown < total
                    ? string.Format(_l10n.Get("Playlists.Detail.Summary.TotalShown"), DetailTitle, total, chartShown)
                    : string.Format(_l10n.Get("Playlists.Detail.Summary.Total"), DetailTitle, total);
            }

            if (HasPendingOnlineTracks)
            {
                var onlineTotal = _onlineDetailPager.TotalCount;
                var localTotal = _playlistTracksPager.TotalCount > 0 ? _playlistTracksPager.TotalCount : PlaylistTracks.Count;
                var localShownCount = PlaylistTracks.Count;
                if (onlineTotal > PendingOnlineTracks.Count || localTotal > localShownCount)
                    return string.Format(
                        _l10n.Get("Playlists.Detail.Summary.MixedPaging"),
                        DetailTitle,
                        localTotal,
                        localShownCount,
                        onlineTotal,
                        PendingOnlineTracks.Count);
                return string.Format(
                    _l10n.Get("Playlists.Detail.Summary.Mixed"),
                    DetailTitle,
                    localTotal,
                    onlineTotal);
            }

            var localCount = _playlistTracksPager.TotalCount > 0 ? _playlistTracksPager.TotalCount : PlaylistTracks.Count;
            var localShown = PlaylistTracks.Count;
            if (localCount > localShown)
                return string.Format(_l10n.Get("Playlists.Detail.Summary.LocalPaging"), DetailTitle, localCount, localShown);
            return localCount > 0
                ? string.Format(_l10n.Get("Playlists.Detail.Summary.LocalTotal"), DetailTitle, localCount)
                : DetailTitle;
        }
    }

    public PlaylistsPageViewModel(
        AppServices services,
        ILocalizationService l10n,
        PlayerBarViewModel? player = null,
        AppModalHostViewModel? appModal = null,
        FormDrawerHostViewModel? formDrawer = null,
        OnlineFavoritesService? onlineFavorites = null,
        OnlinePlaylistTracksService? onlinePlaylistTracks = null)
    {
        _services = services;
        _l10n = l10n;
        _player = player;
        _appModal = appModal;
        _formDrawer = formDrawer;
        _onlineFavorites = onlineFavorites;
        _onlinePlaylistTracks = onlinePlaylistTracks;
        NavItems.Add(new PlaylistNavItemViewModel(PlaylistNavCategory.Charts, ChartsSectionTitle));
        NavItems.Add(new PlaylistNavItemViewModel(PlaylistNavCategory.Playlists, AllPlaylistsSectionTitle));
        NavItems.Add(new PlaylistNavItemViewModel(PlaylistNavCategory.OnlineFavorites, OnlineFavoritesSectionTitle));
        NavItems.Add(new PlaylistNavItemViewModel(PlaylistNavCategory.OnlineListenHistory, OnlineListenHistorySectionTitle));
        SelectedNavItem = NavItems[0];
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        if (_onlineFavorites != null)
            _onlineFavorites.Changed += (_, _) => Dispatcher.UIThread.Post(() => _ = RefreshOnlineFavoritesIfActiveAsync());
        _services.OnlinePreviewCache.Changed += (_, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                SyncPreviewCacheStates();
                _ = RefreshOnlineListenHistoryIfActiveAsync();
            });
        if (_onlinePlaylistTracks != null)
            _onlinePlaylistTracks.Changed += (_, args) => Dispatcher.UIThread.Post(() => _ = RefreshSelectedPlaylistOnlineTracksAsync(args.PlaylistId));
        if (_player != null)
            _player.OnlinePreviewStarted += (_, online) =>
                Dispatcher.UIThread.Post(() => BindOnlinePreviewQueue(online));
    }

    private void BindOnlinePreviewQueue(OnlineTrack online)
    {
        var rows = _onlineDetailPager.TotalCount > 0 ? _onlineDetailPager.All : DetailOnlineTracks;
        var models = rows.Select(t => t.Model).ToList();
        if (OnlinePreviewQueue.IndexOf(models, online) < 0)
            return;
        _player?.SetOnlinePreviewQueue(models, online);
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(AllPlaylistsSectionTitle));
        OnPropertyChanged(nameof(ChartsSectionTitle));
        OnPropertyChanged(nameof(OnlineFavoritesSectionTitle));
        OnPropertyChanged(nameof(OnlineListenHistorySectionTitle));
        OnPropertyChanged(nameof(ImportLinkWatermark));
        OnPropertyChanged(nameof(ImportPlatformButton));
        OnPropertyChanged(nameof(CreatePlaylistButton));
        OnPropertyChanged(nameof(BackToBrowseLabel));
        OnPropertyChanged(nameof(PreviewChartPlaylistButton));
        OnPropertyChanged(nameof(PreviewOnlinePlaylistButton));
        OnPropertyChanged(nameof(NewPlaylistWatermark));
        OnPropertyChanged(nameof(BrowseColPlaylist));
        OnPropertyChanged(nameof(BrowseColKind));
        OnPropertyChanged(nameof(BrowseColTracks));
        OnPropertyChanged(nameof(BrowseColCreated));
        OnPropertyChanged(nameof(BrowseColActions));
        OnPropertyChanged(nameof(BrowseColSource));
        OnPropertyChanged(nameof(EditPlaylistButton));
        OnPropertyChanged(nameof(DeletePlaylistButton));
        OnPropertyChanged(nameof(MergePlaylistsButton));
        OnPropertyChanged(nameof(MergePlaylistsToolTip));
        OnPropertyChanged(nameof(MoreActionsToolTip));
        OnPropertyChanged(nameof(BrowseColMeta));
        OnPropertyChanged(nameof(BrowseIntroText));
        OnPropertyChanged(nameof(PlaylistsBrowsePageSubtitle));
        OnPropertyChanged(nameof(BrowseEmptyTitle));
        OnPropertyChanged(nameof(BrowseEmptyDescription));
        OnPropertyChanged(nameof(ChartsRetryButtonText));
        OnPropertyChanged(nameof(ChartsOpenSettingsButtonText));
        OnPropertyChanged(nameof(OnlineListenHistoryIntro));
        OnPropertyChanged(nameof(LoadMoreButtonText));
        OnPropertyChanged(nameof(ColumnSelectHeader));
        OnPropertyChanged(nameof(ColumnSongHeader));
        OnPropertyChanged(nameof(ColumnArtistHeader));
        OnPropertyChanged(nameof(ColumnSourceHeader));
        OnPropertyChanged(nameof(ColumnDurationHeader));
        OnPropertyChanged(nameof(ColumnCacheHeader));
        OnPropertyChanged(nameof(ColumnFavoriteHeader));
        OnPropertyChanged(nameof(ColumnPlaylistHeader));
        OnPropertyChanged(nameof(ColumnActionsHeader));
        OnPropertyChanged(nameof(ListenHistorySelectAllToolTip));
        OnPropertyChanged(nameof(RemoveFromListenHistoryButton));
        OnPropertyChanged(nameof(RemoveFromListenHistoryToolTip));
        OnPropertyChanged(nameof(ClearListenHistoryCacheButton));
        OnPropertyChanged(nameof(ClearListenHistoryCacheToolTip));
        OnPropertyChanged(nameof(M3uImportLabel));
        OnPropertyChanged(nameof(M3uImportWatermark));
        OnPropertyChanged(nameof(M3uImportButton));
        OnPropertyChanged(nameof(ExportM3uButton));
        OnPropertyChanged(nameof(BatchDownloadButton));
        OnPropertyChanged(nameof(RetryFailedButton));
        OnPropertyChanged(nameof(AddToPlaylistToolTip));
        OnPropertyChanged(nameof(IsOnlineDownloadAvailable));
        OnPropertyChanged(nameof(DownloadToolTip));
        OnPropertyChanged(nameof(BatchDownloadToolTip));
        OnPropertyChanged(nameof(EmptyTracksTitle));
        OnPropertyChanged(nameof(EmptyTracksDescription));
        OnPropertyChanged(nameof(PlaylistTracksSummary));
        OnPropertyChanged(nameof(OnlineDetailSummaryText));
        foreach (var item in PlatformPlaylists.Concat(LocalPlaylists))
            item.NotifyLocalizedStrings();
        if (IsOnlineFavoritesDetail)
            DetailDescription = _l10n.Get("Playlists.OnlineFavorites.Desc");
        if (!ShowingDetail)
            RebuildBrowseCards();
        else if (SelectedCategory is PlaylistNavCategory.Playlists or PlaylistNavCategory.Charts)
            RebuildBrowseCards();
        if (NavItems.Count >= 4)
        {
            NavItems[0].Title = ChartsSectionTitle;
            NavItems[1].Title = AllPlaylistsSectionTitle;
            NavItems[2].Title = OnlineFavoritesSectionTitle;
            NavItems[3].Title = OnlineListenHistorySectionTitle;
        }
    }

    partial void OnSelectedNavItemChanged(PlaylistNavItemViewModel? value)
    {
        if (value == null) return;
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(IsPlaylistsCategory));
        OnPropertyChanged(nameof(IsChartsCategory));
        OnPropertyChanged(nameof(IsOnlineFavoritesCategory));
        OnPropertyChanged(nameof(IsOnlineListenHistoryCategory));
        OnPropertyChanged(nameof(IsOnlineCollectionCategory));
        OnPropertyChanged(nameof(ShowPlaylistToolbar));
        OnPropertyChanged(nameof(ShowBackToBrowseButton));
        OnPropertyChanged(nameof(BrowseAsList));
        OnPropertyChanged(nameof(BrowseAsGrid));
        OnPropertyChanged(nameof(ShowPlaylistsBrowsePageHeader));
        OnPropertyChanged(nameof(PlaylistsBrowsePageTitle));
        OnPropertyChanged(nameof(PlaylistsBrowsePageSubtitle));
        OnPropertyChanged(nameof(PlaylistsBrowseSummaryText));
        OnPropertyChanged(nameof(BrowseColMeta));
        OnPropertyChanged(nameof(BrowseEmptyTitle));
        OnPropertyChanged(nameof(BrowseEmptyDescription));
        if (IsOnlineFavoritesCategory)
        {
            _ = EnterOnlineFavoritesDetailAsync();
            return;
        }

        if (IsOnlineListenHistoryCategory)
        {
            _ = EnterOnlineListenHistoryDetailAsync();
            return;
        }

        ExitBrowseDetail();
        if (IsChartsCategory && _charts.Count == 0)
            _ = LoadChartsAsync();
        else
        {
            RebuildBrowseCards();
            _ = LoadBrowseCardCoversAsync();
        }
    }

    /// <summary>内置 API 就绪后补拉榜单（避免启动竞态导致空列表）。</summary>
    public async Task RefreshChartsIfNeededAsync()
    {
        if (_charts.Count > 0)
            return;

        await LoadChartsAsync().ConfigureAwait(true);
    }

    private async Task LoadChartsAsync(bool forceRefresh = false)
    {
        if (IsLoadingCharts)
            return;

        IsLoadingCharts = true;
        NotifyChartsBrowseUi();
        try
        {
            var (charts, chartError) = await NetEaseChartsService.FetchChartsAsync(forceRefresh).ConfigureAwait(true);
            _chartsLoadError = chartError;
            _charts.Clear();
            _charts.AddRange(charts);

            if (!string.IsNullOrWhiteSpace(chartError) && IsChartsCategory)
                StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, chartError);
            else if (charts.Count > 0 && IsChartsCategory && string.IsNullOrWhiteSpace(StatusMessage))
                StatusMessage = string.Empty;

            if (charts.Count > 0)
                MusicVault.App.Services.OnlineApiHealthService.Instance.MarkNetEaseHealthyFromSuccessfulRequest();

            if (IsChartsCategory && !ShowingDetail)
            {
                RebuildBrowseCards();
                _ = LoadBrowseCardCoversAsync();
            }
        }
        finally
        {
            IsLoadingCharts = false;
            NotifyChartsBrowseUi();
            NotifyBrowseUi();
        }
    }

    partial void OnIsLoadingChartsChanged(bool value) => NotifyChartsBrowseUi();

    [RelayCommand]
    private async Task RetryLoadChartsAsync()
    {
        NetEaseChartsService.InvalidateCache();
        await LoadChartsAsync(forceRefresh: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenOnlineSourcesSettings()
    {
        StatusMessage = string.Empty;
        OpenOnlineSourcesSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyChartsBrowseUi()
    {
        OnPropertyChanged(nameof(ShowChartsBrowseSkeleton));
        OnPropertyChanged(nameof(ShowBrowseEmpty));
        OnPropertyChanged(nameof(ShowChartsEmptyActions));
        OnPropertyChanged(nameof(BrowseEmptyTitle));
        OnPropertyChanged(nameof(BrowseEmptyDescription));
    }

    partial void OnShowingDetailChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseGrid));
        OnPropertyChanged(nameof(ShowBrowseEmpty));
        OnPropertyChanged(nameof(ShowPlaylistToolbar));
        OnPropertyChanged(nameof(ShowPlaylistsBrowsePageHeader));
        OnPropertyChanged(nameof(PlaylistsBrowsePageTitle));
        OnPropertyChanged(nameof(PlaylistsBrowsePageSubtitle));
        OnPropertyChanged(nameof(PlaylistsBrowseSummaryText));
        OnPropertyChanged(nameof(PlaylistTracksSummary));
        NotifyBrowseUi();
        NotifyDetailTracksUi();
        NotifyPlaylistDetailUi();
    }

    partial void OnIsLoadingChartTracksChanged(bool value) => NotifyDetailTracksUi();

    partial void OnIsChartDetailChanged(bool value)
    {
        OnPropertyChanged(nameof(BackToBrowseLabel));
        NotifyDetailTracksUi();
        NotifyPlaylistDetailUi();
    }

    partial void OnIsOnlineFavoritesDetailChanged(bool value)
    {
        NotifyDetailTracksUi();
        NotifyPlaylistDetailUi();
    }

    partial void OnIsOnlineListenHistoryDetailChanged(bool value)
    {
        NotifyDetailTracksUi();
        NotifyPlaylistDetailUi();
        NotifyListenHistoryBulkUi();
        OnPropertyChanged(nameof(ShowOnlineListenHistoryGuide));
        OnPropertyChanged(nameof(ShowOnlineCollectionDetailDescription));
    }

    partial void OnDetailDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowOnlineCollectionDetailDescription));
        OnPropertyChanged(nameof(ShowStandardDetailDescription));
    }

    partial void OnListenHistorySelectAllChanged(bool value)
    {
        if (_syncingListenHistorySelectAll || !IsOnlineListenHistoryDetail)
            return;

        foreach (var track in ChartTracks)
            track.IsSelected = value;
    }

    [RelayCommand]
    private void ToggleListenHistorySelectAll()
    {
        if (!IsOnlineListenHistoryDetail || ChartTracks.Count == 0)
            return;

        ListenHistorySelectAll = !ListenHistorySelectAll;
    }

    partial void OnSelectedPlaylistChanged(PlaylistItemViewModel? value) => NotifyPlaylistDetailUi();

    private void NotifyPlaylistDetailUi()
    {
        OnPropertyChanged(nameof(IsPlatformPlaylistDetail));
        OnPropertyChanged(nameof(IsLocalPlaylistDetail));
        OnPropertyChanged(nameof(ShowPlaylistManageActions));
        OnPropertyChanged(nameof(CanEditSelectedPlaylist));
        OnPropertyChanged(nameof(CanDeleteSelectedPlaylist));
        OnPropertyChanged(nameof(ShowImportM3uPanel));
        OnPropertyChanged(nameof(ShowExportM3uAction));
        NotifyOverflowChrome();
    }

    private void NotifyOverflowChrome()
    {
        OnPropertyChanged(nameof(ShowPlaylistDetailOverflow));
        OnPropertyChanged(nameof(ShowListenHistoryOverflow));
    }

    private void NotifyDetailTracksUi()
    {
        OnPropertyChanged(nameof(ShowOnlineCollectionPageHeader));
        OnPropertyChanged(nameof(ShowOnlineListenHistoryGuide));
        OnPropertyChanged(nameof(ShowOnlineCollectionDetailDescription));
        OnPropertyChanged(nameof(ShowStandardDetailHeader));
        OnPropertyChanged(nameof(ShowStandardDetailDescription));
        OnPropertyChanged(nameof(ShowPlaylistsBrowsePageHeader));
        OnPropertyChanged(nameof(ShowPlaylistTracksList));
        OnPropertyChanged(nameof(DetailOnlineTracks));
        OnPropertyChanged(nameof(ShowChartTracksList));
        OnPropertyChanged(nameof(ShowDetailTracksEmpty));
        OnPropertyChanged(nameof(PlaylistTracksSummary));
        OnPropertyChanged(nameof(HasPendingOnlineTracks));
        OnPropertyChanged(nameof(ShowListenHistoryTracksList));
        OnPropertyChanged(nameof(ShowStandardChartTracksList));
        NotifyListenHistoryBulkUi();
        NotifyOnlineDetailPagingUi();
        NotifyOverflowChrome();
    }

    private void NotifyListenHistoryBulkUi()
    {
        OnPropertyChanged(nameof(ShowListenHistoryBulkActions));
        OnPropertyChanged(nameof(ListenHistorySelectedCount));
        OnPropertyChanged(nameof(CanClearSelectedListenHistoryCache));
        OnPropertyChanged(nameof(CanRemoveSelectedFromListenHistory));
        ClearSelectedListenHistoryCacheCommand.NotifyCanExecuteChanged();
        RemoveSelectedFromListenHistoryCommand.NotifyCanExecuteChanged();
        UpdateListenHistorySelectAllState();
        NotifyOverflowChrome();
    }

    private void UpdateListenHistorySelectAllState()
    {
        if (!IsOnlineListenHistoryDetail || ChartTracks.Count == 0)
        {
            if (ListenHistorySelectAll)
            {
                _syncingListenHistorySelectAll = true;
                ListenHistorySelectAll = false;
                _syncingListenHistorySelectAll = false;
            }

            return;
        }

        var allSelected = ChartTracks.All(t => t.IsSelected);
        if (ListenHistorySelectAll == allSelected)
            return;

        _syncingListenHistorySelectAll = true;
        ListenHistorySelectAll = allSelected;
        _syncingListenHistorySelectAll = false;
    }

    private void WireListenHistorySelection(OnlineTrackViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OnlineTrackViewModel.IsSelected))
                NotifyListenHistoryBulkUi();
        };
    }

    private void NotifyOnlineDetailPagingUi()
    {
        OnPropertyChanged(nameof(HasMoreOnlineDetailTracks));
        OnPropertyChanged(nameof(OnlineDetailSummaryText));
        OnPropertyChanged(nameof(ShowOnlineDetailSummary));
        OnPropertyChanged(nameof(PlaylistTracksSummary));
        NotifyOverflowChrome();
    }

    partial void OnIsLoadingMoreOnlineTracksChanged(bool value)
    {
        NotifyOnlineDetailPagingUi();
        OnPropertyChanged(nameof(ShowChartTracksLoadMoreSkeleton));
    }

    partial void OnIsLoadingMorePlaylistTracksChanged(bool value) => NotifyPlaylistTracksPagingUi();

    private void NotifyBrowseUi()
    {
        OnPropertyChanged(nameof(HasBrowseCards));
        OnPropertyChanged(nameof(ShowBrowseEmpty));
        OnPropertyChanged(nameof(ShowBrowseGrid));
        OnPropertyChanged(nameof(BrowseAsList));
        OnPropertyChanged(nameof(BrowseAsGrid));
        OnPropertyChanged(nameof(PlaylistsBrowseSummaryText));
    }

    public async Task InitializeAsync()
    {
        PlatformPlaylists.Clear();
        LocalPlaylists.Clear();
        foreach (var p in await _services.PlaylistService.GetAllAsync())
        {
            var count = await _services.Playlists.GetTrackCountAsync(p.Id);
            var item = new PlaylistItemViewModel(p, count, _l10n);
            if (p.Kind == PlaylistKind.Platform)
                PlatformPlaylists.Add(item);
            else
                LocalPlaylists.Add(item);
        }

        await LoadChartsAsync().ConfigureAwait(true);

        if (IsOnlineFavoritesCategory)
        {
            await EnterOnlineFavoritesDetailAsync().ConfigureAwait(true);
            return;
        }

        if (IsOnlineListenHistoryCategory)
        {
            await EnterOnlineListenHistoryDetailAsync().ConfigureAwait(true);
            return;
        }

        if (ShowingDetail && SelectedPlaylist != null)
        {
            SelectedPlaylist = FindPlaylistById(SelectedPlaylist.Playlist.Id);
            if (SelectedPlaylist != null)
            {
                _loadedPlaylistId = null;
                await LoadSelectedPlaylistAsync(SelectedPlaylist);
            }
            else
                ExitBrowseDetail();
        }
        else
        {
            RebuildBrowseCards();
            _ = LoadBrowseCardCoversAsync();
        }
    }

    private void RebuildBrowseCards()
    {
        BrowseCards.Clear();

        switch (SelectedCategory)
        {
            case PlaylistNavCategory.Playlists:
                foreach (var p in AllPlaylistsOrdered())
                {
                    var card = PlaylistBrowseCardViewModel.FromPlaylist(p);
                    card.SelectionChanged += (_, _) => OnBrowseCardSelectionChanged();
                    BrowseCards.Add(card);
                }
                break;
            case PlaylistNavCategory.Charts:
                foreach (var c in _charts)
                    BrowseCards.Add(PlaylistBrowseCardViewModel.FromChart(c, _l10n));
                break;
        }

        NotifyBrowseUi();
        AssignBrowseRowIndexes();
        NotifyPlaylistSelectionUi();
    }

    private void AssignBrowseRowIndexes()
    {
        for (var i = 0; i < BrowseCards.Count; i++)
            BrowseCards[i].RowIndex = i + 1;
    }

    private void OnBrowseCardSelectionChanged() => NotifyPlaylistSelectionUi();

    private void NotifyPlaylistSelectionUi()
    {
        OnPropertyChanged(nameof(SelectedMergeablePlaylistCount));
        OnPropertyChanged(nameof(CanMergeSelectedPlaylists));
    }

    private IEnumerable<PlaylistItemViewModel> AllPlaylistsOrdered() =>
        PlatformPlaylists.Concat(LocalPlaylists)
            .OrderByDescending(IsFavoritesPlaylist)
            .ThenByDescending(p => p.Playlist.Kind == PlaylistKind.System)
            .ThenByDescending(p => p.Playlist.UpdatedAt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);

    private static bool IsFavoritesPlaylist(PlaylistItemViewModel item) =>
        item.Playlist.Kind == PlaylistKind.System
        && (string.Equals(item.Name, FavoritesPlaylistNames.PlaylistName, StringComparison.Ordinal)
            || string.Equals(item.Name, FavoritesPlaylistNames.LegacyPlaylistName, StringComparison.Ordinal));

    private OnlineTrackViewModel CreateOnlineTrackVm(OnlineTrack track)
    {
        MusicVault.Services.Search.OnlineTrackQuality.ApplyDownloadCapabilities(track);
        var vm = new OnlineTrackViewModel(track, _services, _player, appModal: _appModal, onlineFavorites: _onlineFavorites);
        vm.OnTrackFeedback = fb => StatusMessage = fb.Message;
        return vm;
    }

    private async Task LoadBrowseCardCoversAsync()
    {
        _coverLoadCts?.Cancel();
        _coverLoadCts = new CancellationTokenSource();
        var ct = _coverLoadCts.Token;
        var cards = BrowseCards.ToList();

        using var gate = new SemaphoreSlim(4);
        foreach (var card in cards)
        {
            if (ct.IsCancellationRequested) return;
            await gate.WaitAsync(ct).ConfigureAwait(false);
            _ = LoadOneCardCoverAsync(card, ct, gate);
        }
    }

    private async Task LoadOneCardCoverAsync(
        PlaylistBrowseCardViewModel card,
        CancellationToken ct,
        SemaphoreSlim gate)
    {
        try
        {
            byte[]? thumb = null;
            if (card.Chart != null)
                thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(card.Chart.CoverUrl ?? string.Empty, CoverFetchSource.NetEase, ct)
                    .ConfigureAwait(false);
            else if (card.Playlist != null)
                thumb = await PlaylistCoverLoader.LoadForPlaylistAsync(_services, card.Playlist.Playlist.Id, ct)
                    .ConfigureAwait(false);

            if (thumb is { Length: > 0 } && !ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => card.CoverThumb = thumb);
        }
        catch (OperationCanceledException) { }
        catch { /* 单张失败忽略 */ }
        finally
        {
            gate.Release();
        }
    }

    public async Task OnDownloadCompletedAsync()
    {
        var selectedId = SelectedPlaylist?.Playlist.Id;
        var wasDetail = ShowingDetail;
        await InitializeAsync();

        if (!wasDetail || !selectedId.HasValue) return;

        SelectedPlaylist = FindPlaylistById(selectedId.Value);
        if (SelectedPlaylist == null)
        {
            ExitBrowseDetail();
            return;
        }

        IsChartDetail = false;
        DetailTitle = SelectedPlaylist.Name;
        DetailDescription = SelectedPlaylist.Playlist.Description ?? string.Empty;
        ShowingDetail = true;
        _loadedPlaylistId = null;
        await LoadSelectedPlaylistAsync(SelectedPlaylist);
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Playlists.Status.DownloadUpdated"));
    }

    private PlaylistItemViewModel? FindPlaylistById(long id) =>
        PlatformPlaylists.FirstOrDefault(p => p.Playlist.Id == id)
        ?? LocalPlaylists.FirstOrDefault(p => p.Playlist.Id == id);

    private async Task LoadSelectedPlaylistAsync(PlaylistItemViewModel item)
    {
        PlaylistTracks.Clear();
        PendingOnlineTracks.Clear();
        _playlistTracksPager.Clear();
        await RefreshFavoriteTrackIdsAsync().ConfigureAwait(true);
        var localTracks = (await _services.PlaylistService.GetTracksAsync(item.Playlist.Id))
            .Select((t, index) =>
            {
                var vm = new TrackItemViewModel(t) { RowIndex = index + 1 };
                vm.IsFavorite = _favoriteTrackIds.Contains(t.Id);
                return vm;
            })
            .ToList();
        _playlistTracksPager.ResetBuffer(localTracks);
        _playlistTracksPager.ResetDisplayed(PlaylistTracks);
        SyncPlaylistTrackRowIndices();

        if (_onlinePlaylistTracks != null)
        {
            var onlineVms = new List<OnlineTrackViewModel>();
            foreach (var onlineTrack in _onlinePlaylistTracks.GetAll(item.Playlist.Id))
                onlineVms.Add(CreateOnlineTrackVm(onlineTrack));

            ApplyOnlineDetailBatch(PendingOnlineTracks, onlineVms);
        }
        else
        {
            _onlineDetailPager.Clear();
        }

        _loadedPlaylistId = item.Playlist.Id;
        NotifyDetailTracksUi();
        NotifyPlaylistTracksPagingUi();
    }

    private void NotifyPlaylistTracksPagingUi()
    {
        OnPropertyChanged(nameof(HasMorePlaylistLocalTracks));
        OnPropertyChanged(nameof(ActivePlaylistLocalTracks));
        OnPropertyChanged(nameof(PlaylistTracksSummary));
        NotifyOverflowChrome();
    }

    [RelayCommand]
    private async Task LoadMorePlaylistTracksAsync()
    {
        if (!_playlistTracksPager.HasMore || IsLoadingMorePlaylistTracks)
            return;

        IsLoadingMorePlaylistTracks = true;
        try
        {
            await Task.Yield();
            _playlistTracksPager.AppendNext(PlaylistTracks);
            SyncPlaylistTrackRowIndices();
            NotifyPlaylistTracksPagingUi();
        }
        finally
        {
            IsLoadingMorePlaylistTracks = false;
        }
    }

    [RelayCommand]
    private async Task TogglePlaylistTrackFavoriteAsync(TrackItemViewModel? item)
    {
        if (item == null || item.Track.Id <= 0)
            return;

        try
        {
            var added = await _services.Favorites.ToggleAsync(item.Track.Id).ConfigureAwait(true);
            if (added)
                _favoriteTrackIds.Add(item.Track.Id);
            else
                _favoriteTrackIds.Remove(item.Track.Id);
            item.IsFavorite = added;
            StatusMessage = string.Empty;
            MessageService.Instance.ShowInfo(added
                ? _l10n.Get("Library.Tracks.FavoriteAdded")
                : _l10n.Get("Library.Tracks.FavoriteRemoved"));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.FavoriteFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveTrackFromPlaylistAsync(TrackItemViewModel? item)
    {
        if (item == null || item.Track.Id <= 0 || SelectedPlaylist == null)
            return;

        try
        {
            await _services.PlaylistService
                .RemoveTrackAsync(SelectedPlaylist.Playlist.Id, item.Track.Id)
                .ConfigureAwait(true);
            _loadedPlaylistId = null;
            await LoadSelectedPlaylistAsync(SelectedPlaylist).ConfigureAwait(true);
            StatusMessage = string.Format(_l10n.Get("Playlists.Status.RemovedFromPlaylist"), item.Title);
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Playlists.Status.RemoveFailed", ex.Message);
        }
    }

    private async Task RefreshFavoriteTrackIdsAsync()
    {
        _favoriteTrackIds.Clear();
        foreach (var id in await _services.Favorites.GetFavoriteTrackIdsAsync().ConfigureAwait(true))
            _favoriteTrackIds.Add(id);
    }

    private void SyncPlaylistTrackRowIndices()
    {
        for (var i = 0; i < PlaylistTracks.Count; i++)
            PlaylistTracks[i].RowIndex = i + 1;
    }

    private async Task LoadChartTracksAsync(MusicChart chart)
    {
        IsLoadingChartTracks = true;
        ChartTracks.Clear();
        PendingOnlineTracks.Clear();
        _onlineDetailPager.Clear();
        NotifyDetailTracksUi();

        try
        {
            var provider = _services.Search.GetProvider(MusicPlatform.NetEase);
            if (provider == null)
            {
                StatusMessage = _l10n.Get("Playlists.Status.NetEaseDisabled");
                return;
            }

            var tracks = await provider.GetPlaylistTracksAsync(chart.Id);
            var vms = tracks.Select(CreateOnlineTrackVm).ToList();
            ApplyOnlineDetailBatch(ChartTracks, vms);

            _loadedChartId = chart.Id;
            StatusMessage = tracks.Count > 0 ? string.Empty : _l10n.Get("Playlists.Status.ChartEmpty");
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            _loadedChartId = null;
        }
        finally
        {
            IsLoadingChartTracks = false;
            IsOnlineFavoritesDetail = false;
            IsOnlineListenHistoryDetail = false;
            NotifyDetailTracksUi();
            OnPropertyChanged(nameof(PlaylistTracksSummary));
        }
    }

    private async Task EnterOnlineFavoritesDetailAsync()
    {
        ShowingDetail = true;
        IsChartDetail = false;
        IsOnlineFavoritesDetail = true;
        IsOnlineListenHistoryDetail = false;
        SelectedPlaylist = null;
        PlaylistTracks.Clear();
        PendingOnlineTracks.Clear();
        ChartTracks.Clear();
        DetailTitle = OnlineFavoritesSectionTitle;
        DetailDescription = _l10n.Get("Playlists.OnlineFavorites.Desc");

        if (_onlineFavorites == null)
        {
            NotifyDetailTracksUi();
            return;
        }

        var vms = _onlineFavorites.GetAll().Select(CreateOnlineTrackVm).ToList();
        ApplyOnlineDetailBatch(ChartTracks, vms);

        await Task.CompletedTask;
        NotifyDetailTracksUi();
        OnPropertyChanged(nameof(PlaylistTracksSummary));
    }

    private async Task RefreshOnlineFavoritesIfActiveAsync()
    {
        if (!IsOnlineFavoritesCategory)
            return;

        await EnterOnlineFavoritesDetailAsync().ConfigureAwait(true);
    }

    private async Task EnterOnlineListenHistoryDetailAsync()
    {
        ShowingDetail = true;
        IsChartDetail = false;
        IsOnlineFavoritesDetail = false;
        IsOnlineListenHistoryDetail = true;
        SelectedPlaylist = null;
        PlaylistTracks.Clear();
        PendingOnlineTracks.Clear();
        ChartTracks.Clear();
        DetailTitle = OnlineListenHistorySectionTitle;
        DetailDescription = string.Empty;

        ListenHistorySelectAll = false;
        var vms = _services.OnlinePreviewCache.GetRecentListenTracks().Select(CreateOnlineTrackVm).ToList();
        foreach (var vm in vms)
        {
            vm.IsSelected = false;
            WireListenHistorySelection(vm);
        }

        ApplyOnlineDetailBatch(ChartTracks, vms);

        await Task.CompletedTask;
        NotifyDetailTracksUi();
        OnPropertyChanged(nameof(PlaylistTracksSummary));
    }

    [RelayCommand(CanExecute = nameof(CanClearSelectedListenHistoryCache))]
    private async Task ClearSelectedListenHistoryCacheAsync()
    {
        if (_appModal == null)
            return;

        var targets = ChartTracks.Where(t => t is { IsSelected: true, IsPreviewCached: true }).ToList();
        if (targets.Count == 0)
            return;

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Playlists.Confirm.ClearCache.Title"),
            string.Format(_l10n.Get("Playlists.Confirm.ClearCache.Description"), targets.Count),
            _l10n.Get("Playlists.Confirm.ClearCache.Confirm"),
            _l10n.Get("Common.Cancel"),
            confirmIsDanger: true).ConfigureAwait(true);
        if (!confirmed)
            return;

        var cleared = 0;
        foreach (var vm in targets)
        {
            if (_services.OnlinePreviewCache.ClearCache(vm.Model))
                cleared++;
            vm.IsSelected = false;
            vm.RefreshPreviewCacheState();
        }

        _syncingListenHistorySelectAll = true;
        ListenHistorySelectAll = false;
        _syncingListenHistorySelectAll = false;
        NotifyListenHistoryBulkUi();
        var cacheMessage = cleared > 0
            ? string.Format(_l10n.Get("Playlists.Status.CacheCleared"), cleared)
            : _l10n.Get("Playlists.Status.CacheClearNone");
        StatusMessage = cacheMessage;
        MessageService.Instance.ShowSuccess(cacheMessage);
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFromListenHistory))]
    private async Task RemoveSelectedFromListenHistoryAsync()
    {
        if (_appModal == null)
            return;

        var targets = ChartTracks.Where(t => t.IsSelected).ToList();
        if (targets.Count == 0)
            return;

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Playlists.Confirm.RemoveFromHistory.Title"),
            string.Format(_l10n.Get("Playlists.Confirm.RemoveFromHistory.Description"), targets.Count),
            _l10n.Get("Playlists.Confirm.RemoveFromHistory.Confirm"),
            _l10n.Get("Common.Cancel"),
            confirmIsDanger: true).ConfigureAwait(true);
        if (!confirmed)
            return;

        var removed = 0;
        foreach (var vm in targets)
        {
            if (_services.OnlinePreviewCache.RemoveFromListenHistory(vm.Model))
                removed++;
        }

        _syncingListenHistorySelectAll = true;
        ListenHistorySelectAll = false;
        _syncingListenHistorySelectAll = false;
        await EnterOnlineListenHistoryDetailAsync().ConfigureAwait(true);
        var removeMessage = removed > 0
            ? string.Format(_l10n.Get("Playlists.Status.RemovedFromHistory"), removed)
            : _l10n.Get("Playlists.Status.RemoveFromHistoryNone");
        StatusMessage = removeMessage;
        MessageService.Instance.ShowSuccess(removeMessage);
    }

    private async Task RefreshOnlineListenHistoryIfActiveAsync()
    {
        if (!IsOnlineListenHistoryCategory)
            return;

        await EnterOnlineListenHistoryDetailAsync().ConfigureAwait(true);
    }

    private void SyncPreviewCacheStates()
    {
        foreach (var row in ChartTracks)
            row.RefreshPreviewCacheState();
        foreach (var row in PendingOnlineTracks)
            row.RefreshPreviewCacheState();
        if (_onlineDetailPager.TotalCount > 0)
        {
            foreach (var row in _onlineDetailPager.All)
                row.RefreshPreviewCacheState();
        }
    }

    private async Task RefreshSelectedPlaylistOnlineTracksAsync(long playlistId)
    {
        if (!ShowingDetail || IsChartDetail || IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail || SelectedPlaylist == null)
            return;
        if (SelectedPlaylist.Playlist.Id != playlistId)
            return;

        await LoadSelectedPlaylistAsync(SelectedPlaylist).ConfigureAwait(true);
    }

    public sealed record PlaylistMenuItem(long Id, string Name);

    public async Task<IReadOnlyList<PlaylistMenuItem>> GetAddablePlaylistsAsync(CancellationToken ct = default)
    {
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        return playlists
            .Where(p => p.Kind != PlaylistKind.System)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlaylistMenuItem(p.Id, p.Name))
            .ToList();
    }

    public async Task AddOnlineTrackToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default)
    {
        if (trackVm == null || playlistId <= 0 || _onlinePlaylistTracks == null)
            return;

        var added = await _onlinePlaylistTracks.AddAsync(playlistId, trackVm.Model, ct).ConfigureAwait(true);
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        var playlistName = playlists.FirstOrDefault(p => p.Id == playlistId)?.Name
            ?? string.Format(_l10n.Get("Playlists.PlaylistFallbackName"), playlistId);
        StatusMessage = added
            ? string.Format(_l10n.Get("Playlists.Status.PlaylistAdded"), trackVm.Title, playlistName)
            : string.Format(_l10n.Get("Playlists.Status.PlaylistDuplicate"), trackVm.Title, playlistName);
    }

    /// <summary>从发现页深链打开榜单/歌单浏览目标。</summary>
    public async Task OpenBrowseTargetAsync(
        MusicChart? chart = null,
        long? playlistId = null,
        PlaylistNavCategory? categoryOnly = null)
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
                await SelectBrowseCardCommand.ExecuteAsync(card).ConfigureAwait(true);
            return;
        }

        if (playlistId is long id)
        {
            SelectedCategory = PlaylistNavCategory.Playlists;
            RebuildBrowseCards();
            var card = BrowseCards.FirstOrDefault(c => c.Playlist?.Playlist.Id == id);
            if (card != null)
                await SelectBrowseCardCommand.ExecuteAsync(card).ConfigureAwait(true);
            return;
        }

        if (categoryOnly is PlaylistNavCategory cat)
        {
            SelectedCategory = cat;
            RebuildBrowseCards();
        }
    }

    [RelayCommand]
    private async Task SelectBrowseCardAsync(PlaylistBrowseCardViewModel? card)
    {
        if (card == null) return;

        DetailTitle = card.Name;
        DetailDescription = card.Description;
        ShowingDetail = true;

        if (card.Playlist != null)
        {
            IsChartDetail = false;
            SelectedPlaylist = card.Playlist;
            if (_loadedPlaylistId != card.Playlist.Playlist.Id)
                await LoadSelectedPlaylistAsync(card.Playlist);
            return;
        }

        if (card.Chart != null)
        {
            IsChartDetail = true;
            SelectedPlaylist = null;
            PlaylistTracks.Clear();
            _loadedChartId = null;
            await LoadChartTracksAsync(card.Chart);
        }
    }

    [RelayCommand]
    private void BackToBrowse()
    {
        ExitBrowseDetail();
        RebuildBrowseCards();
        _ = LoadBrowseCardCoversAsync();
    }

    private void ExitBrowseDetail()
    {
        ShowingDetail = false;
        IsChartDetail = false;
        IsOnlineFavoritesDetail = false;
        SelectedPlaylist = null;
        DetailTitle = string.Empty;
        DetailDescription = string.Empty;
        PlaylistTracks.Clear();
        ChartTracks.Clear();
        PendingOnlineTracks.Clear();
        _onlineDetailPager.Clear();
        _playlistTracksPager.Clear();
        _loadedPlaylistId = null;
        _loadedChartId = null;
        NotifyDetailTracksUi();
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        if (_appModal == null)
            return;

        var result = await _appModal.ShowPlaylistEditAsync(
            _l10n.Get("Playlists.Create.Title"),
            string.Empty,
            null,
            _l10n.Get("Playlists.NewPlaylist.Watermark"),
            _l10n.Get("Playlists.Edit.DescriptionWatermark")).ConfigureAwait(true);
        if (result == null)
            return;

        await _services.PlaylistService.CreateAsync(result.Name, result.Description).ConfigureAwait(true);
        await InitializeAsync().ConfigureAwait(true);
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Playlists.Status.Created"), result.Name));
    }

    [RelayCommand]
    private async Task EditPlaylistAsync(PlaylistBrowseCardViewModel? card)
    {
        var item = card?.Playlist ?? SelectedPlaylist;
        if (item == null || !item.CanEdit || _appModal == null)
            return;

        var result = await _appModal.ShowPlaylistEditAsync(
            _l10n.Get("Playlists.Edit.Title"),
            item.Name,
            item.Playlist.Description,
            _l10n.Get("Playlists.Edit.NameWatermark"),
            _l10n.Get("Playlists.Edit.DescriptionWatermark")).ConfigureAwait(true);
        if (result == null)
            return;

        await _services.PlaylistService.UpdateAsync(item.Playlist.Id, result.Name, result.Description)
            .ConfigureAwait(true);

        var wasOpen = SelectedPlaylist?.Playlist.Id == item.Playlist.Id;
        await InitializeAsync().ConfigureAwait(true);

        if (wasOpen)
        {
            var refreshed = FindPlaylistById(item.Playlist.Id);
            if (refreshed != null)
            {
                SelectedPlaylist = refreshed;
                DetailTitle = refreshed.Name;
                DetailDescription = refreshed.Playlist.Description ?? string.Empty;
            }
        }

        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Playlists.Status.Updated"), result.Name));
    }

    [RelayCommand]
    private async Task DeletePlaylistAsync(PlaylistBrowseCardViewModel? card)
    {
        var item = card?.Playlist ?? SelectedPlaylist;
        if (item == null || !item.CanDelete || _appModal == null)
            return;

        var messageKey = item.Playlist.Kind == PlaylistKind.Platform
            ? "Playlists.Delete.Platform"
            : "Playlists.Delete.Local";
        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Playlists.Delete.Title"),
            string.Format(_l10n.Get(messageKey), item.Name),
            _l10n.Get("Playlists.DeletePlaylist"),
            _l10n.Get("Common.Cancel"),
            confirmIsDanger: true).ConfigureAwait(true);
        if (!confirmed)
            return;

        var wasOpen = SelectedPlaylist?.Playlist.Id == item.Playlist.Id;
        var name = item.Name;
        await _services.PlaylistService.DeleteAsync(item.Playlist.Id).ConfigureAwait(true);
        if (wasOpen)
            ExitBrowseDetail();
        await InitializeAsync().ConfigureAwait(true);
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Playlists.Status.Deleted"), name));
    }

    [RelayCommand]
    private async Task MergeSelectedPlaylistsAsync()
    {
        if (!CanMergeSelectedPlaylists || _appModal == null)
            return;

        var selected = BrowseCards
            .Where(c => c is { IsPlaylist: true, IsSelected: true, CanMerge: true })
            .OrderBy(c => c.RowIndex)
            .ToList();
        if (selected.Count < 2 || selected[0].Playlist == null)
            return;

        var target = selected[0];
        var targetName = target.Name;
        var others = selected.Skip(1).Select(c => c.Name).ToList();
        var message = string.Format(
            _l10n.Get("Playlists.Merge.Confirm"),
            targetName,
            others.Count,
            string.Join("、", others));

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Playlists.Merge.Title"),
            message,
            _l10n.Get("Playlists.MergePlaylists"),
            _l10n.Get("Common.Cancel")).ConfigureAwait(true);
        if (!confirmed)
            return;

        try
        {
            var sourceIds = selected.Skip(1).Select(c => c.Playlist!.Playlist.Id).ToList();
            var result = await _services.PlaylistService.MergeLocalPlaylistsAsync(
                target.Playlist!.Playlist.Id, sourceIds).ConfigureAwait(true);
            await InitializeAsync().ConfigureAwait(true);
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Playlists.Merge.Success"),
                targetName,
                result.MergedPlaylistCount,
                result.TracksAdded));
        }
        catch (Exception ex)
        {
            var errorMessage = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            StatusMessage = errorMessage;
            MessageService.Instance.ShowInfo(errorMessage);
        }
    }

    [RelayCommand]
    private async Task ImportM3uAsync()
    {
        if (SelectedPlaylist == null || string.IsNullOrWhiteSpace(ImportLink)) return;
        if (!File.Exists(ImportLink)) { StatusMessage = _l10n.Get("Playlists.Status.FileNotFound"); return; }
        var n = await _services.PlaylistService.ImportLocalFilesAsync(SelectedPlaylist.Playlist.Id, ImportLink);
        _loadedPlaylistId = null;
        await LoadSelectedPlaylistAsync(SelectedPlaylist);
        StatusMessage = string.Format(_l10n.Get("Playlists.Status.M3uImported"), n);
    }

    [RelayCommand]
    private async Task ImportPlatformPlaylistAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportLink)) return;
        try
        {
            var playlistName = string.Format(
                _l10n.Get("Playlists.PlatformPlaylistName"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            var (pid, tracks) = await _services.PlaylistService.ImportPlatformPlaylistAsync(
                ImportLink, playlistName);
            if (_onlinePlaylistTracks != null && tracks.Count > 0)
                await _onlinePlaylistTracks.AddManyAsync(pid, tracks).ConfigureAwait(true);
            await InitializeAsync();
            var item = FindPlaylistById(pid);
            if (item != null)
            {
                DetailTitle = item.Name;
                DetailDescription = item.Playlist.Description ?? string.Empty;
                IsChartDetail = false;
                ShowingDetail = true;
                SelectedPlaylist = item;
                _loadedPlaylistId = null;
                await LoadSelectedPlaylistAsync(item);
            }

            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(
                string.Format(_l10n.Get("Playlists.Status.PlatformParsed"), tracks.Count));
            NotifyDetailTracksUi();
        }
        catch (Exception ex) { StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, ex.Message); }
    }

    [RelayCommand]
    private async Task LoadMoreOnlineDetailTracksAsync()
    {
        if (!_onlineDetailPager.HasMore || IsLoadingMoreOnlineTracks)
            return;

        IsLoadingMoreOnlineTracks = true;
        try
        {
            await Task.Yield();
            var target = IsChartDetail || IsOnlineFavoritesDetail || IsOnlineListenHistoryDetail
                ? ChartTracks
                : PendingOnlineTracks;
            _onlineDetailPager.AppendNext(target);
            NotifyOnlineDetailPagingUi();
        }
        finally
        {
            IsLoadingMoreOnlineTracks = false;
        }
    }

    private void ApplyOnlineDetailBatch(
        ObservableCollection<OnlineTrackViewModel> target,
        IReadOnlyList<OnlineTrackViewModel> items)
    {
        _onlineDetailPager.ResetBuffer(items);
        _onlineDetailPager.ResetDisplayed(target);
        if (IsOnlineListenHistoryDetail && ReferenceEquals(target, ChartTracks))
        {
            foreach (var vm in items)
                WireListenHistorySelection(vm);
        }

        NotifyOnlineDetailPagingUi();
    }

    public void NotifyOnlineDownloadAvailability()
    {
        OnPropertyChanged(nameof(IsOnlineDownloadAvailable));
        OnPropertyChanged(nameof(DownloadToolTip));
        OnPropertyChanged(nameof(BatchDownloadToolTip));
        BatchDownloadPendingCommand.NotifyCanExecuteChanged();
        BatchDownloadChartCommand.NotifyCanExecuteChanged();
        foreach (var row in PendingOnlineTracks.Concat(ChartTracks))
            row.NotifyOnlineDownloadAvailability();
    }

    private string BuildOnlineDetailSummaryText()
    {
        var total = _onlineDetailPager.TotalCount;
        if (total <= 0)
            return string.Empty;

        var shown = DetailOnlineTracks.Count;
        var parts = new List<string> { string.Format(_l10n.Get("Discover.Status.ResultsTotal"), total) };
        if (shown < total)
            parts.Add(string.Format(_l10n.Get("Discover.Status.ResultsShown"), shown));
        if (_onlineDetailPager.HasMore)
            parts.Add(_l10n.Get("Discover.Status.ResultsLoadMore"));
        return string.Join(" · ", parts);
    }

    private bool CanBatchDownloadOnline() => IsOnlineDownloadAvailable;

    [RelayCommand(CanExecute = nameof(CanBatchDownloadOnline))]
    private async Task BatchDownloadPendingAsync()
    {
        if (!CanBatchDownloadOnline())
            return;
        var models = _onlineDetailPager.TotalCount > 0
            ? _onlineDetailPager.All.Select(t => t.Model).ToList()
            : PendingOnlineTracks.Select(t => t.Model).ToList();
        var result = await _services.PlaylistService.BatchDownloadOnlineTracksAsync(
            models, AudioQuality.High320);
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(
            _l10n.Get("Discover.Status.EnqueueSuccessWithSkip"), result.Enqueued, result.Skipped));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task PreviewPendingOnlinePlaylistAsync()
    {
        var pool = _onlineDetailPager.TotalCount > 0 ? _onlineDetailPager.All : PendingOnlineTracks;
        if (pool.Count == 0 || _player == null)
            return;

        var target = pool[0];
        var platform = target.Model.Platform;
        if (!await EnsureChartPreviewAuthAsync(platform).ConfigureAwait(true))
            return;

        await target.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PreviewChartPlaylistAsync()
    {
        var pool = _onlineDetailPager.TotalCount > 0 ? _onlineDetailPager.All : ChartTracks;
        if (pool.Count == 0 || _player == null)
            return;

        var platform = pool[0].Model.Platform;
        if (!await EnsureChartPreviewAuthAsync(platform).ConfigureAwait(true))
            return;

        _player.IsShuffleOn = true;
        var target = pool[Random.Shared.Next(pool.Count)];
        var statusBefore = StatusMessage;
        await target.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);

        if (target.PreviewBlocked
            && !string.Equals(StatusMessage, statusBefore, StringComparison.Ordinal)
            && ShouldPromptLoginAfterPreviewFailure(StatusMessage))
        {
            await PromptLoginAndOpenDrawerAsync(platform).ConfigureAwait(true);
        }
    }

    private async Task<bool> EnsureChartPreviewAuthAsync(MusicPlatform platform)
    {
        if (!GoMusicApiClient.IsConfigured)
        {
            MessageService.Instance.ShowInfo(_l10n.Get(OnlinePlaybackStatusText.ApiNotConfiguredHintKey));
            await OpenMusicApiAccountDrawerAsync().ConfigureAwait(true);
            return false;
        }

        if (MusicApiLoggedInSources.TryGetSourceKey(platform) is null)
            return true;

        IReadOnlyList<string> loggedIn;
        try
        {
            loggedIn = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync().ConfigureAwait(false);
        }
        catch
        {
            loggedIn = [];
        }

        if (MusicApiLoggedInSources.IsPlatformLoggedIn(platform, loggedIn))
            return true;

        await PromptLoginAndOpenDrawerAsync(platform).ConfigureAwait(true);
        return false;
    }

    private async Task PromptLoginAndOpenDrawerAsync(MusicPlatform platform)
    {
        var label = MusicPlatformCatalog.GetLabel(platform, _l10n.Get("Playlists.Kind.Platform"));
        MessageService.Instance.ShowInfo(string.Format(
            _l10n.Get("Playlists.PreviewChartLoginHint"), label));
        await OpenMusicApiAccountDrawerAsync().ConfigureAwait(true);
    }

    private bool ShouldPromptLoginAfterPreviewFailure(string? statusMessage)
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
            return false;

        return OnlinePlaybackStatusText.IsAuthRelatedPlaybackFailure(statusMessage, _l10n);
    }

    private async Task OpenMusicApiAccountDrawerAsync()
    {
        if (_formDrawer == null)
        {
            StatusMessage = _l10n.Get("Playlists.Status.LoginWindowFailed");
            return;
        }

        var baseUrl = MusicApiConfig.Resolve();
        var prepared = await PlatformConnectionTester.PrepareUnifiedApiAsync(baseUrl).ConfigureAwait(true);
        if (!prepared.Ok)
        {
            StatusMessage = prepared.Message;
            return;
        }

        try
        {
            await _formDrawer.ShowMusicApiAccountAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBatchDownloadOnline))]
    private async Task BatchDownloadChartAsync()
    {
        if (!CanBatchDownloadOnline())
            return;
        var pool = _onlineDetailPager.TotalCount > 0 ? _onlineDetailPager.All : ChartTracks;
        if (pool.Count == 0) return;
        var enqueued = 0;
        var skipped = 0;
        foreach (var vm in pool)
        {
            if (_services.Downloads.Enqueue(vm.Model, AudioQuality.High320))
                enqueued++;
            else
                skipped++;
        }

        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(
            _l10n.Get("Discover.Status.EnqueueSuccessWithSkip"), enqueued, skipped));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportM3uAsync()
    {
        if (SelectedPlaylist == null) return;
        var path = Path.Combine(_services.DataDirectory, $"{SelectedPlaylist.Playlist.Name}.m3u8");
        await _services.PlaylistService.ExportM3uAsync(SelectedPlaylist.Playlist.Id, path);
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Playlists.Status.Exported"), path));
    }

    [RelayCommand]
    private async Task RetryFailedDownloadsAsync()
    {
        _services.Downloads.RetryAllFailed();
        var failures = await _services.PlaylistService.GetFailuresAsync();
        StatusMessage = string.Empty;
        if (failures.Count > 0)
            MessageService.Instance.ShowInfo(string.Format(_l10n.Get("Playlists.Status.RetryQueued"), failures.Count));
        else
            MessageService.Instance.ShowInfo(_l10n.Get("Playlists.Status.NoFailures"));
        await Task.CompletedTask;
    }
}

public sealed class PlaylistNavItemViewModel(PlaylistNavCategory category, string title)
{
    public PlaylistNavCategory Category { get; } = category;
    public string Title { get; set; } = title;
}

public partial class PlaylistBrowseCardViewModel : ViewModelBase
{
    public event EventHandler? SelectionChanged;

    public PlaylistItemViewModel? Playlist { get; init; }
    public MusicChart? Chart { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TrackCountText { get; init; } = string.Empty;
    public string CreatedAtText { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;
    public string MetaColumnText { get; init; } = string.Empty;
    public string DetailLine { get; init; } = string.Empty;
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public bool CanMerge { get; init; }

    public bool IsPlaylist => Playlist != null;
    public bool HasDetailLine => !string.IsNullOrWhiteSpace(DetailLine);
    public bool ShowRowActions => IsPlaylist && (CanEdit || CanDelete);
    public bool ShowSelectCheck => IsPlaylist && CanMerge;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowIndexText))]
    private int _rowIndex;

    [ObservableProperty]
    private bool _isSelected;

    public string RowIndexText => RowIndex > 0 ? RowIndex.ToString() : string.Empty;

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.PlaylistCheckboxName(Name);

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke(this, EventArgs.Empty);

    public PlaylistBrowseCardViewModel()
    {
        TrackRowActionLabels.LabelsChanged += OnLabelsChanged;
    }

    private void OnLabelsChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));

    [ObservableProperty] private byte[]? _coverThumb;

    public bool HasCover => CoverThumb is { Length: > 0 };

    partial void OnCoverThumbChanged(byte[]? value) => OnPropertyChanged(nameof(HasCover));

    public static PlaylistBrowseCardViewModel FromPlaylist(PlaylistItemViewModel playlist) =>
        new()
        {
            Playlist = playlist,
            Name = playlist.Name,
            Subtitle = playlist.Subtitle,
            Description = playlist.Playlist.Description ?? string.Empty,
            TrackCountText = playlist.TrackCountText,
            CreatedAtText = playlist.CreatedAtText,
            KindLabel = playlist.KindLabel,
            MetaColumnText = playlist.MetaColumnText,
            DetailLine = playlist.DetailLine,
            CanEdit = playlist.CanEdit,
            CanDelete = playlist.CanDelete,
            CanMerge = playlist.CanMerge
        };

    public static PlaylistBrowseCardViewModel FromChart(MusicChart chart, ILocalizationService l10n) =>
        new()
        {
            Chart = chart,
            Name = chart.Name,
            Subtitle = ServiceMessageLocalizer.Resolve(l10n, chart.Subtitle),
            Description = chart.Description ?? string.Empty
        };
}

public sealed class PlaylistItemViewModel : ViewModelBase
{
    private readonly ILocalizationService _l10n;

    public PlaylistItemViewModel(Playlist playlist, int trackCount, ILocalizationService l10n)
    {
        Playlist = playlist;
        TrackCount = trackCount;
        _l10n = l10n;
    }

    public Playlist Playlist { get; }
    public string Name => Playlist.Name;
    public int TrackCount { get; }
    public bool ShowPlatformLabel => Playlist.Kind == PlaylistKind.Platform && Playlist.SourcePlatform.HasValue;

    public bool CanEdit => !IsSystemPlaylist;

    public bool CanDelete => !IsSystemPlaylist;

    public bool CanMerge => Playlist.Kind == PlaylistKind.Local && !IsSystemPlaylist;

    public bool IsSystemPlaylist =>
        Playlist.Kind == PlaylistKind.System
        || string.Equals(Playlist.Name, FavoritesPlaylistNames.PlaylistName, StringComparison.Ordinal)
        || string.Equals(Playlist.Name, FavoritesPlaylistNames.LegacyPlaylistName, StringComparison.Ordinal);

    public string PlatformLabel => Playlist.SourcePlatform.HasValue
        ? _l10n.GetPlatformLabel(Playlist.SourcePlatform.Value)
        : string.Empty;

    public string Subtitle
    {
        get
        {
            var count = string.Format(_l10n.Get("Playlists.TrackCountFormat"), TrackCount);
            return ShowPlatformLabel ? $"{PlatformLabel} · {count}" : count;
        }
    }

    public string TrackCountText => string.Format(_l10n.Get("Playlists.TrackCountFormat"), TrackCount);

    public string CreatedAtText => Playlist.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string UpdatedAtText => Playlist.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string KindLabel => Playlist.Kind switch
    {
        PlaylistKind.System => _l10n.Get("Playlists.Kind.System"),
        PlaylistKind.Platform => ShowPlatformLabel ? PlatformLabel : _l10n.Get("Playlists.Kind.Platform"),
        _ => _l10n.Get("Playlists.Kind.Local")
    };

    public string MetaColumnText => Playlist.Kind == PlaylistKind.Platform
        ? (ShowPlatformLabel ? PlatformLabel : _l10n.Get("Playlists.Kind.Platform"))
        : KindLabel;

    public string DetailLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Playlist.SourceUrl))
                return Playlist.SourceUrl!;
            if (!string.IsNullOrWhiteSpace(Playlist.Description))
                return Playlist.Description;
            return string.Format(_l10n.Get("Playlists.UpdatedAt"), UpdatedAtText);
        }
    }

    public void NotifyLocalizedStrings()
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(TrackCountText));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(MetaColumnText));
        OnPropertyChanged(nameof(DetailLine));
    }
}
