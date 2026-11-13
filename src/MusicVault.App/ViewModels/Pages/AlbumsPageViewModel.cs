using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Browse;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.Views;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Browse;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>专辑浏览页：A–Z 分区、搜索、封面与详情曲目表。</summary>
public partial class AlbumsPageViewModel : PageViewModelBase, ILibrarySearchTarget, IAlbumCatalogOnlineTrackHost
{
    private readonly AppServices _services;
    private readonly PlayerBarViewModel _player;
    private readonly AppModalHostViewModel _appModal;
    private readonly OnlineFavoritesService _onlineFavorites;
    private readonly OnlinePlaylistTrackActions _onlinePlaylistActions;
    private readonly ILocalizationService _l10n;
    private List<AlbumGroup> _allAlbums = [];
    private List<Track> _currentAlbumTracks = [];
    private List<ArtistAlbumTrackItemViewModel> _allDetailTracks = [];
    private readonly ResultsListPager<ArtistAlbumTrackItemViewModel> _albumDetailPager = new();
    private string _detailTrackSearch = string.Empty;
    private readonly Dictionary<string, AlbumListItemViewModel> _albumItems = new(StringComparer.Ordinal);
    private CancellationTokenSource? _albumSearchDebounceCts;
    private CancellationTokenSource? _coverLoadCts;
    private CancellationTokenSource? _albumDetailCts;
    private CancellationTokenSource? _catalogRefreshCts;

    public override bool ShowStandardPageHeader => false;
    public override string PageTitle => string.Empty;
    public override bool ShowPageSubtitle => false;
    public override bool ShowPageBackButton => false;
    public override string PageBackButtonLabel => _l10n.Get("Library.Albums.BackLabel");
    public override ICommand? PageBackCommand => BackToAlbumsCommand;
    public override bool ShowPageHeaderDivider => false;

    public string PageHeaderText => _l10n.Get("Library.Albums.PageHeader");
    public string BrowsePageTitle => _l10n.Get("Library.Albums.Title");
    public string SearchFieldLabel => _l10n.Get("Library.Albums.SearchLabel");
    public string SearchWatermarkText => _l10n.Get("Library.Albums.SearchWatermark");
    public string SearchButtonLabel => _l10n.Get("Common.Search");
    public string ResetButtonLabel => _l10n.Get("Common.Reset");
    public string RefreshMetadataButtonLabel => _l10n.Get("Library.Albums.RefreshMetadata");
    public string RefreshMetadataToolTip => _l10n.Get("Library.Albums.RefreshMetadataToolTip");
    public string EmptyFilterTitle => _l10n.Get("Library.Albums.EmptyFilterTitle");
    public string EmptyFilterAction => _l10n.Get("Library.Albums.EmptyFilterAction");
    public string EmptyLibraryTitle => _l10n.Get("Library.Albums.EmptyLibraryTitle");
    public string EmptyLibraryDescription => _l10n.Get("Library.Albums.EmptyLibraryDesc");
    public string PlayAllLabel => _l10n.Get("Library.Albums.PlayAll");
    public string ShufflePlayLabel => _l10n.Get("Library.Albums.ShufflePlay");
    public string IntroSectionLabel => _l10n.Get("Library.Albums.Intro");
    public string TracksSectionLabel => _l10n.Get("Library.Albums.Tracks");
    public string ExpandFullLabel => _l10n.Get("Library.Albums.ExpandFull");
    public string CollapseLabel => _l10n.Get("Library.Albums.Collapse");
    public string DownloadSelectedLabel => _l10n.Get("Library.Albums.DownloadSelected");
    public string DownloadMissingLabel => _l10n.Get("Library.Albums.DownloadMissing");
    public string SelectAllDownloadableLabel => _l10n.Get("Library.Albums.SelectAllDownloadable");
    public string DeselectAllLabel => _l10n.Get("Library.Albums.DeselectAll");
    public string LoadMoreLabel => _l10n.Get("Common.LoadMore");
    public string QualityPriorityLabel => _l10n.Get("Library.Albums.QualityPriority");

    public IReadOnlyList<string> AlbumDetailQualityOptionLabels =>
    [
        _l10n.Get("Discover.Quality.Mp3128"),
        _l10n.Get("Discover.Quality.Mp3320"),
        _l10n.Get("Discover.Quality.Flac"),
        _l10n.Get("Library.Albums.QualityPriority")
    ];
    public string AlbumDescriptionSourceDisplay =>
        string.IsNullOrWhiteSpace(AlbumDescriptionSource)
            ? string.Empty
            : string.Format(
                _l10n.Get("Library.Albums.SourceFormat"),
                ServiceMessageLocalizer.Resolve(_l10n, AlbumDescriptionSource));

    public ObservableCollection<object> BrowseItems { get; } = [];
    public static IReadOnlyList<string> AlphabetLetters => ArtistSortHelper.IndexLetters;
    public IReadOnlyList<string> IndexRailLetters => AlphabetLetters;
    public ObservableCollection<ArtistAlbumTrackItemViewModel> AlbumDetailTracks { get; } = [];

    [ObservableProperty] private AlbumListItemViewModel? _selectedAlbum;
    [ObservableProperty] private bool _showingTracks;
    [ObservableProperty] private string _albumSearchText = string.Empty;
    [ObservableProperty] private byte[]? _albumCoverLarge;
    [ObservableProperty] private int _filteredAlbumCount;
    [ObservableProperty] private int _filteredTrackCount;
    [ObservableProperty] private string? _albumDescription;
    [ObservableProperty] private string? _albumDescriptionSource;
    [ObservableProperty] private bool _isAlbumDescriptionExpanded;
    [ObservableProperty] private bool _isLoadingAlbumDetail;
    [ObservableProperty] private bool _isLoadingMoreAlbumDetailTracks;
    [ObservableProperty] private string? _albumDetailStatusMessage;
    [ObservableProperty] private bool _isLoadingBrowse = true;
    [ObservableProperty] private bool _isRefreshingAlbumCatalogs;
    [ObservableProperty] private string? _albumCatalogsRefreshMessage;
    /// <summary>0=128 / 1=320 / 2=FLAC / 3=音质优先</summary>
    [ObservableProperty] private int _albumDetailDownloadQualityIndex = 3;

    public int TotalAlbumCount => _allAlbums.Count;
    public bool HasActiveBrowseFilter => !string.IsNullOrWhiteSpace(AlbumSearchText);
    public bool HasFilteredAlbums => FilteredAlbumCount > 0;
    public bool ShowBrowseFilterEmpty => !IsLoadingBrowse && !ShowingTracks && TotalAlbumCount > 0 && FilteredAlbumCount == 0;
    public bool ShowLibraryEmpty => !IsLoadingBrowse && !ShowingTracks && TotalAlbumCount == 0;
    public bool ShowBrowseSkeleton => IsLoadingBrowse && !ShowingTracks;

    public string BrowseStatsLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AlbumCatalogsRefreshMessage))
                return AlbumCatalogsRefreshMessage;

            return HasActiveBrowseFilter
                ? string.Format(_l10n.Get("Library.Albums.BrowseStatsFiltered"), FilteredAlbumCount, FilteredTrackCount, TotalAlbumCount)
                : string.Format(_l10n.Get("Library.Albums.BrowseStats"), TotalAlbumCount, FilteredTrackCount);
        }
    }

    public string BrowseFilterEmptyHint => _l10n.Get("Library.Albums.FilterEmptyHint");

    public IReadOnlyList<ArtistAlbumTrackItemViewModel> ActiveAlbumDetailTracks =>
        _albumDetailPager.TotalCount > 0 ? _albumDetailPager.All : _allDetailTracks;

    public int AlbumDownloadedCount => ActiveAlbumDetailTracks.Count(t => t.IsInLibrary);
    public int AlbumMissingCount => ActiveAlbumDetailTracks.Count(t => !t.IsInLibrary);
    public bool HasAlbumMissingTracks => AlbumMissingCount > 0;
    public bool HasMoreAlbumDetailTracks => _albumDetailPager.HasMore;
    public bool ShowAlbumDetailStatus => !string.IsNullOrWhiteSpace(AlbumDetailStatusMessage);
    public int SelectedDownloadableCount => ActiveAlbumDetailTracks.Count(t => t.IsSelected && t.CanDownload);
    public bool HasSelectedDownloadable => SelectedDownloadableCount > 0;
    public bool CanDownloadSelectedOnline =>
        HasSelectedDownloadable && OnlineDownloadAvailability.IsAvailable(GoMusicApiClient.IsConfigured);
    public string DownloadSelectedToolTip =>
        OnlineDownloadAvailability.IsAvailable(GoMusicApiClient.IsConfigured)
            ? DownloadSelectedLabel
            : OnlineTrackRowLabels.DownloadDisabledHint;
    public bool UseQualityPriorityDownload => AlbumDetailDownloadQualityIndex == 3;

    public string AlbumDetailTracksSummary => BuildAlbumDetailTracksSummary();

    public string AlbumTracksStatsSuffix => ActiveAlbumDetailTracks.Count > 0
        ? string.Format(_l10n.Get("Library.Albums.StatsDownloaded"), AlbumDownloadedCount, AlbumMissingCount)
        : SelectedAlbum != null
            ? string.Format(_l10n.Get("Library.Albums.StatsInLibrary"), SelectedAlbum.TrackCount)
            : string.Empty;

    public string AlbumDetailSubtitle
    {
        get
        {
            if (SelectedAlbum == null)
                return string.Empty;

            var line = SelectedAlbum.Artist;
            if (SelectedAlbum.Year > 0)
                line += $" · {SelectedAlbum.Year}";
            if (ActiveAlbumDetailTracks.Count == 0)
                line += string.Format(_l10n.Get("Library.Albums.StatsInLibrary"), SelectedAlbum.TrackCount);
            return line;
        }
    }

    partial void OnIsLoadingMoreAlbumDetailTracksChanged(bool value) => NotifyAlbumDetailPagingUi();

    public AudioQuality SelectedDownloadQuality => AlbumDetailDownloadQualityIndex switch
    {
        0 => AudioQuality.Standard128,
        2 => AudioQuality.Flac,
        _ => AudioQuality.High320
    };

    public AudioQuality ResolveAlbumDetailDownloadQuality(OnlineTrack track) =>
        UseQualityPriorityDownload
            ? OnlineTrackDownloadQualityResolver.ResolveQualityPriority(track)
            : SelectedDownloadQuality;

    public bool HasAlbumCover => AlbumCoverLarge is { Length: > 0 };
    public bool HasAlbumDescription => !string.IsNullOrWhiteSpace(AlbumDescription);
    public bool ShowAlbumDescriptionSource => !string.IsNullOrWhiteSpace(AlbumDescriptionSource);
    public bool HasLongAlbumDescription => (AlbumDescription?.Length ?? 0) > 220;
    public bool ShowAlbumDescriptionExpandLink => HasLongAlbumDescription && !IsAlbumDescriptionExpanded;
    public bool ShowAlbumDescriptionCollapseLink => HasLongAlbumDescription && IsAlbumDescriptionExpanded;
    public bool ShowAlbumDescriptionCollapsedPlain =>
        HasAlbumDescription && !IsAlbumDescriptionExpanded && !HasLongAlbumDescription;

    public bool ShowAlbumDescriptionExpandedPlain =>
        IsAlbumDescriptionExpanded && !HasLongAlbumDescription;

    public string AlbumDescriptionPreviewText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AlbumDescription))
                return string.Empty;
            if (!HasLongAlbumDescription || IsAlbumDescriptionExpanded)
                return AlbumDescription;
            return AlbumDescription.Length <= 360
                ? AlbumDescription
                : AlbumDescription[..360].TrimEnd();
        }
    }

    public string AlbumDescriptionPreviewDisplay => FormatFirstLineIndent(
        ServiceMessageLocalizer.Resolve(_l10n, AlbumDescriptionPreviewText));

    public string AlbumDescriptionExpandedDisplay => FormatFirstLineIndent(
        ServiceMessageLocalizer.Resolve(_l10n, AlbumDescription ?? string.Empty));

    private static string FormatFirstLineIndent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        const string indent = "　　";
        var newline = text.IndexOf('\n');
        return newline < 0
            ? indent + text
            : indent + text[..newline] + text[newline..];
    }

    private AudioQuality? PreferredQuality => DownloadQualityPreference.ToFilterQuality(
        DownloadQualityPreference.NormalizeIndex(App.Settings.Current.DefaultDownloadQualityIndex));

    public AlbumsPageViewModel(
        AppServices services,
        PlayerBarViewModel player,
        AppModalHostViewModel appModal,
        OnlineFavoritesService onlineFavorites,
        OnlinePlaylistTrackActions onlinePlaylistActions,
        ILocalizationService l10n)
    {
        _services = services;
        _player = player;
        _appModal = appModal;
        _onlineFavorites = onlineFavorites;
        _onlinePlaylistActions = onlinePlaylistActions;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        _albumDetailDownloadQualityIndex = 3;
        _onlineFavorites.Changed += (_, _) => Dispatcher.UIThread.Post(SyncAlbumDetailOnlineFavoriteStates);
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(PageBackButtonLabel));
        OnPropertyChanged(nameof(PageHeaderText));
        OnPropertyChanged(nameof(BrowsePageTitle));
        OnPropertyChanged(nameof(SearchFieldLabel));
        OnPropertyChanged(nameof(SearchWatermarkText));
        OnPropertyChanged(nameof(SearchButtonLabel));
        OnPropertyChanged(nameof(ResetButtonLabel));
        OnPropertyChanged(nameof(RefreshMetadataButtonLabel));
        OnPropertyChanged(nameof(RefreshMetadataToolTip));
        OnPropertyChanged(nameof(EmptyFilterTitle));
        OnPropertyChanged(nameof(EmptyFilterAction));
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryDescription));
        OnPropertyChanged(nameof(PlayAllLabel));
        OnPropertyChanged(nameof(ShufflePlayLabel));
        OnPropertyChanged(nameof(IntroSectionLabel));
        OnPropertyChanged(nameof(TracksSectionLabel));
        OnPropertyChanged(nameof(ExpandFullLabel));
        OnPropertyChanged(nameof(CollapseLabel));
        OnPropertyChanged(nameof(DownloadSelectedLabel));
        OnPropertyChanged(nameof(DownloadSelectedToolTip));
        OnPropertyChanged(nameof(DownloadMissingLabel));
        OnPropertyChanged(nameof(SelectAllDownloadableLabel));
        OnPropertyChanged(nameof(DeselectAllLabel));
        OnPropertyChanged(nameof(LoadMoreLabel));
        OnPropertyChanged(nameof(QualityPriorityLabel));
        OnPropertyChanged(nameof(AlbumDetailQualityOptionLabels));
        OnPropertyChanged(nameof(AlbumDescriptionSourceDisplay));
        OnPropertyChanged(nameof(BrowseStatsLine));
        OnPropertyChanged(nameof(BrowseFilterEmptyHint));
        OnPropertyChanged(nameof(AlbumTracksStatsSuffix));
        OnPropertyChanged(nameof(AlbumDetailSubtitle));
        OnPropertyChanged(nameof(AlbumDetailTracksSummary));
        RefreshAlbumListItemStats();
    }

    private void RefreshAlbumListItemStats()
    {
        foreach (var item in _albumItems.Values)
            item.RefreshLocalizedStrings(_l10n);
    }

    public Task<IReadOnlyList<PlaylistMenuEntry>> GetAddablePlaylistsAsync(
        CancellationToken ct = default) =>
        _onlinePlaylistActions.GetAddablePlaylistsAsync(ct);

    public async Task AddOnlineTrackToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default)
    {
        var msg = await _onlinePlaylistActions.AddToPlaylistAsync(trackVm, playlistId, ct).ConfigureAwait(true);
        AlbumDetailStatusMessage = $"{trackVm.Title} · {msg}";
    }

    private void SyncAlbumDetailOnlineFavoriteStates()
    {
        foreach (var row in _allDetailTracks)
        {
            if (row.OnlineVm == null)
                continue;
            row.OnlineVm.IsOnlineFavorite = _onlineFavorites.IsFavorite(row.OnlineVm.Model);
        }
    }

    partial void OnAlbumDetailDownloadQualityIndexChanged(int value)
    {
        foreach (var track in _allDetailTracks)
            track.OnlineVm?.NotifyPreferredDownloadChanged();
    }

    partial void OnShowingTracksChanged(bool value)
    {
        ShowingTracksChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(ShowStandardPageHeader));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(ShowPageBackButton));
        OnPropertyChanged(nameof(PageBackButtonLabel));
        OnPropertyChanged(nameof(PageBackCommand));
        OnPropertyChanged(nameof(ShowPageHeaderDivider));
    }

    partial void OnSelectedAlbumChanged(AlbumListItemViewModel? value) =>
        OnPropertyChanged(nameof(AlbumDetailSubtitle));

    partial void OnAlbumCoverLargeChanged(byte[]? value) => OnPropertyChanged(nameof(HasAlbumCover));

    partial void OnAlbumDescriptionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAlbumDescription));
        OnPropertyChanged(nameof(HasLongAlbumDescription));
        OnPropertyChanged(nameof(AlbumDescriptionPreviewText));
        OnPropertyChanged(nameof(AlbumDescriptionPreviewDisplay));
        OnPropertyChanged(nameof(AlbumDescriptionExpandedDisplay));
        OnPropertyChanged(nameof(ShowAlbumDescriptionExpandLink));
        OnPropertyChanged(nameof(ShowAlbumDescriptionCollapseLink));
        OnPropertyChanged(nameof(ShowAlbumDescriptionCollapsedPlain));
        OnPropertyChanged(nameof(ShowAlbumDescriptionExpandedPlain));
    }

    partial void OnAlbumDescriptionSourceChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowAlbumDescriptionSource));
        OnPropertyChanged(nameof(AlbumDescriptionSourceDisplay));
    }

    partial void OnIsAlbumDescriptionExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AlbumDescriptionPreviewText));
        OnPropertyChanged(nameof(AlbumDescriptionPreviewDisplay));
        OnPropertyChanged(nameof(AlbumDescriptionExpandedDisplay));
        OnPropertyChanged(nameof(ShowAlbumDescriptionExpandLink));
        OnPropertyChanged(nameof(ShowAlbumDescriptionCollapseLink));
        OnPropertyChanged(nameof(ShowAlbumDescriptionCollapsedPlain));
        OnPropertyChanged(nameof(ShowAlbumDescriptionExpandedPlain));
    }

    [RelayCommand]
    private void ToggleAlbumDescriptionExpanded() =>
        IsAlbumDescriptionExpanded = !IsAlbumDescriptionExpanded;

    partial void OnAlbumDetailStatusMessageChanged(string? value) => OnPropertyChanged(nameof(ShowAlbumDetailStatus));

    partial void OnAlbumSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveBrowseFilter));
        _albumSearchDebounceCts?.Cancel();
        _albumSearchDebounceCts = new CancellationTokenSource();
        _ = DebouncedAlbumSearchAsync(_albumSearchDebounceCts.Token);
    }

    partial void OnFilteredAlbumCountChanged(int value) => NotifyBrowseStatsChanged();
    partial void OnFilteredTrackCountChanged(int value) => NotifyBrowseStatsChanged();
    partial void OnAlbumCatalogsRefreshMessageChanged(string? value) => NotifyBrowseStatsChanged();
    partial void OnIsRefreshingAlbumCatalogsChanged(bool value) => NotifyBrowseStatsChanged();

    public event EventHandler? ShowingTracksChanged;
    public event EventHandler<string>? LetterScrollRequested;

    public async Task InitializeAsync()
    {
        IsLoadingBrowse = true;
        OnPropertyChanged(nameof(ShowBrowseSkeleton));
        OnPropertyChanged(nameof(ShowLibraryEmpty));
        try
        {
            _allAlbums = (await _services.Tracks.GetAlbumsAsync())
                .Where(a => ArtistAlbumBrowseFilter.IsDisplayableName(a.Album))
                .ToList();
            ApplyAlbumListFilter();
        }
        finally
        {
            IsLoadingBrowse = false;
            OnPropertyChanged(nameof(ShowBrowseSkeleton));
            OnPropertyChanged(nameof(ShowLibraryEmpty));
        }
    }

    partial void OnIsLoadingBrowseChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseSkeleton));
        OnPropertyChanged(nameof(ShowLibraryEmpty));
    }

    public void ApplyLibrarySearch(string query)
    {
        _detailTrackSearch = query;
        if (ShowingTracks)
            ApplyAlbumTracksFilter();
    }

    public void ExitDetailViewIfOpen()
    {
        if (!ShowingTracks)
            return;

        BackToAlbums();
    }

    [RelayCommand]
    private void ApplyAlbumSearch()
    {
        ApplyAlbumListFilter();
    }

    [RelayCommand]
    private void ClearBrowseFilters()
    {
        _albumSearchDebounceCts?.Cancel();
        AlbumSearchText = string.Empty;
        ApplyAlbumListFilter();
    }

    [RelayCommand]
    private async Task RefreshCurrentAlbumDetailAsync()
    {
        if (!ShowingTracks || SelectedAlbum == null || IsRefreshingAlbumCatalogs)
            return;

        if (!GoMusicApiClient.IsConfigured)
        {
            AlbumDetailStatusMessage = _l10n.Get("Library.Albums.ApiNotConfigured");
            return;
        }

        var cached = await _services.AlbumCatalogs
            .GetCachedAsync(SelectedAlbum.Artist, SelectedAlbum.Album)
            .ConfigureAwait(false);
        var hasCachedData = cached != null
            && (!string.IsNullOrWhiteSpace(cached.Description)
                || AlbumCatalogRepository.DeserializeTracks(cached.TracksJson).Count > 0);

        if (hasCachedData)
        {
            var choice = await _appModal
                .ShowAlbumCatalogRefreshConfirmAsync(SelectedAlbum.Album, willOverwriteCache: true, _l10n)
                .ConfigureAwait(true);
            if (choice != AlbumCatalogRefreshChoice.Confirm)
                return;
        }

        await RefreshAlbumItemsAsync([SelectedAlbum], reloadDetail: true).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RefreshAlbumCatalogsAsync()
    {
        if (IsRefreshingAlbumCatalogs || ShowingTracks)
            return;

        var targets = GetFilteredAlbumItems().ToList();
        if (targets.Count == 0)
            return;

        await RefreshAlbumItemsAsync(targets, reloadDetail: false).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectAllDownloadableTracks()
    {
        foreach (var track in ActiveAlbumDetailTracks.Where(t => t.CanDownload))
            track.IsSelected = true;
        NotifyAlbumTrackSelectionChanged();
    }

    [RelayCommand]
    private void ClearDownloadableTrackSelection()
    {
        foreach (var track in ActiveAlbumDetailTracks)
            track.IsSelected = false;
        NotifyAlbumTrackSelectionChanged();
    }

    private bool CanDownloadSelectedAlbumTracks() => CanDownloadSelectedOnline;

    [RelayCommand(CanExecute = nameof(CanDownloadSelectedAlbumTracks))]
    private async Task DownloadSelectedAlbumTracksAsync()
    {
        if (!CanDownloadSelectedOnline)
            return;

        var tracks = ActiveAlbumDetailTracks
            .Where(t => t.IsSelected && t.OnlineVm != null)
            .Select(t => t.OnlineVm!.Model)
            .ToList();
        if (tracks.Count == 0)
            return;

        var preview = _services.DiscoverApplication.BuildBatchEnqueuePreview(
            tracks,
            SelectedDownloadQuality,
            useQualityPriority: UseQualityPriorityDownload);

        var choice = await _appModal
            .ShowBatchEnqueueAsync(preview, _l10n, SelectedAlbum?.Album)
            .ConfigureAwait(true);
        if (choice != BatchEnqueueDialogChoice.Confirm)
            return;

        var enqueued = 0;
        var skipped = 0;
        if (UseQualityPriorityDownload)
        {
            foreach (var track in tracks)
            {
                if (!OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track))
                {
                    skipped++;
                    continue;
                }

                var quality = OnlineTrackDownloadQualityResolver.ResolveQualityPriority(track);
                var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(track, quality));
                if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                    enqueued++;
                else
                    skipped++;
            }
        }
        else
        {
            foreach (var track in tracks)
            {
                var enqueue = _services.DownloadApplication.Enqueue(
                    new DownloadEnqueueRequest(track, SelectedDownloadQuality));
                if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                    enqueued++;
                else
                    skipped++;
            }
        }

        AlbumDetailStatusMessage = FormatDownloadEnqueueStatus(enqueued, skipped);
    }

    private string FormatDownloadEnqueueStatus(int enqueued, int skipped)
    {
        if (enqueued > 0)
        {
            var msg = string.Format(_l10n.Get("Library.Albums.DownloadEnqueued"), enqueued);
            if (skipped > 0)
                msg += string.Format(_l10n.Get("Library.Albums.DownloadSkippedSuffix"), skipped);
            return msg;
        }

        return string.Format(_l10n.Get("Library.Albums.DownloadNoneSkipped"), skipped);
    }

    private async Task RefreshAlbumItemsAsync(
        IReadOnlyList<AlbumListItemViewModel> targets,
        bool reloadDetail)
    {
        if (IsRefreshingAlbumCatalogs || targets.Count == 0)
            return;

        _catalogRefreshCts?.Cancel();
        _coverLoadCts?.Cancel();
        _catalogRefreshCts = new CancellationTokenSource();
        var ct = _catalogRefreshCts.Token;

        IsRefreshingAlbumCatalogs = true;
        if (reloadDetail && SelectedAlbum != null)
            AlbumDetailStatusMessage = string.Format(_l10n.Get("Library.Albums.RefreshingOne"), SelectedAlbum.Album);
        else
            AlbumCatalogsRefreshMessage = string.Format(_l10n.Get("Library.Albums.RefreshingMany"), targets.Count);

        var updated = 0;
        var failed = 0;

        try
        {
            if (!GoMusicApiClient.IsConfigured)
            {
                var apiMsg = _l10n.Get("Library.Albums.ApiNotConfigured");
                if (reloadDetail)
                    AlbumDetailStatusMessage = apiMsg;
                else
                    AlbumCatalogsRefreshMessage = apiMsg;
                return;
            }

            using var gate = new SemaphoreSlim(2);
            foreach (var item in targets)
            {
                if (ct.IsCancellationRequested)
                    return;

                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var ok = await RefreshSingleAlbumItemAsync(item, reloadDetail, ct).ConfigureAwait(false);
                    if (ok)
                        updated++;
                    else
                        failed++;
                }
                catch
                {
                    failed++;
                }
                finally
                {
                    gate.Release();
                }
            }

            if (reloadDetail && SelectedAlbum != null)
            {
                AlbumDetailStatusMessage = updated > 0
                    ? string.Format(_l10n.Get("Library.Albums.RefreshSuccessOne"), SelectedAlbum.Album)
                    : string.Format(_l10n.Get("Library.Albums.RefreshFailedOne"), SelectedAlbum.Album);
            }
            else
            {
                AlbumCatalogsRefreshMessage = targets.Count == 1
                    ? (updated > 0
                        ? string.Format(_l10n.Get("Library.Albums.RefreshSuccessOne"), targets[0].Album)
                        : string.Format(_l10n.Get("Library.Albums.RefreshFailedOne"), targets[0].Album))
                    : failed > 0
                        ? string.Format(_l10n.Get("Library.Albums.RefreshCompleteMixed"), updated, failed)
                        : string.Format(_l10n.Get("Library.Albums.RefreshCompleteAll"), updated);
            }
        }
        finally
        {
            IsRefreshingAlbumCatalogs = false;
        }
    }

    private async Task<bool> RefreshSingleAlbumItemAsync(
        AlbumListItemViewModel item,
        bool reloadDetail,
        CancellationToken ct)
    {
        CoverArtCache.InvalidateAlbum(item.Artist, item.Album);

        var catalog = await _services.AlbumCatalogs
            .RefreshAsync(item.Artist, item.Album, ct)
            .ConfigureAwait(false);

        byte[]? thumb = null;
        if (!string.IsNullOrWhiteSpace(catalog?.CoverUrl))
        {
            thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(
                catalog.CoverUrl, CoverFetchSource.NetEase, ct).ConfigureAwait(false);
        }

        if (thumb == null)
        {
            thumb = await AlbumCoverLoader.LoadThumbForAlbumAsync(
                _services, item.Artist, item.Album, ct).ConfigureAwait(false);
        }

        if (thumb is { Length: > 0 })
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                item.CoverThumb = thumb;
                if (SelectedAlbum?.Album == item.Album && SelectedAlbum?.Artist == item.Artist)
                    AlbumCoverLarge = thumb;
            });
        }

        if (reloadDetail
            && ShowingTracks
            && SelectedAlbum?.Album == item.Album
            && SelectedAlbum?.Artist == item.Artist
            && catalog != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrWhiteSpace(catalog.Description))
                {
                    AlbumDescription = catalog.Description;
                    AlbumDescriptionSource = catalog.DescriptionSource;
                    IsAlbumDescriptionExpanded = false;
                }
            });
            _albumDetailCts?.Cancel();
            await LoadAlbumDetailAsync(item).ConfigureAwait(false);
        }

        return catalog != null && catalog.Tracks.Count > 0;
    }

    [RelayCommand]
    private void JumpToLetter(string? letter)
    {
        if (string.IsNullOrEmpty(letter) || ShowingTracks)
            return;

        LetterScrollRequested?.Invoke(this, letter);
    }

    [RelayCommand]
    private async Task SelectAlbumAsync(AlbumListItemViewModel? album)
    {
        if (album == null)
            return;

        SelectedAlbum = album;
        ShowingTracks = true;
        AlbumCoverLarge = album.CoverThumb;
        AlbumDescription = null;
        AlbumDescriptionSource = null;
        IsAlbumDescriptionExpanded = false;
        AlbumDetailStatusMessage = null;
        _detailTrackSearch = string.Empty;
        _currentAlbumTracks = (await _services.Tracks.GetByAlbumAsync(album.Album, album.Artist)).ToList();
        OnPropertyChanged(nameof(AlbumDetailSubtitle));
        _ = LoadAlbumDetailAsync(album);
    }

    [RelayCommand]
    private void BackToAlbums()
    {
        _albumDetailCts?.Cancel();
        ShowingTracks = false;
        SelectedAlbum = null;
        AlbumCoverLarge = null;
        AlbumDescription = null;
        AlbumDescriptionSource = null;
        IsAlbumDescriptionExpanded = false;
        AlbumDetailTracks.Clear();
        _allDetailTracks.Clear();
        _albumDetailPager.Clear();
        _detailTrackSearch = string.Empty;
        AlbumDetailStatusMessage = null;
        ApplyAlbumListFilter();
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        var tracks = ActiveAlbumDetailTracks
            .Where(t => t.IsInLibrary && t.LibraryTrack != null)
            .Select(t => t.LibraryTrack!)
            .ToList();
        if (tracks.Count == 0)
            return;

        var label = SelectedAlbum?.Album ?? _l10n.Get("Library.Albums.DefaultAlbumLabel");
        await _player.PlayTrackAsync(tracks[0], tracks, queueSourceLabel: label);
    }

    [RelayCommand]
    private async Task ShufflePlayAsync()
    {
        var tracks = ActiveAlbumDetailTracks
            .Where(t => t.IsInLibrary && t.LibraryTrack != null)
            .Select(t => t.LibraryTrack!)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        if (tracks.Count == 0)
            return;

        _player.IsShuffleOn = true;
        var label = SelectedAlbum?.Album ?? _l10n.Get("Library.Albums.DefaultAlbumLabel");
        await _player.PlayTrackAsync(tracks[0], tracks, queueSourceLabel: label);
    }

    private bool CanDownloadMissingAlbumTracks() =>
        HasAlbumMissingTracks && OnlineDownloadAvailability.IsAvailable(GoMusicApiClient.IsConfigured);

    [RelayCommand(CanExecute = nameof(CanDownloadMissingAlbumTracks))]
    private Task DownloadMissingAlbumTracksAsync()
    {
        if (!CanDownloadMissingAlbumTracks())
            return Task.CompletedTask;

        var missing = ActiveAlbumDetailTracks
            .Where(t => !t.IsInLibrary && t.OnlineVm != null)
            .Select(t => t.OnlineVm!.Model)
            .ToList();
        if (missing.Count == 0)
            return Task.CompletedTask;

        var enqueued = 0;
        var skipped = 0;
        foreach (var track in missing)
        {
            var quality = PreferredQuality ?? AudioQuality.High320;
            var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(track, quality));
            if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                enqueued++;
            else
                skipped++;
        }

        AlbumDetailStatusMessage = FormatDownloadEnqueueStatus(enqueued, skipped);
        return Task.CompletedTask;
    }

    private async Task DebouncedAlbumSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct).ConfigureAwait(true);
            ApplyAlbumListFilter();
        }
        catch (OperationCanceledException)
        {
            /* 继续输入 */
        }
    }

    private void ApplyAlbumListFilter()
    {
        BrowseItems.Clear();
        _albumItems.Clear();
        _coverLoadCts?.Cancel();
        _coverLoadCts = new CancellationTokenSource();

        var q = AlbumSearchText.Trim();
        IEnumerable<AlbumGroup> src = _allAlbums;
        if (!string.IsNullOrEmpty(q))
        {
            src = src.Where(a =>
                ArtistPinyinHelper.MatchesSearch(a.Album, q)
                || ArtistPinyinHelper.MatchesSearch(a.Artist, q)
                || a.Album.Contains(q, StringComparison.OrdinalIgnoreCase)
                || a.Artist.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = src.ToList();
        FilteredAlbumCount = filtered.Count;
        FilteredTrackCount = filtered.Sum(a => a.TrackCount);
        NotifyBrowseStatsChanged();

        var grouped = filtered
            .GroupBy(a => ArtistSortHelper.GetSectionKey(a.Album))
            .OrderBy(g => g.Key, Comparer<string>.Create(ArtistSortHelper.CompareSectionKeys));

        foreach (var group in grouped)
        {
            BrowseItems.Add(new ArtistSectionHeaderViewModel
            {
                Letter = group.Key,
                ArtistCount = group.Count()
            });

            foreach (var a in group.OrderBy(x => x.Album, Comparer<string>.Create(ArtistSortHelper.CompareArtistNames)))
            {
                var item = new AlbumListItemViewModel(a);
                item.RefreshLocalizedStrings(_l10n);
                _albumItems[BuildAlbumKey(a.Album, a.Artist)] = item;
                BrowseItems.Add(item);
            }
        }

        _ = LoadCoversAsync(_albumItems.Values.ToList(), _coverLoadCts.Token);
    }

    private void ApplyAlbumTracksFilter()
    {
        var q = _detailTrackSearch.Trim();
        var src = string.IsNullOrEmpty(q)
            ? _allDetailTracks
            : _allDetailTracks.Where(t =>
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _albumDetailPager.ResetBuffer(src);
        _albumDetailPager.ResetDisplayed(AlbumDetailTracks);
        NotifyAlbumTrackStatsChanged();
    }

    private void NotifyAlbumTrackStatsChanged()
    {
        OnPropertyChanged(nameof(ActiveAlbumDetailTracks));
        OnPropertyChanged(nameof(AlbumDetailTracksSummary));
        OnPropertyChanged(nameof(AlbumDetailSubtitle));
        OnPropertyChanged(nameof(AlbumTracksStatsSuffix));
        OnPropertyChanged(nameof(AlbumDownloadedCount));
        OnPropertyChanged(nameof(AlbumMissingCount));
        OnPropertyChanged(nameof(HasAlbumMissingTracks));
        NotifyAlbumDetailPagingUi();
        NotifyAlbumTrackSelectionChanged();
        DownloadMissingAlbumTracksCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAlbumDetailPagingUi()
    {
        OnPropertyChanged(nameof(HasMoreAlbumDetailTracks));
        OnPropertyChanged(nameof(AlbumDetailTracksSummary));
    }

    private string BuildAlbumDetailTracksSummary()
    {
        var total = _albumDetailPager.TotalCount;
        if (total <= 0)
            return ActiveAlbumDetailTracks.Count > 0
                ? string.Format(_l10n.Get("Library.Albums.TracksSummaryTotal"), ActiveAlbumDetailTracks.Count, AlbumTracksStatsSuffix)
                : string.Empty;

        var shown = AlbumDetailTracks.Count;
        var parts = new List<string>
        {
            string.Format(_l10n.Get("Library.Albums.TracksSummaryTotal"), total, AlbumTracksStatsSuffix)
        };
        if (shown < total)
            parts.Add(string.Format(_l10n.Get("Library.Albums.TracksSummaryShown"), shown));
        if (_albumDetailPager.HasMore)
            parts.Add(_l10n.Get("Organize.Paging.LoadMore"));
        return string.Join(" · ", parts);
    }

    [RelayCommand]
    private async Task LoadMoreAlbumDetailTracksAsync()
    {
        if (!_albumDetailPager.HasMore || IsLoadingMoreAlbumDetailTracks)
            return;

        IsLoadingMoreAlbumDetailTracks = true;
        try
        {
            await Task.Yield();
            _albumDetailPager.AppendNext(AlbumDetailTracks);
            NotifyAlbumDetailPagingUi();
        }
        finally
        {
            IsLoadingMoreAlbumDetailTracks = false;
        }
    }

    private void NotifyAlbumTrackSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedDownloadableCount));
        OnPropertyChanged(nameof(HasSelectedDownloadable));
        OnPropertyChanged(nameof(CanDownloadSelectedOnline));
        DownloadSelectedAlbumTracksCommand.NotifyCanExecuteChanged();
    }

    public void NotifyOnlineDownloadAvailability()
    {
        OnPropertyChanged(nameof(CanDownloadSelectedOnline));
        OnPropertyChanged(nameof(DownloadSelectedToolTip));
        DownloadSelectedAlbumTracksCommand.NotifyCanExecuteChanged();
        DownloadMissingAlbumTracksCommand.NotifyCanExecuteChanged();
        foreach (var track in ActiveAlbumDetailTracks)
            track.NotifyOnlineDownloadAvailability();
    }

    private void AttachDetailTrackHandlers(ArtistAlbumTrackItemViewModel item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ArtistAlbumTrackItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedDownloadableCount));
                OnPropertyChanged(nameof(HasSelectedDownloadable));
                OnPropertyChanged(nameof(CanDownloadSelectedOnline));
                DownloadSelectedAlbumTracksCommand.NotifyCanExecuteChanged();
            }
        };
    }

    private async Task LoadAlbumDetailAsync(AlbumListItemViewModel album)
    {
        _albumDetailCts?.Cancel();
        _albumDetailCts = new CancellationTokenSource();
        var ct = _albumDetailCts.Token;

        IsLoadingAlbumDetail = true;
        AlbumDetailTracks.Clear();
        _allDetailTracks.Clear();
        _albumDetailPager.Clear();
        AlbumDetailStatusMessage = null;

        try
        {
            var libraryForAlbum = _currentAlbumTracks
                .Where(t => LibraryTrackMatcher.AlbumMatches(t.Album, album.Album))
                .ToList();

            IReadOnlyList<OnlineTrack> onlineTracks = [];
            string? loadHint = null;
            AlbumOnlineCatalog? catalog = null;

            if (GoMusicApiClient.IsConfigured)
            {
                catalog = await _services.AlbumCatalogs.GetOrFetchAsync(album.Artist, album.Album, ct)
                    .ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                    return;

                if (catalog != null)
                {
                    onlineTracks = catalog.Tracks;

                    var description = catalog.Description;
                    var descriptionSource = catalog.DescriptionSource;
                    if (AlbumCatalogService.NeedsRichDescription(description, descriptionSource))
                    {
                        var enriched = await _services.AlbumCatalogs.TryEnrichDescriptionAsync(
                                album.Artist,
                                album.Album,
                                catalog.PlatformAlbumId,
                                ct)
                            .ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(enriched.Description))
                        {
                            description = enriched.Description;
                            descriptionSource = enriched.Source;
                        }
                    }

                    if (catalog.CoverUrl is { Length: > 0 } coverUrl)
                        _ = LoadOnlineCoverAsync(album, coverUrl, ct);

                    if (onlineTracks.Count == 0 && !string.IsNullOrWhiteSpace(catalog.PlatformAlbumId))
                        loadHint = _l10n.Get("Library.Albums.OnlineMatchedNoTracks");

                    var descriptionForUi = description;
                    var sourceForUi = descriptionSource;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AlbumDescription = descriptionForUi;
                        AlbumDescriptionSource = sourceForUi;
                        IsAlbumDescriptionExpanded = false;
                    });
                }
                else
                    loadHint = _l10n.Get("Library.Albums.OnlineNotMatched");
            }
            else
                loadHint = _l10n.Get("Library.Albums.ApiNotConfiguredLibraryOnly");

            if (ct.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var items = AlbumDetailTrackCatalogBuilder.Build(
                    album.Artist,
                    album.Album,
                    libraryForAlbum,
                    onlineTracks,
                    _services,
                    _player,
                    ResolveAlbumDetailDownloadQuality,
                    message => AlbumDetailStatusMessage = message,
                    appModal: _appModal,
                    onlineFavorites: _onlineFavorites);

                _allDetailTracks = items.ToList();
                foreach (var row in _allDetailTracks)
                    AttachDetailTrackHandlers(row);
                ApplyAlbumTracksFilter();

                if (_allDetailTracks.Count == 0)
                    AlbumDetailStatusMessage = _l10n.Get("Library.Albums.NoTracksFound");
                else if (onlineTracks.Count == 0)
                    AlbumDetailStatusMessage = loadHint ?? _l10n.Get("Library.Albums.LibraryTracksOnly");
                else
                    AlbumDetailStatusMessage = null;
            });
        }
        catch (OperationCanceledException)
        {
            /* 切换专辑 */
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var items = AlbumDetailTrackCatalogBuilder.Build(
                    album.Artist,
                    album.Album,
                    _currentAlbumTracks,
                    [],
                    _services,
                    _player,
                    ResolveAlbumDetailDownloadQuality,
                    appModal: _appModal,
                    onlineFavorites: _onlineFavorites);
                _allDetailTracks = items.ToList();
                foreach (var row in _allDetailTracks)
                    AttachDetailTrackHandlers(row);
                ApplyAlbumTracksFilter();
                AlbumDetailStatusMessage = ServiceMessageLocalizer.Format(
                    _l10n,
                    "Library.Albums.OnlineLoadError",
                    ex.Message);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoadingAlbumDetail = false);
        }
    }

    private async Task LoadOnlineCoverAsync(AlbumListItemViewModel album, string coverUrl, CancellationToken ct)
    {
        try
        {
            var thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(coverUrl, CoverFetchSource.NetEase, ct)
                .ConfigureAwait(false);
            if (thumb is not { Length: > 0 } || ct.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedAlbum?.Album == album.Album && SelectedAlbum?.Artist == album.Artist)
                {
                    AlbumCoverLarge = thumb;
                    album.CoverThumb = thumb;
                }
            });
        }
        catch
        {
            /* 忽略 */
        }
    }

    private async Task LoadCoversAsync(IReadOnlyList<AlbumListItemViewModel> items, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(4);
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested)
                return;

            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var thumb = await AlbumCoverLoader.LoadThumbForAlbumAsync(
                    _services, item.Artist, item.Album, ct).ConfigureAwait(false);
                if (thumb is not { Length: > 0 } || ct.IsCancellationRequested)
                    continue;

                await Dispatcher.UIThread.InvokeAsync(() => item.CoverThumb = thumb);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                /* 单张封面失败不影响列表 */
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private async Task LoadAlbumCoverLargeAsync(AlbumListItemViewModel album)
    {
        if (album.HasCover)
        {
            AlbumCoverLarge = album.CoverThumb;
            return;
        }

        try
        {
            var thumb = await AlbumCoverLoader.LoadThumbForAlbumAsync(
                _services, album.Artist, album.Album).ConfigureAwait(false);
            if (thumb is { Length: > 0 })
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (SelectedAlbum?.Album == album.Album && SelectedAlbum?.Artist == album.Artist)
                    {
                        AlbumCoverLarge = thumb;
                        album.CoverThumb = thumb;
                    }
                });
            }
        }
        catch
        {
            /* 忽略 */
        }
    }

    private void NotifyBrowseStatsChanged()
    {
        OnPropertyChanged(nameof(TotalAlbumCount));
        OnPropertyChanged(nameof(HasFilteredAlbums));
        OnPropertyChanged(nameof(ShowBrowseFilterEmpty));
        OnPropertyChanged(nameof(ShowLibraryEmpty));
        OnPropertyChanged(nameof(BrowseStatsLine));
        OnPropertyChanged(nameof(BrowseFilterEmptyHint));
    }

    private IEnumerable<AlbumListItemViewModel> GetFilteredAlbumItems() =>
        _albumItems.Values;

    private static string BuildAlbumKey(string album, string artist) =>
        $"{album}\0{artist}";
}
