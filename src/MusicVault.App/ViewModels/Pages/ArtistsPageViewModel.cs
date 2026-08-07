using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
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
using MusicVault.Core.Artists;
using MusicVault.Core.Browse;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Features;
using MusicVault.Services.Metadata;
using MusicVault.Services.Playlist;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>艺术家浏览页：分区索引、区域筛选、百科简介/头像、详情曲目表。</summary>
public partial class ArtistsPageViewModel : PageViewModelBase, IAlbumCatalogOnlineTrackHost
{
    private readonly AppServices _services;
    private readonly PlayerBarViewModel _player;
    private readonly AppModalHostViewModel _appModal;
    private readonly OnlineFavoritesService _onlineFavorites;
    private readonly OnlinePlaylistTrackActions _onlinePlaylistActions;
    private readonly ILocalizationService _l10n;
    private readonly HashSet<long> _favoriteTrackIds = [];
    private readonly Dictionary<string, ArtistListItemViewModel> _artistItems = new(StringComparer.Ordinal);
    private List<ArtistGroup> _allArtists = [];
    private List<Track> _currentArtistTracks = [];
    private readonly ResultsListPager<ArtistAlbumTrackItemViewModel> _albumDetailPager = new();
    private readonly ResultsListPager<TrackItemViewModel> _artistTracksPager = new();
    private Dictionary<string, ArtistProfile> _profiles = new(StringComparer.Ordinal);
    private CancellationTokenSource? _profileFetchCts;
    private CancellationTokenSource? _prefetchCts;
    private CancellationTokenSource? _artistSearchDebounceCts;

    public override bool ShowStandardPageHeader => false;

    public override string PageTitle => string.Empty;

    public override bool ShowPageSubtitle => false;

    public override bool ShowPageBackButton => false;

    public override string PageBackButtonLabel => string.Empty;

    public override ICommand? PageBackCommand => null;

    public override bool ShowPageHeaderDivider => false;

    /// <summary>列表页功能说明（与发现页 page-intro 同区展示）。</summary>
    public string PageHeaderText => _l10n.Get("Library.Artists.PageHeader");

    public string BrowsePageTitle => _l10n.Get("Library.Artists.Title");
    public string FilterFieldLabel => _l10n.Get("Library.Artists.FilterLabel");
    public string SearchWatermarkText => _l10n.Get("Library.Artists.SearchWatermark");
    public string SearchButtonLabel => _l10n.Get("Common.Search");
    public string ResetButtonLabel => _l10n.Get("Common.Reset");
    public string DataGovernanceButtonLabel => _l10n.Get("Library.Artists.DataGovernance");
    public string DataGovernanceToolTip => _l10n.Get("Library.Artists.DataGovernanceToolTip");
    public string DataGovernancePopconfirmTitle => _l10n.Get("Library.Artists.DataGovernanceTitle");
    public string DataGovernancePopconfirmDescription => _l10n.Get("Library.Artists.DataGovernanceDesc");
    public string DataGovernanceConfirmText => _l10n.Get("Library.Artists.DataGovernanceConfirm");
    public string EmptyFilterTitle => _l10n.Get("Library.Artists.EmptyFilterTitle");
    public string EmptyFilterAction => _l10n.Get("Library.Artists.EmptyFilterAction");
    public string EmptyLibraryTitle => _l10n.Get("Library.Artists.EmptyLibraryTitle");
    public string EmptyLibraryDescription => _l10n.Get("Library.Artists.EmptyLibraryDesc");
    public string PlayAllLabel => _l10n.Get("Library.Artists.PlayAll");
    public string ShufflePlayLabel => _l10n.Get("Library.Artists.ShufflePlay");
    public string RefreshProfileLabel => _l10n.Get("Library.Artists.RefreshProfile");
    public string IntroSectionLabel => _l10n.Get("Library.Artists.Intro");
    public string AlbumsSectionLabel => _l10n.Get("Library.Artists.AlbumsSection");
    public string TracksSectionLabel => _l10n.Get("Library.Artists.TracksSection");
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
    public string AlbumIntroSectionLabel => _l10n.Get("Library.Albums.Intro");
    public string AlbumTracksSectionLabel => _l10n.Get("Library.Albums.Tracks");
    public string RemoveTrackDeleteWithFileText => _l10n.Get("Library.RemoveTrack.DeleteWithFile");
    public string DeletePopconfirmText => _l10n.Get("Common.Delete");

    public override string PageSubtitle => string.Empty;

    public ObservableCollection<object> BrowseItems { get; } = [];
    public static IReadOnlyList<string> AlphabetLetters => ArtistSortHelper.IndexLetters;
    public IReadOnlyList<string> IndexRailLetters => AlphabetLetters;
    public ObservableCollection<TrackItemViewModel> ArtistTracks { get; } = [];
    public ObservableCollection<ArtistAlbumItemViewModel> ArtistAlbums { get; } = [];
    public ObservableCollection<ArtistAlbumTrackItemViewModel> SelectedAlbumTracks { get; } = [];
    public ObservableCollection<Playlist> PlaylistsForMenu { get; } = [];

    public IReadOnlyList<ArtistRegionFilter> RegionFilterOptions => ArtistRegionFilterLabels.AllOptions;

    [ObservableProperty] private ArtistListItemViewModel? _selectedArtist;
    [ObservableProperty] private bool _showingTracks;
    [ObservableProperty] private bool _isLoadingProfile;
    [ObservableProperty] private bool _isRefreshingArtistAlbums;
    [ObservableProperty] private string? _artistSummary;
    [ObservableProperty] private string? _artistDescription;
    [ObservableProperty] private byte[]? _artistPhotoLarge;
    [ObservableProperty] private string? _artistSourceUrl;
    [ObservableProperty] private string? _artistSourceName;
    [ObservableProperty] private string? _profileStatusMessage;
    [ObservableProperty] private bool _isDescriptionExpanded;
    [ObservableProperty] private string _artistSearchText = string.Empty;
    [ObservableProperty] private ArtistRegionFilter _selectedRegionFilter = ArtistRegionFilter.All;
    [ObservableProperty] private bool _showingAlbumTracks;
    [ObservableProperty] private ArtistAlbumItemViewModel? _selectedAlbum;
    [ObservableProperty] private bool _isLoadingAlbumTracks;
    [ObservableProperty] private bool _isLoadingMoreAlbumTracks;
    [ObservableProperty] private bool _isLoadingMoreArtistTracks;
    [ObservableProperty] private string? _albumTracksStatusMessage;
    [ObservableProperty] private string? _albumDescription;
    [ObservableProperty] private string? _albumDescriptionSource;
    [ObservableProperty] private bool _isAlbumDescriptionExpanded;
    [ObservableProperty] private int _albumDetailDownloadQualityIndex = 3;
    [ObservableProperty] private int _filteredArtistCount;
    [ObservableProperty] private int _filteredTrackCount;
    [ObservableProperty] private int _filteredAlbumCount;
    [ObservableProperty] private bool _isLoadingBrowse = true;
    [ObservableProperty] private bool _isRefreshingArtistProfiles;
    [ObservableProperty] private string? _artistProfilesRefreshMessage;
    [ObservableProperty] private bool _isRunningDataGovernance;

    public int TotalArtistCount => _allArtists.Count;
    public bool HasActiveBrowseFilter =>
        !string.IsNullOrWhiteSpace(ArtistSearchText) || SelectedRegionFilter != ArtistRegionFilter.All;
    public bool HasFilteredArtists => FilteredArtistCount > 0;
    public bool ShowBrowseFilterEmpty => !IsLoadingBrowse && !ShowingTracks && TotalArtistCount > 0 && FilteredArtistCount == 0;
    public bool ShowLibraryEmpty => !IsLoadingBrowse && !ShowingTracks && TotalArtistCount == 0;
    public bool ShowBrowseSkeleton => IsLoadingBrowse && !ShowingTracks;
    public bool ShowArtistProfilesRefreshSkeleton =>
        IsRefreshingArtistProfiles && !IsRunningDataGovernance && !ShowingTracks && HasFilteredArtists && !IsLoadingBrowse;

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

    public bool ShowArtistAlbumsSkeleton => IsRefreshingArtistAlbums && !HasArtistAlbums;
    public string BrowseStatsLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ArtistProfilesRefreshMessage))
                return ArtistProfilesRefreshMessage;

            var stats = HasActiveBrowseFilter
                ? string.Format(
                    _l10n.Get("Library.Artists.BrowseStatsFiltered"),
                    FilteredArtistCount,
                    FilteredTrackCount,
                    FilteredAlbumCount,
                    TotalArtistCount)
                : string.Format(
                    _l10n.Get("Library.Artists.BrowseStats"),
                    TotalArtistCount,
                    FilteredTrackCount,
                    FilteredAlbumCount);

            return SelectedRegionFilter != ArtistRegionFilter.All
                ? $"{ArtistRegionFilterLabels.GetLabel(SelectedRegionFilter, _l10n)} · {stats}"
                : stats;
        }
    }
    public string BrowseFilterEmptyHint => SelectedRegionFilter != ArtistRegionFilter.All
        ? string.Format(
            _l10n.Get("Library.Artists.FilterEmptyHintRegion"),
            ArtistRegionFilterLabels.GetLabel(SelectedRegionFilter, _l10n))
        : _l10n.Get("Library.Artists.FilterEmptyHint");
    public string BrowseSummaryText => BrowseStatsLine;
    public string RegionFilterSummary => SelectedRegionFilter == ArtistRegionFilter.All
        ? _l10n.Get("Library.Artists.Region.AllAreas")
        : string.Format(
            _l10n.Get("Library.Artists.RegionSummary"),
            ArtistRegionFilterLabels.GetLabel(SelectedRegionFilter, _l10n),
            FilteredArtistCount);
    public string RegionAllLabel => FormatRegionFilterLabel(ArtistRegionFilter.All);
    public string RegionMandarinLabel => FormatRegionFilterLabel(ArtistRegionFilter.Mandarin);
    public string RegionJapaneseLabel => FormatRegionFilterLabel(ArtistRegionFilter.Japanese);
    public string RegionKoreanLabel => FormatRegionFilterLabel(ArtistRegionFilter.Korean);
    public string RegionWesternLabel => FormatRegionFilterLabel(ArtistRegionFilter.Western);
    public string RegionOtherLabel => FormatRegionFilterLabel(ArtistRegionFilter.Other);

    public IReadOnlyList<string> RegionSegmentLabels { get; private set; } = [];

    public int RegionFilterIndex
    {
        get
        {
            var options = ArtistRegionFilterLabels.AllOptions;
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] == SelectedRegionFilter)
                    return i;
            }

            return 0;
        }
        set
        {
            var options = ArtistRegionFilterLabels.AllOptions;
            if (value < 0 || value >= options.Count)
                return;

            var filter = options[value];
            if (SelectedRegionFilter != filter)
                SelectedRegionFilter = filter;
        }
    }
    public IReadOnlyList<ArtistAlbumTrackItemViewModel> ActiveAlbumDetailTracks =>
        _albumDetailPager.TotalCount > 0 ? _albumDetailPager.All : SelectedAlbumTracks;

    public IReadOnlyList<Track> ActiveArtistTracksForPlayback =>
        _artistTracksPager.TotalCount > 0
            ? _artistTracksPager.All.Select(t => t.Track).ToList()
            : ArtistTracks.Select(t => t.Track).ToList();

    public bool HasMoreArtistTracks => _artistTracksPager.HasMore;

    public string ArtistTracksSummary => BuildArtistTracksSummary();

    public int AlbumOwnedCount => ActiveAlbumDetailTracks.Count(t => t.IsInLibrary);
    public int AlbumMissingCount => ActiveAlbumDetailTracks.Count(t => !t.IsInLibrary);
    public bool HasAlbumMissingTracks => AlbumMissingCount > 0;
    public bool HasMoreAlbumDetailTracks => _albumDetailPager.HasMore;
    public bool HasAlbumDescription => !string.IsNullOrWhiteSpace(AlbumDescription);
    public bool ShowAlbumDescriptionSource => !string.IsNullOrWhiteSpace(AlbumDescriptionSource);
    public bool HasLongAlbumDescription => (AlbumDescription?.Length ?? 0) > 220;
    public bool ShowAlbumDescriptionExpandLink => HasLongAlbumDescription && !IsAlbumDescriptionExpanded;
    public bool ShowAlbumDescriptionCollapseLink => HasLongAlbumDescription && IsAlbumDescriptionExpanded;
    public bool ShowAlbumDescriptionCollapsedPlain =>
        HasAlbumDescription && !IsAlbumDescriptionExpanded && !HasLongAlbumDescription;
    public bool ShowAlbumDescriptionExpandedPlain =>
        IsAlbumDescriptionExpanded && !HasLongAlbumDescription;
    public bool ShowAlbumTracksStatus => !string.IsNullOrWhiteSpace(AlbumTracksStatusMessage);
    public bool UseQualityPriorityDownload => AlbumDetailDownloadQualityIndex == 3;
    public int SelectedDownloadableCount => ActiveAlbumDetailTracks.Count(t => t.IsSelected && t.CanDownload);
    public bool HasSelectedDownloadable => SelectedDownloadableCount > 0;

    public string AlbumTracksSummary => BuildAlbumTracksSummary();

    partial void OnIsLoadingMoreAlbumTracksChanged(bool value) => NotifyAlbumDetailPagingUi();

    partial void OnIsLoadingMoreArtistTracksChanged(bool value) => NotifyArtistTracksPagingUi();

    public string AlbumDetailSubtitle
    {
        get
        {
            if (SelectedAlbum == null || SelectedArtist == null)
                return string.Empty;

            var line = SelectedArtist.Name;
            if (SelectedAlbum.Year is > 0)
                line += $" · {SelectedAlbum.Year}";
            return line;
        }
    }

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

    public string ArtistBioPreviewText
    {
        get
        {
            var text = !string.IsNullOrWhiteSpace(ArtistSummary)
                ? ArtistSummary
                : ArtistDescription;
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            if (!HasLongArtistBio || IsDescriptionExpanded)
                return text;
            return text.Length <= 360 ? text : text[..360].TrimEnd();
        }
    }

    public string ArtistBioPreviewDisplay => FormatFirstLineIndent(ArtistBioPreviewText);
    public string ArtistBioExpandedDisplay => FormatFirstLineIndent(
        !string.IsNullOrWhiteSpace(ArtistSummary) ? ArtistSummary! : ArtistDescription ?? string.Empty);

    public bool HasLongArtistBio
    {
        get
        {
            var text = !string.IsNullOrWhiteSpace(ArtistSummary) ? ArtistSummary : ArtistDescription;
            return (text?.Length ?? 0) > 220;
        }
    }

    public bool ShowArtistBioExpandLink => HasArtistBioSection && HasLongArtistBio && !IsDescriptionExpanded;
    public bool ShowArtistBioCollapseLink => HasArtistBioSection && HasLongArtistBio && IsDescriptionExpanded;
    public bool ShowArtistBioCollapsedPlain =>
        HasArtistBioSection && !IsDescriptionExpanded && !HasLongArtistBio;
    public bool ShowArtistBioExpandedPlain => IsDescriptionExpanded && !HasLongArtistBio;

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

    private AudioQuality? PreferredQuality => DownloadQualityPreference.ToFilterQuality(
        DownloadQualityPreference.NormalizeIndex(App.Settings.Current.DefaultDownloadQualityIndex));

    public bool HasArtistAlbums => ArtistAlbums.Count > 0;
    public string ArtistAlbumsStatsLine
    {
        get
        {
            if (SelectedArtist == null)
                return string.Empty;

            if (ArtistAlbums.Count > 0)
            {
                var line = string.Format(_l10n.Get("Library.Artists.AlbumStatsTotal"), ArtistAlbums.Count);
                if (SelectedArtist.AlbumCount > 0 && SelectedArtist.AlbumCount != ArtistAlbums.Count)
                    line = string.Format(
                        _l10n.Get("Library.Artists.AlbumStatsWithLibrary"),
                        ArtistAlbums.Count,
                        SelectedArtist.AlbumCount);
                return line;
            }

            return SelectedArtist.AlbumCount > 0
                ? string.Format(_l10n.Get("Library.Artists.AlbumStatsTotal"), SelectedArtist.AlbumCount)
                : string.Empty;
        }
    }
    [ObservableProperty] private string? _artistAlbumsSourceHint;
    public bool HasArtistPhoto => ArtistPhotoLarge is { Length: > 0 };
    public bool HasArtistSummary => !string.IsNullOrWhiteSpace(ArtistSummary);
    public bool HasArtistDescription => !string.IsNullOrWhiteSpace(ArtistDescription);
    public bool HasLongDescription => HasLongArtistBio;
    public bool HasArtistSource => !string.IsNullOrWhiteSpace(ArtistSourceUrl);
    public string ArtistSourceLinkText =>
        string.IsNullOrWhiteSpace(ArtistSourceName)
            ? _l10n.Get("Library.Artists.SourceLinkView")
            : string.Format(
                _l10n.Get("Library.Artists.SourceLinkOn"),
                ServiceMessageLocalizer.Resolve(_l10n, ArtistSourceName));
    public bool ShowProfileStatus => !string.IsNullOrWhiteSpace(ProfileStatusMessage);
    public bool HasArtistBioSection =>
        HasArtistSummary || HasArtistDescription || ShowProfileStatus || IsLoadingProfile;
    public int DescriptionMaxLines => IsDescriptionExpanded ? 0 : 6;
    public string DescriptionToggleText => IsDescriptionExpanded
        ? _l10n.Get("Library.Albums.Collapse")
        : _l10n.Get("Library.Albums.ExpandFull");

    private void NotifyArtistBioDisplayChanged()
    {
        OnPropertyChanged(nameof(HasArtistSummary));
        OnPropertyChanged(nameof(HasArtistDescription));
        OnPropertyChanged(nameof(ShowProfileStatus));
        OnPropertyChanged(nameof(HasArtistBioSection));
        OnPropertyChanged(nameof(HasLongArtistBio));
        OnPropertyChanged(nameof(HasLongDescription));
        OnPropertyChanged(nameof(ArtistBioPreviewText));
        OnPropertyChanged(nameof(ArtistBioPreviewDisplay));
        OnPropertyChanged(nameof(ArtistBioExpandedDisplay));
        OnPropertyChanged(nameof(ShowArtistBioExpandLink));
        OnPropertyChanged(nameof(ShowArtistBioCollapseLink));
        OnPropertyChanged(nameof(ShowArtistBioCollapsedPlain));
        OnPropertyChanged(nameof(ShowArtistBioExpandedPlain));
    }

    partial void OnIsDescriptionExpandedChanged(bool value) => NotifyArtistBioDisplayChanged();

    partial void OnIsLoadingProfileChanged(bool value)
    {
        OnPropertyChanged(nameof(HasArtistBioSection));
        NotifyArtistBioDisplayChanged();
    }

    partial void OnIsRefreshingArtistAlbumsChanged(bool value) =>
        OnPropertyChanged(nameof(ShowArtistAlbumsSkeleton));

    partial void OnPendingRemoveTrackChanged(TrackItemViewModel? value)
    {
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        OnPropertyChanged(nameof(ShowRemoveTrackAlternateConfirm));
    }

    partial void OnAlbumDetailDownloadQualityIndexChanged(int value)
    {
        foreach (var track in SelectedAlbumTracks)
            track.OnlineVm?.NotifyPreferredDownloadChanged();
    }

    partial void OnArtistSummaryChanged(string? value) => NotifyArtistBioDisplayChanged();

    public ArtistsPageViewModel(
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
        _services.Favorites.FavoritesChanged += (_, _) => _ = OnFavoritesChangedAsync();
        _onlineFavorites.Changed += (_, _) => Dispatcher.UIThread.Post(SyncSelectedAlbumOnlineFavoriteStates);
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(PageHeaderText));
        OnPropertyChanged(nameof(BrowsePageTitle));
        OnPropertyChanged(nameof(FilterFieldLabel));
        OnPropertyChanged(nameof(SearchWatermarkText));
        OnPropertyChanged(nameof(SearchButtonLabel));
        OnPropertyChanged(nameof(ResetButtonLabel));
        OnPropertyChanged(nameof(DataGovernanceButtonLabel));
        OnPropertyChanged(nameof(DataGovernanceToolTip));
        OnPropertyChanged(nameof(DataGovernancePopconfirmTitle));
        OnPropertyChanged(nameof(DataGovernancePopconfirmDescription));
        OnPropertyChanged(nameof(DataGovernanceConfirmText));
        OnPropertyChanged(nameof(EmptyFilterTitle));
        OnPropertyChanged(nameof(EmptyFilterAction));
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryDescription));
        OnPropertyChanged(nameof(PlayAllLabel));
        OnPropertyChanged(nameof(ShufflePlayLabel));
        OnPropertyChanged(nameof(RefreshProfileLabel));
        OnPropertyChanged(nameof(IntroSectionLabel));
        OnPropertyChanged(nameof(AlbumsSectionLabel));
        OnPropertyChanged(nameof(TracksSectionLabel));
        OnPropertyChanged(nameof(ExpandFullLabel));
        OnPropertyChanged(nameof(CollapseLabel));
        OnPropertyChanged(nameof(DownloadSelectedLabel));
        OnPropertyChanged(nameof(DownloadMissingLabel));
        OnPropertyChanged(nameof(SelectAllDownloadableLabel));
        OnPropertyChanged(nameof(DeselectAllLabel));
        OnPropertyChanged(nameof(LoadMoreLabel));
        OnPropertyChanged(nameof(QualityPriorityLabel));
        OnPropertyChanged(nameof(AlbumDetailQualityOptionLabels));
        OnPropertyChanged(nameof(AlbumDescriptionSourceDisplay));
        OnPropertyChanged(nameof(AlbumIntroSectionLabel));
        OnPropertyChanged(nameof(AlbumTracksSectionLabel));
        OnPropertyChanged(nameof(BrowseStatsLine));
        OnPropertyChanged(nameof(BrowseFilterEmptyHint));
        OnPropertyChanged(nameof(BrowseSummaryText));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        OnPropertyChanged(nameof(RemoveTrackDeleteWithFileText));
        OnPropertyChanged(nameof(DeletePopconfirmText));
        OnPropertyChanged(nameof(ArtistSourceLinkText));
        OnPropertyChanged(nameof(DescriptionToggleText));
        OnPropertyChanged(nameof(ArtistAlbumsStatsLine));
        OnPropertyChanged(nameof(ArtistTracksSummary));
        OnPropertyChanged(nameof(AlbumTracksSummary));
        NotifyRegionFilterLabelsChanged();
        RefreshArtistItemStatsLines();
        RefreshArtistAlbumItemLabels();
    }

    private void RefreshArtistAlbumItemLabels()
    {
        foreach (var item in ArtistAlbums)
            item.RefreshLocalizedStrings(_l10n);
    }

    private void RefreshArtistItemStatsLines()
    {
        var format = _l10n.Get("Library.Artists.AlbumCountFormat");
        foreach (var item in _artistItems.Values)
            item.RefreshStatsLine(format);
    }

    public Task<IReadOnlyList<PlaylistMenuEntry>> GetAddablePlaylistsAsync(
        CancellationToken ct = default) =>
        _onlinePlaylistActions.GetAddablePlaylistsAsync(ct);

    public async Task AddOnlineTrackToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default)
    {
        var msg = await _onlinePlaylistActions.AddToPlaylistAsync(trackVm, playlistId, ct).ConfigureAwait(true);
        AlbumTracksStatusMessage = string.Format(_l10n.Get("Library.Artists.OnlinePlaylistAdded"), trackVm.Title, msg);
    }

    private void SyncSelectedAlbumOnlineFavoriteStates()
    {
        foreach (var row in SelectedAlbumTracks)
        {
            if (row.OnlineVm == null)
                continue;
            row.OnlineVm.IsOnlineFavorite = _onlineFavorites.IsFavorite(row.OnlineVm.Model);
        }
    }

    partial void OnShowingTracksChanged(bool value)
    {
        ShowingTracksChanged?.Invoke(this, EventArgs.Empty);
        NotifyPageChromeChanged();
    }

    partial void OnShowingAlbumTracksChanged(bool value)
    {
        NotifyPageChromeChanged();
        ShowingTracksChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(AlbumTracksSummary));
    }

    partial void OnSelectedArtistChanged(ArtistListItemViewModel? value)
    {
        OnPropertyChanged(nameof(AlbumTracksSummary));
        OnPropertyChanged(nameof(ArtistAlbumsStatsLine));
        NotifyPageChromeChanged();
    }

    partial void OnSelectedAlbumChanged(ArtistAlbumItemViewModel? value)
    {
        OnPropertyChanged(nameof(AlbumTracksSummary));
        OnPropertyChanged(nameof(AlbumDetailSubtitle));
        NotifyPageChromeChanged();
    }

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

    private void NotifyPageChromeChanged()
    {
        OnPropertyChanged(nameof(ShowStandardPageHeader));
        OnPropertyChanged(nameof(ShowPageSubtitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(ShowPageBackButton));
        OnPropertyChanged(nameof(PageBackButtonLabel));
        OnPropertyChanged(nameof(PageBackCommand));
        OnPropertyChanged(nameof(ShowPageHeaderDivider));
    }

    partial void OnAlbumTracksStatusMessageChanged(string? value) =>
        OnPropertyChanged(nameof(ShowAlbumTracksStatus));

    partial void OnArtistSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveBrowseFilter));
        _artistSearchDebounceCts?.Cancel();
        _artistSearchDebounceCts = new CancellationTokenSource();
        var token = _artistSearchDebounceCts.Token;
        _ = DebouncedArtistSearchAsync(token);
    }

    partial void OnSelectedRegionFilterChanged(ArtistRegionFilter value)
    {
        OnPropertyChanged(nameof(RegionFilterIndex));
        ApplyArtistListFilter();
        OnPropertyChanged(nameof(RegionFilterSummary));
        OnPropertyChanged(nameof(HasActiveBrowseFilter));
    }

    partial void OnFilteredArtistCountChanged(int value) => NotifyBrowseStatsChanged();

    partial void OnFilteredTrackCountChanged(int value) => NotifyBrowseStatsChanged();

    partial void OnFilteredAlbumCountChanged(int value) => NotifyBrowseStatsChanged();

    partial void OnArtistProfilesRefreshMessageChanged(string? value) => NotifyBrowseStatsChanged();

    partial void OnIsRefreshingArtistProfilesChanged(bool value) => NotifyBrowseStatsChanged();

    partial void OnIsRunningDataGovernanceChanged(bool value) => NotifyBrowseStatsChanged();

    private void NotifyBrowseStatsChanged()
    {
        OnPropertyChanged(nameof(TotalArtistCount));
        OnPropertyChanged(nameof(HasFilteredArtists));
        OnPropertyChanged(nameof(ShowBrowseFilterEmpty));
        OnPropertyChanged(nameof(ShowLibraryEmpty));
        OnPropertyChanged(nameof(ShowArtistProfilesRefreshSkeleton));
        OnPropertyChanged(nameof(BrowseStatsLine));
        OnPropertyChanged(nameof(BrowseFilterEmptyHint));
        OnPropertyChanged(nameof(BrowseSummaryText));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    partial void OnArtistPhotoLargeChanged(byte[]? value) => OnPropertyChanged(nameof(HasArtistPhoto));

    partial void OnArtistDescriptionChanged(string? value) => NotifyArtistBioDisplayChanged();

    partial void OnProfileStatusMessageChanged(string? value) => NotifyArtistBioDisplayChanged();

    partial void OnArtistSourceUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasArtistSource));
        OnPropertyChanged(nameof(HasArtistBioSection));
        OnPropertyChanged(nameof(ArtistSourceLinkText));
    }

    partial void OnArtistSourceNameChanged(string? value) => OnPropertyChanged(nameof(ArtistSourceLinkText));

    public event EventHandler? ShowingTracksChanged;
    public event EventHandler<string>? LetterScrollRequested;

    public async Task InitializeAsync() =>
        await ReloadArtistBrowseDataAsync(showLoadingIndicator: true).ConfigureAwait(true);

    private async Task ReloadArtistBrowseDataAsync(bool showLoadingIndicator)
    {
        if (showLoadingIndicator)
        {
            IsLoadingBrowse = true;
            OnPropertyChanged(nameof(ShowBrowseSkeleton));
            OnPropertyChanged(nameof(ShowArtistProfilesRefreshSkeleton));
            OnPropertyChanged(nameof(ShowLibraryEmpty));
        }

        try
        {
            await ReloadPlaylistsMenuAsync().ConfigureAwait(true);
            await RefreshFavoriteFlagsAsync().ConfigureAwait(true);
            var artists = (await _services.Tracks.GetArtistsAsync()).ToList();
            await LoadProfilesForArtistsAsync(artists.Select(a => a.Name)).ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allArtists = artists;
                ApplyArtistListFilter();
            });
        }
        finally
        {
            if (showLoadingIndicator)
            {
                IsLoadingBrowse = false;
                OnPropertyChanged(nameof(ShowBrowseSkeleton));
                OnPropertyChanged(nameof(ShowArtistProfilesRefreshSkeleton));
                OnPropertyChanged(nameof(ShowLibraryEmpty));
            }

            StartBackgroundProfilePrefetch();
        }
    }

    partial void OnIsLoadingBrowseChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseSkeleton));
        OnPropertyChanged(nameof(ShowArtistProfilesRefreshSkeleton));
        OnPropertyChanged(nameof(ShowLibraryEmpty));
    }

    [RelayCommand]
    private void ApplyArtistSearch()
    {
        ApplyArtistListFilter();
        OnPropertyChanged(nameof(PageSubtitle));
    }

    [RelayCommand]
    private void SetRegionFilter(ArtistRegionFilter filter)
    {
        if (SelectedRegionFilter == filter)
            return;

        SelectedRegionFilter = filter;
    }

    [RelayCommand]
    private async Task DataGovernanceAsync()
    {
        if (IsRunningDataGovernance || IsLoadingBrowse || _allArtists.Count == 0)
            return;

        _prefetchCts?.Cancel();
        IsRunningDataGovernance = true;
        IsRefreshingArtistProfiles = true;

        var mergeGroups = 0;
        var profilesUpdated = 0;
        var profilesFailed = 0;
        var profilesSkipped = 0;

        try
        {
            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync().ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
                ArtistProfilesRefreshMessage = _l10n.Get("Library.Artists.Governance.Merging"));

            var mergeResult = await _services.ArtistDuplicateMerge.MergeDuplicateArtistsAsync().ConfigureAwait(false);
            mergeGroups = mergeResult.GroupsMerged;

            await ReloadArtistBrowseDataAsync(showLoadingIndicator: false).ConfigureAwait(false);

            var names = await Dispatcher.UIThread.InvokeAsync(() => GetFilteredArtistNames().ToList());
            var refreshTotal = names.Count;
            if (refreshTotal == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ArtistProfilesRefreshMessage = BuildGovernanceSummary(
                        mergeGroups, 0, 0, 0, mergeResult.AnyChanges);
                    ShowGovernanceResultMessage(mergeGroups, 0, mergeResult.AnyChanges);
                });
                return;
            }

            var processed = 0;
            using var gate = new SemaphoreSlim(4);
            using var governanceCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var refreshTasks = names.Select(name => RefreshArtistProfileInBatchAsync(
                name,
                bulkListRefresh: true,
                resolveLemmaId: false,
                gate,
                governanceCts.Token,
                () =>
                {
                    var done = Interlocked.Increment(ref processed);
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                        ArtistProfilesRefreshMessage = string.Format(
                            _l10n.Get("Library.Artists.Governance.RefreshingProgress"),
                            done,
                            refreshTotal));
                },
                onSkipped: () => Interlocked.Increment(ref profilesSkipped),
                onUpdated: () => Interlocked.Increment(ref profilesUpdated),
                onFailed: () => Interlocked.Increment(ref profilesFailed)));

            await Dispatcher.UIThread.InvokeAsync(() =>
                ArtistProfilesRefreshMessage = string.Format(
                    _l10n.Get("Library.Artists.Governance.RefreshingProgress"),
                    0,
                    refreshTotal));

            await Task.WhenAll(refreshTasks).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ArtistProfilesRefreshMessage = BuildGovernanceSummary(
                    mergeGroups, profilesUpdated, profilesFailed, profilesSkipped,
                    mergeResult.AnyChanges || profilesUpdated > 0);
                ShowGovernanceResultMessage(
                    mergeGroups, profilesUpdated,
                    mergeResult.AnyChanges || profilesUpdated > 0);
                ApplyArtistListFilter();
            });
        }
        catch (Exception ex)
        {
            MessageService.Instance.ShowError(
                ServiceMessageLocalizer.Format(_l10n, "Library.Artists.DataGovernanceFailed", ex.Message));
            ArtistProfilesRefreshMessage = null;
        }
        finally
        {
            IsRunningDataGovernance = false;
            IsRefreshingArtistProfiles = false;
            StartBackgroundProfilePrefetch();
        }
    }

    private static HashSet<string> CollectPerformerNamesForGovernance(IReadOnlyList<TrackArtistFields> tracks)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in tracks)
        {
            foreach (var field in new[] { track.Artist, track.AlbumArtist })
            {
                foreach (var name in ArtistFieldParser.Split(field))
                {
                    if (ArtistPerformerClassifier.IsLikelyPerformer(name))
                        names.Add(name);
                }
            }
        }

        return names;
    }

    private string BuildGovernanceSummary(
        int mergeGroups, int updated, int failed, int skipped, bool anyChanges)
    {
        if (!anyChanges && updated == 0 && mergeGroups == 0)
            return _l10n.Get("Library.Artists.Governance.CompleteEmpty");

        var parts = new List<string>();
        if (mergeGroups > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.MergedGroups"), mergeGroups));
        if (updated > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.ProfilesUpdated"), updated));
        if (failed > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.ProfilesFailed"), failed));
        if (skipped > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.ProfilesSkipped"), skipped));

        return parts.Count > 0
            ? string.Format(_l10n.Get("Library.Artists.Governance.CompletePrefix"), string.Join(", ", parts))
            : _l10n.Get("Library.Artists.Governance.Complete");
    }

    private void ShowGovernanceResultMessage(int mergeGroups, int profilesUpdated, bool anyChanges)
    {
        if (!anyChanges)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Library.Artists.Governance.CompleteEmpty"));
            return;
        }

        var parts = new List<string>();
        if (mergeGroups > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.MergedGroupsDuplicate"), mergeGroups));
        if (profilesUpdated > 0)
            parts.Add(string.Format(_l10n.Get("Library.Artists.Governance.ProfilesUpdated"), profilesUpdated));

        MessageService.Instance.ShowSuccess(
            parts.Count > 0
                ? string.Format(_l10n.Get("Library.Artists.Governance.CompletePrefix"), string.Join(", ", parts))
                : _l10n.Get("Library.Artists.Governance.Complete"));
    }

    [RelayCommand]
    private async Task RefreshArtistProfilesAsync()
    {
        if (IsRefreshingArtistProfiles || _allArtists.Count == 0)
            return;

        _prefetchCts?.Cancel();
        IsRefreshingArtistProfiles = true;

        var names = SelectedArtist != null
            ? [SelectedArtist.Name]
            : GetFilteredArtistNames().ToList();

        if (names.Count == 0)
        {
            IsRefreshingArtistProfiles = false;
            StartBackgroundProfilePrefetch();
            return;
        }

        var singleArtist = SelectedArtist != null;
        var bulkListRefresh = !singleArtist;
        var total = names.Count;
        var processed = 0;
        var updated = 0;
        var failed = 0;
        var skipped = 0;

        ArtistProfilesRefreshMessage = singleArtist
            ? string.Format(_l10n.Get("Library.Artists.ProfileRefreshingSingle"), names[0])
            : string.Format(_l10n.Get("Library.Artists.Governance.RefreshingProgress"), 0, total);

        try
        {
            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync().ConfigureAwait(false);

            using var gate = new SemaphoreSlim(4);
            using var refreshCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var tasks = names.Select(name => RefreshArtistProfileInBatchAsync(
                name,
                bulkListRefresh,
                singleArtist,
                gate,
                refreshCts.Token,
                () =>
                {
                    var done = Interlocked.Increment(ref processed);
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                        ArtistProfilesRefreshMessage = string.Format(
                            _l10n.Get("Library.Artists.Governance.RefreshingProgress"),
                            done,
                            total));
                },
                onSkipped: () => Interlocked.Increment(ref skipped),
                onUpdated: () => Interlocked.Increment(ref updated),
                onFailed: () => Interlocked.Increment(ref failed)));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
                ArtistProfilesRefreshMessage = names.Count == 1
                    ? (updated > 0
                        ? string.Format(_l10n.Get("Library.Artists.ProfileUpdatedSingle"), names[0])
                        : string.Format(_l10n.Get("Library.Artists.ProfileUpdateFailedSingle"), names[0]))
                    : failed > 0 || skipped > 0
                        ? string.Format(_l10n.Get("Library.Artists.ProfileRefreshCompleteMixed"), updated, failed)
                          + (skipped > 0
                              ? string.Format(_l10n.Get("Library.Artists.ProfileRefreshSkippedSuffix"), skipped)
                              : string.Empty)
                        : string.Format(_l10n.Get("Library.Artists.ProfileRefreshCompleteAll"), updated));
        }
        finally
        {
            IsRefreshingArtistProfiles = false;
            StartBackgroundProfilePrefetch();
        }
    }

    private async Task RefreshArtistProfileInBatchAsync(
        string name,
        bool bulkListRefresh,
        bool resolveLemmaId,
        SemaphoreSlim gate,
        CancellationToken ct,
        Action reportProgress,
        Action onSkipped,
        Action onUpdated,
        Action onFailed)
    {
        if (bulkListRefresh)
        {
            _profiles.TryGetValue(name, out var cached);
            if (cached != null && !_services.ArtistProfiles.NeedsFetch(cached, force: false))
            {
                onSkipped();
                reportProgress();
                return;
            }
        }

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var perArtistCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perArtistCts.CancelAfter(TimeSpan.FromSeconds(45));

            _profiles.TryGetValue(name, out var existing);
            var profile = await _services.ArtistProfiles
                .RefreshFromBaiduAsync(name, existing, perArtistCts.Token, resolveLemmaId: resolveLemmaId)
                .ConfigureAwait(false);
            _profiles[name] = profile;
            var raw = _services.ArtistProfiles.LoadPhotoBytes(profile);
            ArtistPhotoCache.Invalidate(name);
            var thumb = ArtistPhotoCache.GetOrLoad(name, raw, forceReload: true);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateListItemProfile(name, profile, thumb);
                if (SelectedArtist?.Name == name)
                {
                    ApplyDetailProfile(profile);
                    _ = BuildArtistAlbumsAsync(profile);
                }
            });

            if (profile.FetchStatus == ArtistProfileFetchStatus.Ok)
                onUpdated();
            else if (profile.FetchStatus is ArtistProfileFetchStatus.Error or ArtistProfileFetchStatus.NotFound)
                onFailed();
        }
        catch
        {
            onFailed();
        }
        finally
        {
            gate.Release();
            reportProgress();
        }
    }

    private IEnumerable<string> GetFilteredArtistNames()
    {
        var q = ArtistSearchText.Trim();
        IEnumerable<ArtistGroup> src = _allArtists;
        if (!string.IsNullOrEmpty(q))
            src = src.Where(a => ArtistPinyinHelper.MatchesSearch(a.Name, q));
        if (SelectedRegionFilter != ArtistRegionFilter.All)
            src = src.Where(a => MatchesRegionFilter(a.Name));
        return src.Select(a => a.Name);
    }

    [RelayCommand]
    private void ClearBrowseFilters()
    {
        _artistSearchDebounceCts?.Cancel();
        ArtistSearchText = string.Empty;
        SelectedRegionFilter = ArtistRegionFilter.All;
        ApplyArtistListFilter();
        OnPropertyChanged(nameof(HasActiveBrowseFilter));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    private async Task DebouncedArtistSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct).ConfigureAwait(true);
            ApplyArtistListFilter();
            OnPropertyChanged(nameof(PageSubtitle));
        }
        catch (OperationCanceledException)
        {
            // 用户继续输入
        }
    }

    [RelayCommand]
    private void JumpToLetter(string? letter)
    {
        if (string.IsNullOrEmpty(letter) || ShowingTracks)
            return;
        LetterScrollRequested?.Invoke(this, letter);
    }

    [RelayCommand]
    private async Task SelectArtistAsync(ArtistListItemViewModel? artist)
    {
        if (artist == null) return;
        CloseAlbumDetail();
        SelectedArtist = artist;
        ShowingTracks = true;
        IsDescriptionExpanded = false;
        ClearDetailProfile();
        _currentArtistTracks = (await _services.Tracks.GetByArtistAsync(artist.Name)).ToList();
        await Dispatcher.UIThread.InvokeAsync(ApplyArtistTracksFilter);
        OnPropertyChanged(nameof(PageSubtitle));
        await ApplyCachedProfileForDetailAsync(artist.Name, artist);
        _ = LoadArtistDetailFromLibraryAsync(artist.Name);
    }

    /// <summary>在艺术家详情页时，顶栏再次点「艺术家」应回到列表。</summary>
    public void ExitDetailViewIfOpen()
    {
        if (ShowingAlbumTracks)
        {
            BackToArtistDetail();
            return;
        }

        if (!ShowingTracks)
            return;

        BackToArtists();
    }

    [RelayCommand]
    private void BackToArtistDetail() => CloseAlbumDetail();

    [RelayCommand]
    private void BackToArtists()
    {
        _profileFetchCts?.Cancel();
        CloseAlbumDetail();
        ShowingTracks = false;
        SelectedArtist = null;
        ArtistAlbums.Clear();
        ClearDetailProfile();
        OnPropertyChanged(nameof(HasArtistAlbums));
        OnPropertyChanged(nameof(ShowArtistAlbumsSkeleton));
        ApplyArtistListFilter();
        OnPropertyChanged(nameof(PageSubtitle));
    }

    [RelayCommand]
    private void ToggleDescriptionExpanded() => IsDescriptionExpanded = !IsDescriptionExpanded;

    [RelayCommand]
    private async Task RefreshArtistProfileAsync()
    {
        if (SelectedArtist == null) return;
        var name = SelectedArtist.Name;
        _profileFetchCts?.Cancel();
        _profileFetchCts = new CancellationTokenSource();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_profileFetchCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
        var ct = timeoutCts.Token;

        _profiles.TryGetValue(name, out var existing);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoadingProfile = true;
            IsRefreshingArtistAlbums = true;
            ProfileStatusMessage = _l10n.Get("Library.Artists.ProfileRefreshing");
        });

        try
        {
            var refresh = _services.ArtistDetailRefresh;
            var request = new ArtistDetailRefreshRequest(name, _currentArtistTracks, existing);
            var progress = new ArtistDetailRefreshProgress
            {
                OnProfileReady = profile =>
                {
                    if (ct.IsCancellationRequested || SelectedArtist?.Name != name)
                        return;

                    _profiles[name] = profile;
                    var raw = refresh.LoadPhotoBytes(profile);
                    ArtistPhotoCache.Invalidate(name);
                    var thumb = ArtistPhotoCache.GetOrLoad(name, raw, forceReload: true);
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (SelectedArtist?.Name != name)
                            return;

                        UpdateListItemProfile(name, profile, thumb);
                        ApplyDetailProfile(profile);
                        IsLoadingProfile = false;
                        ProfileStatusMessage = IsRefreshingArtistAlbums
                            ? _l10n.Get("Library.Artists.AlbumsRefreshing")
                            : null;
                    });
                },
                OnAlbumsReady = (items, hint) =>
                {
                    if (ct.IsCancellationRequested || SelectedArtist?.Name != name)
                        return;

                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        if (SelectedArtist?.Name != name)
                            return;

                        ApplyArtistAlbumCatalog(items, hint);
                        await LoadArtistAlbumCoversAsync(items, ct);
                        IsRefreshingArtistAlbums = false;
                        if (!IsLoadingProfile)
                            ProfileStatusMessage = null;
                    });
                }
            };

            var result = await refresh.RefreshAsync(request, progress, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested || SelectedArtist?.Name != name)
                return;

            _profiles[name] = result.Profile;
        }
        catch (OperationCanceledException) { }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedArtist?.Name == name)
                    ProfileStatusMessage = _l10n.Get("Library.Artists.ProfileRefreshRetry");
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedArtist?.Name != name)
                    return;

                IsLoadingProfile = false;
                IsRefreshingArtistAlbums = false;
                if (ProfileStatusMessage == _l10n.Get("Library.Artists.ProfileRefreshing")
                    || ProfileStatusMessage == _l10n.Get("Library.Artists.AlbumsRefreshing"))
                    ProfileStatusMessage = null;
            });
        }
    }

    [RelayCommand]
    private void OpenArtistSource()
    {
        if (string.IsNullOrWhiteSpace(ArtistSourceUrl))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(ArtistSourceUrl) { UseShellExecute = true });
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        var tracks = ActiveArtistTracksForPlayback;
        if (tracks.Count == 0) return;
        var label = SelectedArtist?.Name ?? _l10n.Get("Library.Artists.DefaultArtistLabel");
        await _player.PlayTrackAsync(tracks[0], tracks, queueSourceLabel: label);
    }

    [RelayCommand]
    private async Task ShufflePlayAsync()
    {
        var tracks = ActiveArtistTracksForPlayback.OrderBy(_ => Random.Shared.Next()).ToList();
        if (tracks.Count == 0) return;
        _player.IsShuffleOn = true;
        var label = SelectedArtist?.Name ?? _l10n.Get("Library.Artists.DefaultArtistLabel");
        await _player.PlayTrackAsync(tracks[0], tracks, queueSourceLabel: label);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(TrackItemViewModel? item)
    {
        if (item == null || item.Track.Id <= 0) return;
        try
        {
            var added = await _services.Favorites.ToggleAsync(item.Track.Id);
            item.IsFavorite = added;
            if (added) _favoriteTrackIds.Add(item.Track.Id);
            else _favoriteTrackIds.Remove(item.Track.Id);
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private async Task AddToPlaylistAsync((TrackItemViewModel Track, long PlaylistId) args)
    {
        var (trackVm, playlistId) = args;
        var trackId = trackVm?.Track.Id ?? 0;
        var trackTitle = trackVm?.Track.Title ?? _l10n.Get("Library.Artists.DefaultTrackTitle");
        if (trackId <= 0 || playlistId <= 0)
        {
            AlbumTracksStatusMessage = _l10n.Get("Library.Tracks.PlaylistInvalid");
            MessageService.Instance.ShowError(AlbumTracksStatusMessage);
            return;
        }

        try
        {
            var playlist = PlaylistsForMenu.FirstOrDefault(p => p.Id == playlistId);
            var playlistName = playlist?.Name
                ?? string.Format(_l10n.Get("Discover.Online.PlaylistFallbackName"), playlistId);
            var alreadyExists = await _services.PlaylistService
                .IsTrackInPlaylistAsync(playlistId, trackId)
                .ConfigureAwait(true);
            if (alreadyExists)
            {
                AlbumTracksStatusMessage = string.Format(
                    _l10n.Get("Library.Tracks.PlaylistDuplicate"),
                    trackTitle,
                    playlistName);
                MessageService.Instance.ShowInfo(AlbumTracksStatusMessage);
                return;
            }

            await _services.PlaylistService.AddTrackAsync(playlistId, trackId).ConfigureAwait(true);
            AlbumTracksStatusMessage = string.Format(
                _l10n.Get("Library.Tracks.PlaylistAdded"),
                trackTitle,
                playlistName);
            MessageService.Instance.ShowSuccess(AlbumTracksStatusMessage);
        }
        catch (Exception ex)
        {
            AlbumTracksStatusMessage = ServiceMessageLocalizer.Format(
                _l10n,
                "Library.Tracks.PlaylistAddFailed",
                ex.Message);
            MessageService.Instance.ShowError(AlbumTracksStatusMessage);
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
                return;

            _player.RemoveDeletedTrack(item.Track);
            _favoriteTrackIds.Remove(item.Track.Id);
            _currentArtistTracks.RemoveAll(t => t.Id == item.Track.Id);
            ApplyArtistTracksFilter();
        }
        catch
        {
            // 忽略
        }
        finally
        {
            if (ReferenceEquals(PendingRemoveTrack, item))
                PendingRemoveTrack = null;
        }
    }

    public void PrepareRemoveTrackConfirm(TrackItemViewModel item) => PendingRemoveTrack = item;

    [RelayCommand]
    private async Task RemoveTrackAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item, deleteFile: false).ConfigureAwait(true);

    private async Task LoadProfilesForArtistsAsync(IEnumerable<string> names)
    {
        _profiles = (await _services.ArtistProfiles.GetManyAsync(names)).ToDictionary(
            kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    private void NotifyRegionFilterLabelsChanged()
    {
        OnPropertyChanged(nameof(RegionAllLabel));
        OnPropertyChanged(nameof(RegionMandarinLabel));
        OnPropertyChanged(nameof(RegionJapaneseLabel));
        OnPropertyChanged(nameof(RegionKoreanLabel));
        OnPropertyChanged(nameof(RegionWesternLabel));
        OnPropertyChanged(nameof(RegionOtherLabel));
        RegionSegmentLabels =
        [
            RegionAllLabel,
            RegionMandarinLabel,
            RegionJapaneseLabel,
            RegionKoreanLabel,
            RegionWesternLabel,
            RegionOtherLabel
        ];
        OnPropertyChanged(nameof(RegionSegmentLabels));
        OnPropertyChanged(nameof(RegionFilterSummary));
    }

    private bool MatchesRegionFilter(string artistName)
    {
        _profiles.TryGetValue(artistName, out var profile);
        return ArtistBrowseClassifier.ClassifyRegion(artistName, profile) == SelectedRegionFilter;
    }

    private string FormatRegionFilterLabel(ArtistRegionFilter filter)
    {
        var count = ArtistBrowseClassifier.CountByRegion(
            _allArtists.Select(a => (a.Name, _profiles.GetValueOrDefault(a.Name))),
            filter);
        var label = ArtistRegionFilterLabels.GetLabel(filter, _l10n);
        return count > 0 ? $"{label} {count}" : label;
    }

    private void ApplyArtistListFilter()
    {
        var q = ArtistSearchText.Trim();
        IEnumerable<ArtistGroup> src = _allArtists;
        if (!string.IsNullOrEmpty(q))
            src = src.Where(a => ArtistPinyinHelper.MatchesSearch(a.Name, q));
        if (SelectedRegionFilter != ArtistRegionFilter.All)
            src = src.Where(a => MatchesRegionFilter(a.Name));

        var filtered = src.ToList();
        FilteredArtistCount = filtered.Count;
        FilteredTrackCount = filtered.Sum(a => a.TrackCount);
        FilteredAlbumCount = filtered.Sum(a => a.AlbumCount);
        OnPropertyChanged(nameof(HasActiveBrowseFilter));
        NotifyBrowseStatsChanged();
        NotifyRegionFilterLabelsChanged();

        var newBrowseItems = new List<object>();
        var newArtistItems = new Dictionary<string, ArtistListItemViewModel>(StringComparer.Ordinal);

        var grouped = filtered
            .GroupBy(a => ArtistSortHelper.GetSectionKey(a.Name))
            .OrderBy(g => g.Key, Comparer<string>.Create(ArtistSortHelper.CompareSectionKeys));

        foreach (var group in grouped)
        {
            newBrowseItems.Add(new ArtistSectionHeaderViewModel
            {
                Letter = group.Key,
                ArtistCount = group.Count()
            });

            foreach (var a in group.OrderBy(x => x.Name, Comparer<string>.Create(ArtistSortHelper.CompareArtistNames)))
            {
                var item = new ArtistListItemViewModel(a);
                item.RefreshStatsLine(_l10n.Get("Library.Artists.AlbumCountFormat"));
                _profiles.TryGetValue(a.Name, out var profile);
                var raw = _services.ArtistProfiles.LoadPhotoBytes(profile);
                var thumb = ArtistPhotoCache.GetOrLoad(a.Name, raw, forceReload: raw is { Length: > 0 });
                item.ApplyProfile(profile, thumb);
                newArtistItems[a.Name] = item;
                newBrowseItems.Add(item);
            }
        }

        BrowseItems.Clear();
        _artistItems.Clear();
        foreach (var item in newBrowseItems)
            BrowseItems.Add(item);
        foreach (var (name, item) in newArtistItems)
            _artistItems[name] = item;
    }

    private void ApplyDetailProfile(ArtistProfile profile, byte[]? photoFallback = null)
    {
        ArtistSummary = profile.Summary;
        ArtistDescription = profile.Description;
        ArtistSourceUrl = profile.SourceUrl;
        ArtistSourceName = profile.SourceName;
        ProfileStatusMessage = profile.FetchStatus switch
        {
            ArtistProfileFetchStatus.NotFound => ServiceMessageLocalizer.Resolve(_l10n, profile.ErrorMessage)
                is { Length: > 0 } notFound
                    ? notFound
                    : _l10n.Get("Artist.Profile.NotFoundCached"),
            ArtistProfileFetchStatus.Error => ServiceMessageLocalizer.Resolve(_l10n, profile.ErrorMessage)
                is { Length: > 0 } error
                    ? error
                    : _l10n.Get("Artist.Profile.LoadFailed"),
            ArtistProfileFetchStatus.Ok when string.IsNullOrWhiteSpace(profile.Summary)
                && string.IsNullOrWhiteSpace(profile.PhotoFileName) =>
                _l10n.Get("Library.Artists.ProfileEmptyHint"),
            _ => null
        };

        var raw = _services.ArtistDetailRefresh.LoadPhotoBytes(profile);
        if (raw is not { Length: > 0 })
            raw = photoFallback;

        ArtistPhotoLarge = raw is { Length: > 0 }
            ? CoverImageScaler.ScaleToMaxEdge(raw, 192)
            : null;

        ArtistPhotoCache.Invalidate(profile.Name);
        var thumb = ArtistPhotoCache.GetOrLoad(profile.Name, raw, forceReload: raw is { Length: > 0 });
        UpdateListItemProfile(profile.Name, profile, thumb);
    }

    /// <summary>进入详情：只读库内 ArtistProfiles + 本地头像文件，不请求网络。</summary>
    private async Task ApplyCachedProfileForDetailAsync(
        string artistName,
        ArtistListItemViewModel? listItem = null)
    {
        if (!_profiles.TryGetValue(artistName, out var profile))
            profile = await _services.ArtistDetailRefresh.GetCachedProfileAsync(artistName).ConfigureAwait(false);

        byte[]? listThumb = listItem?.PhotoThumb;
        if (listThumb == null && _artistItems.TryGetValue(artistName, out var item))
            listThumb = item.PhotoThumb;

        if (profile == null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedArtist?.Name != artistName)
                    return;

                if (listThumb is { Length: > 0 })
                    ArtistPhotoLarge = CoverImageScaler.ScaleToMaxEdge(listThumb, 192);

                ProfileStatusMessage = _l10n.Get("Library.Artists.ProfileMissingHint");
            });
            return;
        }

        _profiles[artistName] = profile;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (SelectedArtist?.Name != artistName)
                return;

            ApplyDetailProfile(profile, listThumb);
        });
    }

    private void UpdateListItemProfile(string artistName, ArtistProfile profile, byte[]? thumb)
    {
        if (_artistItems.TryGetValue(artistName, out var item))
            item.ApplyProfile(profile, thumb);
    }

    private async Task ReconcileBrowseRegionsInBackgroundAsync(CancellationToken ct)
    {
        if (_allArtists.Count == 0)
            return;

        var names = _allArtists.Select(a => a.Name).ToList();
        var updated = await _services.ArtistProfiles.ReconcileBrowseRegionsAsync(names, ct)
            .ConfigureAwait(false);
        if (updated == 0)
            return;

        var refreshed = await _services.ArtistProfiles.GetManyAsync(names, ct).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var (name, profile) in refreshed)
                _profiles[name] = profile;
            ApplyArtistListFilter();
        });
    }

    private void StartBackgroundProfilePrefetch()
    {
        _prefetchCts?.Cancel();
        _prefetchCts = new CancellationTokenSource();
        var ct = _prefetchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await ReconcileBrowseRegionsInBackgroundAsync(ct).ConfigureAwait(false);

                var pending = _allArtists
                    .Select(a => a.Name)
                    .Where(name =>
                    {
                        _profiles.TryGetValue(name, out var p);
                        return _services.ArtistProfiles.NeedsFetch(p);
                    })
                    .ToList();

                await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

                using var gate = new SemaphoreSlim(3);
                var prefetchTasks = pending.Select(async name =>
                {
                    if (ct.IsCancellationRequested)
                        return;

                    await gate.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var profile = await _services.ArtistProfiles
                            .GetOrFetchAsync(name, forceRefresh: false, ct)
                            .ConfigureAwait(false);
                        _profiles[name] = profile;
                        var raw = _services.ArtistProfiles.LoadPhotoBytes(profile);
                        ArtistPhotoCache.Invalidate(name);
                        var thumb = ArtistPhotoCache.GetOrLoad(name, raw, forceReload: true);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                            UpdateListItemProfile(name, profile, thumb));
                    }
                    finally
                    {
                        gate.Release();
                    }
                });

                await Task.WhenAll(prefetchTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 忽略
            }
        }, ct);
    }

    private void ClearDetailProfile()
    {
        ArtistSummary = null;
        ArtistDescription = null;
        ArtistPhotoLarge = null;
        ArtistSourceUrl = null;
        ArtistSourceName = null;
        ProfileStatusMessage = null;
        IsLoadingProfile = false;
    }

    private void CloseAlbumDetail()
    {
        ShowingAlbumTracks = false;
        SelectedAlbum = null;
        SelectedAlbumTracks.Clear();
        _albumDetailPager.Clear();
        AlbumDescription = null;
        AlbumDescriptionSource = null;
        IsAlbumDescriptionExpanded = false;
        AlbumTracksStatusMessage = null;
        IsLoadingAlbumTracks = false;
        OnPropertyChanged(nameof(AlbumOwnedCount));
        OnPropertyChanged(nameof(AlbumMissingCount));
        OnPropertyChanged(nameof(HasAlbumMissingTracks));
        OnPropertyChanged(nameof(AlbumTracksSummary));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    [RelayCommand]
    private async Task SelectAlbumAsync(ArtistAlbumItemViewModel? album)
    {
        if (album == null || SelectedArtist == null)
            return;

        SelectedAlbum = album;
        ShowingAlbumTracks = true;
        AlbumDescription = null;
        AlbumDescriptionSource = null;
        IsAlbumDescriptionExpanded = false;
        AlbumTracksStatusMessage = null;
        OnPropertyChanged(nameof(PageSubtitle));
        await LoadSelectedAlbumTracksAsync();
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

    [RelayCommand]
    private async Task DownloadSelectedAlbumTracksAsync()
    {
        if (!HasSelectedDownloadable)
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

        AlbumTracksStatusMessage = enqueued > 0
            ? (skipped > 0
                ? string.Format(_l10n.Get("Library.Artists.DownloadEnqueuedWithSkip"), enqueued, skipped)
                : string.Format(_l10n.Get("Library.Artists.DownloadEnqueuedOnly"), enqueued))
            : string.Format(_l10n.Get("Library.Artists.DownloadNoneSkipped"), skipped);
    }

    private void NotifyAlbumTrackSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedDownloadableCount));
        OnPropertyChanged(nameof(HasSelectedDownloadable));
    }

    [RelayCommand]
    private Task DownloadMissingAlbumTracksAsync()
    {
        if (!HasAlbumMissingTracks)
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
            var quality = UseQualityPriorityDownload
                ? OnlineTrackDownloadQualityResolver.ResolveQualityPriority(track)
                : SelectedDownloadQuality;
            var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(track, quality));
            if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                enqueued++;
            else
                skipped++;
        }

        AlbumTracksStatusMessage = enqueued > 0
            ? (skipped > 0
                ? string.Format(_l10n.Get("Library.Artists.DownloadEnqueuedWithSkip"), enqueued, skipped)
                : string.Format(_l10n.Get("Library.Artists.DownloadEnqueuedOnly"), enqueued))
            : string.Format(_l10n.Get("Library.Artists.DownloadNoneDuplicate"), skipped);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadMoreAlbumDetailTracksAsync()
    {
        if (!_albumDetailPager.HasMore || IsLoadingMoreAlbumTracks)
            return;

        IsLoadingMoreAlbumTracks = true;
        try
        {
            await Task.Yield();
            _albumDetailPager.AppendNext(SelectedAlbumTracks);
            NotifyAlbumDetailPagingUi();
        }
        finally
        {
            IsLoadingMoreAlbumTracks = false;
        }
    }

    private void NotifyAlbumDetailPagingUi()
    {
        OnPropertyChanged(nameof(ActiveAlbumDetailTracks));
        OnPropertyChanged(nameof(HasMoreAlbumDetailTracks));
        OnPropertyChanged(nameof(AlbumTracksSummary));
        OnPropertyChanged(nameof(AlbumOwnedCount));
        OnPropertyChanged(nameof(AlbumMissingCount));
        OnPropertyChanged(nameof(HasAlbumMissingTracks));
        OnPropertyChanged(nameof(SelectedDownloadableCount));
        OnPropertyChanged(nameof(HasSelectedDownloadable));
    }

    private string BuildAlbumTracksSummary()
    {
        var total = _albumDetailPager.TotalCount;
        if (total <= 0)
        {
            return SelectedAlbum != null
                ? SelectedAlbum.OnlineTrackCount is int online && online > 0
                    ? string.Format(
                        _l10n.Get("Library.Artists.AlbumTracksSummary.OnlineAndLibrary"),
                        online,
                        SelectedAlbum.LibraryTrackCount)
                    : SelectedAlbum.LibraryTrackCount > 0
                        ? string.Format(
                            _l10n.Get("Library.Artists.AlbumTracksSummary.LibraryOnly"),
                            SelectedAlbum.LibraryTrackCount)
                        : string.Empty
                : string.Empty;
        }

        var shown = SelectedAlbumTracks.Count;
        var parts = new List<string>
        {
            string.Format(
                _l10n.Get("Library.Artists.AlbumTracksSummary.TotalWithDownload"),
                total,
                AlbumOwnedCount,
                AlbumMissingCount)
        };
        if (shown < total)
            parts.Add(string.Format(_l10n.Get("Library.Artists.AlbumTracksSummary.Shown"), shown));
        if (_albumDetailPager.HasMore)
            parts.Add(_l10n.Get("Discover.Status.ResultsLoadMore"));
        return string.Join(" · ", parts);
    }

    private async Task LoadSelectedAlbumTracksAsync()
    {
        if (SelectedAlbum == null || SelectedArtist == null)
            return;

        IsLoadingAlbumTracks = true;
        SelectedAlbumTracks.Clear();
        _albumDetailPager.Clear();
        OnPropertyChanged(nameof(AlbumOwnedCount));
        OnPropertyChanged(nameof(AlbumMissingCount));
        OnPropertyChanged(nameof(HasAlbumMissingTracks));
        OnPropertyChanged(nameof(AlbumTracksSummary));

        try
        {
            var artistName = SelectedArtist.Name;
            var albumName = SelectedAlbum.Album;
            var libraryForAlbum = _currentArtistTracks
                .Where(t => LibraryTrackMatcher.AlbumMatches(t.Album, albumName))
                .ToList();

            IReadOnlyList<OnlineTrack> onlineTracks = [];
            string? descriptionForUi = null;
            string? sourceForUi = null;
            if (GoMusicApiClient.IsConfigured)
            {
                var catalog = await _services.AlbumCatalogs
                    .GetOrFetchAsync(artistName, albumName)
                    .ConfigureAwait(false);
                if (catalog != null)
                {
                    onlineTracks = catalog.Tracks;
                    var description = catalog.Description;
                    var descriptionSource = catalog.DescriptionSource;
                    if (AlbumCatalogService.NeedsRichDescription(description, descriptionSource))
                    {
                        var enriched = await _services.AlbumCatalogs.TryEnrichDescriptionAsync(
                                artistName,
                                albumName,
                                catalog.PlatformAlbumId)
                            .ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(enriched.Description))
                        {
                            description = enriched.Description;
                            descriptionSource = enriched.Source;
                        }
                    }

                    descriptionForUi = description;
                    sourceForUi = descriptionSource;
                }
            }
            else if (SelectedAlbum.HasOnlineCatalog)
            {
                var provider = _services.Discover.Search.GetProvider(SelectedAlbum.Platform);
                if (provider != null && !string.IsNullOrWhiteSpace(SelectedAlbum.PlatformAlbumId))
                    onlineTracks = await provider.GetAlbumTracksAsync(SelectedAlbum.PlatformAlbumId);
            }

            var items = AlbumDetailTrackCatalogBuilder.Build(
                artistName,
                albumName,
                libraryForAlbum,
                onlineTracks,
                _services,
                _player,
                ResolveAlbumDetailDownloadQuality,
                message => AlbumTracksStatusMessage = message,
                appModal: _appModal,
                onlineFavorites: _onlineFavorites);

            var owned = items.Count(t => t.IsInLibrary);
            var missing = items.Count - owned;
            SelectedAlbum.LibraryTrackCount = owned;
            SelectedAlbum.OnlineTrackCount = onlineTracks.Count > 0 ? onlineTracks.Count : null;
            SelectedAlbum.MissingTrackCount = missing > 0 ? missing : null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AlbumDescription = AlbumCatalogService.NormalizeDescriptionForDisplay(
                    descriptionForUi,
                    sourceForUi,
                    albumName);
                AlbumDescriptionSource = sourceForUi;
                IsAlbumDescriptionExpanded = false;
                _albumDetailPager.ResetBuffer(items);
                _albumDetailPager.ResetDisplayed(SelectedAlbumTracks);
                NotifyAlbumDetailPagingUi();
                OnPropertyChanged(nameof(PageSubtitle));
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AlbumTracksStatusMessage = _l10n.Get("Library.Artists.AlbumTracksLoadFailed"));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoadingAlbumTracks = false);
        }
    }

    private static string FormatDurationMs(int durationMs) =>
        durationMs > 0
            ? TimeSpan.FromMilliseconds(durationMs).ToString(@"m\:ss")
            : "--:--";

    private async Task LoadArtistDetailFromLibraryAsync(string artistName)
    {
        var artist = SelectedArtist?.Name ?? artistName;
        var cachedAlbums = await _services.ArtistDetailRefresh
            .GetCachedOfficialAlbumsAsync(artistName)
            .ConfigureAwait(false);
        var catalog = _services.ArtistDetailRefresh.BuildCatalog(_currentArtistTracks, artist, cachedAlbums);
        var hint = ArtistAlbumCatalogBuilder.ResolveSourceHint(cachedAlbums);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (SelectedArtist?.Name != artistName)
                return;

            ApplyArtistAlbumCatalog(catalog, hint);
        });
        await LoadArtistAlbumCoversAsync(catalog);
    }

    private async Task BuildArtistAlbumsAsync(
        ArtistProfile? profile,
        bool fetchOnlineOfficial = false,
        CancellationToken ct = default)
    {
        var artistName = SelectedArtist?.Name ?? profile?.Name ?? string.Empty;
        IReadOnlyList<OnlineArtistAlbum> official = [];
        if (fetchOnlineOfficial && profile != null)
            official = await _services.ArtistDetailRefresh.FetchOfficialAlbumsAsync(profile, ct).ConfigureAwait(false);

        var catalog = _services.ArtistDetailRefresh.BuildCatalog(_currentArtistTracks, artistName, official);
        var hint = ArtistAlbumCatalogBuilder.ResolveSourceHint(official);

        await Dispatcher.UIThread.InvokeAsync(() => ApplyArtistAlbumCatalog(catalog, hint));
        await LoadArtistAlbumCoversAsync(catalog, ct);
    }

    private void ApplyArtistAlbumCatalog(IReadOnlyList<ArtistAlbumCatalogItem> catalog, string hint)
    {
        if (catalog.Count == 0 && ArtistAlbums.Count > 0)
            return;

        ArtistAlbums.Clear();
        foreach (var row in catalog)
        {
            ArtistAlbums.Add(new ArtistAlbumItemViewModel
            {
                Album = row.Album,
                LibraryTrackCount = row.LibraryTrackCount,
                Year = row.Year,
                Platform = row.Platform ?? MusicPlatform.NetEase,
                PlatformAlbumId = row.PlatformAlbumId
            });
        }

        ArtistAlbumsSourceHint = ServiceMessageLocalizer.Resolve(_l10n, hint);
        RefreshArtistAlbumItemLabels();
        OnPropertyChanged(nameof(HasArtistAlbums));
        OnPropertyChanged(nameof(ShowArtistAlbumsSkeleton));
        OnPropertyChanged(nameof(ArtistAlbumsStatsLine));
        if (SelectedArtist != null
            && ProfileStatusMessage == _l10n.Get("Library.Artists.AlbumsRefreshing"))
            ProfileStatusMessage = null;
    }

    private async Task LoadArtistAlbumCoversAsync(
        IReadOnlyList<ArtistAlbumCatalogItem> catalog,
        CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = catalog.Select(async row =>
        {
            await gate.WaitAsync(ct);
            try
            {
                byte[]? thumb = null;
                if (!string.IsNullOrWhiteSpace(row.OnlinePicUrl))
                    thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(
                        row.OnlinePicUrl, CoverFetchSource.NetEase, ct);
                if (thumb == null && row.LibrarySample != null)
                    thumb = await AlbumCoverLoader.LoadThumbAsync(_services, row.LibrarySample, ct);

                if (thumb == null)
                    return;

                var albumName = row.Album;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var item = ArtistAlbums.FirstOrDefault(a =>
                        string.Equals(a.Album, albumName, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                        item.CoverThumb = thumb;
                });
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    public void RefreshTrackDisplayForTrack(Track track)
    {
        foreach (var item in EnumerateArtistTrackItems())
            item.ApplyMetadataFrom(track);
    }

    private void ApplyArtistTracksFilter()
    {
        var items = new List<TrackItemViewModel>();
        for (var i = 0; i < _currentArtistTracks.Count; i++)
        {
            var t = _currentArtistTracks[i];
            var vm = new TrackItemViewModel(t) { RowIndex = i + 1 };
            vm.IsFavorite = _favoriteTrackIds.Contains(t.Id);
            items.Add(vm);
        }

        _artistTracksPager.ResetBuffer(items);
        _artistTracksPager.ResetDisplayed(ArtistTracks);
        NotifyArtistTracksPagingUi();
    }

    private void NotifyArtistTracksPagingUi()
    {
        OnPropertyChanged(nameof(ActiveArtistTracksForPlayback));
        OnPropertyChanged(nameof(HasMoreArtistTracks));
        OnPropertyChanged(nameof(ArtistTracksSummary));
    }

    private string BuildArtistTracksSummary()
    {
        var total = _artistTracksPager.TotalCount;
        if (total <= 0)
            return ArtistTracks.Count > 0
                ? string.Format(_l10n.Get("Library.Artists.TracksSummaryTotal"), ArtistTracks.Count)
                : string.Empty;

        var shown = ArtistTracks.Count;
        if (shown >= total)
            return string.Format(_l10n.Get("Library.Artists.TracksSummaryTotal"), total);

        var parts = new List<string>
        {
            string.Format(_l10n.Get("Library.Artists.TracksSummaryTotal"), total),
            string.Format(_l10n.Get("Library.Artists.TracksSummaryShown"), shown)
        };
        if (_artistTracksPager.HasMore)
            parts.Add(_l10n.Get("Discover.Status.ResultsLoadMore"));
        return string.Join(" · ", parts);
    }

    [RelayCommand]
    private async Task LoadMoreArtistTracksAsync()
    {
        if (!_artistTracksPager.HasMore || IsLoadingMoreArtistTracks)
            return;

        IsLoadingMoreArtistTracks = true;
        try
        {
            await Task.Yield();
            _artistTracksPager.AppendNext(ArtistTracks);
            NotifyArtistTracksPagingUi();
        }
        finally
        {
            IsLoadingMoreArtistTracks = false;
        }
    }

    private IEnumerable<TrackItemViewModel> EnumerateArtistTrackItems() =>
        _artistTracksPager.TotalCount > 0 ? _artistTracksPager.All : ArtistTracks;

    private async Task RefreshFavoriteFlagsAsync()
    {
        _favoriteTrackIds.Clear();
        foreach (var id in await _services.Favorites.GetFavoriteTrackIdsAsync())
            _favoriteTrackIds.Add(id);
        foreach (var item in EnumerateArtistTrackItems())
            item.IsFavorite = _favoriteTrackIds.Contains(item.Track.Id);
    }

    private async Task OnFavoritesChangedAsync()
    {
        await RefreshFavoriteFlagsAsync();
        foreach (var item in EnumerateArtistTrackItems())
            item.IsFavorite = _favoriteTrackIds.Contains(item.Track.Id);
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
}
