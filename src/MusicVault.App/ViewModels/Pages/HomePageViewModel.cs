using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Controls;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.ViewModels.Sidebar;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Services;

namespace MusicVault.App.ViewModels.Pages;

public enum HomeListTab
{
    Recent,
    Frequent,
    Recommend
}

/// <summary>Logo 首页：正在播放（封面 + 歌词 + 播放队列，播控由底部栏负责）。</summary>
public partial class HomePageViewModel : PageViewModelBase
{
    private const int MaxFrequentTracks = 10;
    private const int MinFrequentPlayCount = 2;
    private const int MaxRecommendTracks = 8;

    private readonly PlayerBarViewModel _player;
    private readonly AppServices _services;
    private readonly ILocalizationService _l10n;
    private readonly HashSet<long> _favoriteTrackIds = [];
    private readonly ResultsListPager<QueueRowViewModel> _historyPager = new();
    public PlayerBarViewModel Player => _player;
    public RightSidebarViewModel Lyrics { get; }
    public ObservableCollection<QueueRowViewModel> QueueRows { get; } = [];
    public ObservableCollection<QueueRowViewModel> FrequentRows { get; } = [];
    public ObservableCollection<QueueRowViewModel> RecommendRows { get; } = [];

    [ObservableProperty] private QueueRowViewModel? _selectedQueueRow;

    [ObservableProperty] private bool _isLoadingMoreHistory;

    [ObservableProperty] private HomeListTab _activeListTab = HomeListTab.Recent;

    /// <summary>空首页上选择「查看常听/推荐」后，在无会话内容时仍进入列表主区。</summary>
    [ObservableProperty] private bool _preferLibraryLists;

    private bool _listTabDefaultApplied;

    public override string PageTitle => string.Empty;
    public override string PageSubtitle => string.Empty;
    public override bool ShowStandardPageHeader => false;

    public bool HasCurrentTrack => _player.HasActivePlayback;

    /// <summary>左侧正在播放区（封面+歌词）：首页有内容时始终展示。</summary>
    public bool ShowNowPlayingPane => ShowMainHomeContent;
    public int HistoryTotalCount => _historyPager.TotalCount;

    public bool HasQueueRows => HistoryTotalCount > 0;

    public bool HasMoreHistoryRows => _historyPager.HasMore;

    /// <summary>显示封面/歌词 + 历史分栏（有正在播放的本地曲目时）。</summary>
    public bool ShowNowPlayingSplit => HasCurrentTrack;

    /// <summary>仅显示右侧列表（有历史/常听、无本地当前曲时，例如启动未续播）。</summary>
    public bool ShowHistoryOnlyLayout => !HasCurrentTrack && (HasQueueRows || HasFrequentRows || HasRecommendRows);

    /// <summary>
    /// 空首页：无正在播放且无「最近」历史。
    /// 常听/推荐不挡住空首页（清空最近后可进入）；可通过「查看常听」进入列表。
    /// </summary>
    public bool ShowEmptyOnboarding => !HasCurrentTrack && !HasQueueRows && !PreferLibraryLists;

    /// <summary>首页主内容：与空首页互斥。</summary>
    public bool ShowMainHomeContent => !ShowEmptyOnboarding;

    public bool HasFrequentRows => FrequentRows.Count > 0;
    public bool HasRecommendRows => RecommendRows.Count > 0;
    public bool ShowRecommendTab => HasRecommendRows;
    public bool ShowOpenLibraryListsAction => HasFrequentRows || HasRecommendRows;

    public bool IsRecentTabActive => ActiveListTab == HomeListTab.Recent;
    public bool IsFrequentTabActive => ActiveListTab == HomeListTab.Frequent;
    public bool IsRecommendTabActive => ActiveListTab == HomeListTab.Recommend;

    public IReadOnlyList<string> ListTabSegmentLabels
    {
        get
        {
            var labels = new List<string>
            {
                _l10n.Get("Home.ListTab.Recent"),
                _l10n.Get("Home.ListTab.Frequent")
            };
            if (ShowRecommendTab)
                labels.Add(_l10n.Get("Home.ListTab.Recommend"));
            return labels;
        }
    }

    public int ListTabSegmentIndex
    {
        get => ActiveListTab switch
        {
            HomeListTab.Frequent => 1,
            HomeListTab.Recommend when ShowRecommendTab => 2,
            _ => 0
        };
        set => ActiveListTab = MapSegmentIndexToTab(value);
    }

    public ObservableCollection<QueueRowViewModel> ActiveListRows => ActiveListTab switch
    {
        HomeListTab.Frequent => FrequentRows,
        HomeListTab.Recommend => RecommendRows,
        _ => QueueRows
    };

    public bool HasActiveListRows => ActiveListTab switch
    {
        HomeListTab.Frequent => HasFrequentRows,
        HomeListTab.Recommend => HasRecommendRows,
        _ => HasQueueRows
    };

    public string ActiveLeadingColumnHeader => ActiveListTab switch
    {
        HomeListTab.Frequent => FrequentColPlays,
        HomeListTab.Recommend => RecommendColRecent,
        _ => "#"
    };

    public string ActiveTabSummary => IsRecentTabActive ? QueueHeader : string.Empty;

    public bool ShowLoadMoreButton => IsRecentTabActive && HasMoreHistoryRows;
    public bool ShowClearHistoryButton => IsRecentTabActive;
    public bool ShowFrequentPlayActions => IsFrequentTabActive && HasFrequentRows;
    public bool ShowRecommendPlayActions => IsRecommendTabActive && HasRecommendRows;

    public bool ShowActiveTabEmpty => ShowMainHomeContent && !HasActiveListRows;

    public string ActiveTabEmptyTitle => ActiveListTab switch
    {
        HomeListTab.Frequent => _l10n.Get("Home.Frequent.Empty.Title"),
        HomeListTab.Recommend => _l10n.Get("Home.Recommend.Empty.Title"),
        _ => HistoryEmptyTitle
    };

    public string ActiveTabEmptyDescription => ActiveListTab switch
    {
        HomeListTab.Frequent => _l10n.Get("Home.Frequent.Empty.Description"),
        HomeListTab.Recommend => _l10n.Get("Home.Recommend.Empty.Description"),
        _ => HistoryEmptyDescription
    };

    public string ListTabToolTip => _l10n.Get("Home.ListTab.ToolTip");

    public int HistoryPanelColumn => ShowNowPlayingPane ? 2 : 0;

    public int HistoryPanelColumnSpan => ShowNowPlayingPane ? 1 : 3;

    public bool ShowHistoryLoadMoreSkeleton => IsLoadingMoreHistory && IsRecentTabActive && HasQueueRows;

    public string QueueHeader => BuildQueueHeader();

    partial void OnIsLoadingMoreHistoryChanged(bool value)
    {
        NotifyHistoryPagingUi();
        OnPropertyChanged(nameof(ShowHistoryLoadMoreSkeleton));
    }

    public string QueuePositionText => HasCurrentTrack && _player.Queue.Count > 0
        ? string.Format(_l10n.Get("Home.Queue.Position"), _player.CurrentQueueIndex + 1, _player.Queue.Count)
        : string.Empty;

    public string NowPlayingTitle
    {
        get
        {
            var title = _player.Title;
            var notPlaying = _l10n.Get("Player.NotPlaying");
            return string.IsNullOrWhiteSpace(title) || title == notPlaying ? string.Empty : title;
        }
    }

    public string NowPlayingMeta
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NowPlayingTitle))
                return string.Empty;

            return FormatNowPlayingArtist(_player.Artist, NowPlayingTitle);
        }
    }

    public bool HasNowPlayingMeta => !string.IsNullOrWhiteSpace(NowPlayingMeta);

    private static string FormatNowPlayingArtist(string? artist, string title)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return string.Empty;

        artist = artist.Trim();
        title = title.Trim();

        foreach (var sep in new[] { " - ", " — ", " – " })
        {
            var idx = artist.IndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0)
                continue;

            var suffix = artist[(idx + sep.Length)..].Trim();
            if (suffix.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                artist = artist[..idx].Trim();
                break;
            }
        }

        return artist.Equals(title, StringComparison.OrdinalIgnoreCase) ? string.Empty : artist;
    }

    public string EmptyTitle => _l10n.Get("Home.Empty.Title");
    public string EmptyHint => _l10n.Get("Home.Empty.Hint");
    public string EmptyWorkflowHint => _l10n.Get("Home.Empty.Workflow");
    public string EmptyHintShortcut => _l10n.Get("Home.Empty.Shortcut");
    public string EmptyActionDiscoverText => _l10n.Get("Home.Empty.Action.Discover");
    public string EmptyActionDownloadsText => _l10n.Get("Home.Empty.Action.Downloads");
    public string EmptyActionLibraryText => _l10n.Get("Home.Empty.Action.Library");
    public string EmptyActionSettingsText => _l10n.Get("Home.Empty.Action.Settings");
    public string EmptyActionAddLibraryText => _l10n.Get("Home.Empty.Action.AddLibrary");
    public string EmptyActionAddLibraryToolTip => _l10n.Get("Home.Empty.Action.AddLibrary.ToolTip");
    public string EmptyActionLibraryListsText => _l10n.Get("Home.Empty.Action.LibraryLists");
    public string HistoryEmptyTitle => _l10n.Get("Home.History.Empty.Title");
    public string HistoryEmptyDescription => _l10n.Get("Home.History.Empty.Description");
    public string HistoryLoadMoreText => _l10n.Get("Home.History.LoadMore");
    public string HistoryClearText => _l10n.Get("Home.History.Clear");
    public string HistoryClearToolTip => _l10n.Get("Home.History.Clear.ToolTip");
    public string HistoryColTitle => _l10n.Get("Home.History.Column.Title");
    public string HistoryColArtist => _l10n.Get("Home.History.Column.Artist");
    public string HistoryColAlbum => _l10n.Get("Home.History.Column.Album");
    public string HistoryColDuration => _l10n.Get("Home.History.Column.Duration");
    public string HistoryColFavorite => _l10n.Get("Home.History.Column.Favorite");
    public string FrequentHeader => _l10n.Get("Home.Frequent.Header");
    public string FrequentPlayAllLabel => _l10n.Get("Home.Frequent.PlayAll");
    public string FrequentShuffleLabel => _l10n.Get("Home.Frequent.Shuffle");
    public string FrequentColPlays => _l10n.Get("Home.Frequent.Column.Plays");
    public string RecommendHeader => _l10n.Get("Home.Recommend.Header");
    public string RecommendPlayAllLabel => _l10n.Get("Home.Recommend.PlayAll");
    public string RecommendShuffleLabel => _l10n.Get("Home.Recommend.Shuffle");
    public string RecommendColRecent => _l10n.Get("Home.Recommend.Column.Recent");

    public IReadOnlyList<StepItem> OnboardingSteps { get; private set; } = [];

    public event EventHandler<NavigationSection>? NavigateRequested;

    public HomePageViewModel(
        PlayerBarViewModel player,
        RightSidebarViewModel lyrics,
        AppServices services,
        ILocalizationService l10n)
    {
        _player = player;
        _services = services;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        Lyrics = lyrics;
        RefreshLocalizedStrings();
        _player.PropertyChanged += OnPlayerPropertyChanged;
        _player.TrackStarted += (_, _) =>
        {
            RefreshQueue();
            _ = RefreshFrequentAndRecommendationsAsync();
            NotifyPlaybackProps();
        };
        _player.OnlinePreviewStarted += (_, _) => NotifyPlaybackProps();
        _player.OnlinePreviewEnded += (_, _) => NotifyPlaybackProps();
        _player.PlayHistoryChanged += (_, _) => RefreshQueue();
        _services.Favorites.FavoritesChanged += (_, _) => _ = OnFavoritesChangedAsync();
    }

    public void RefreshLocalizedStrings()
    {
        OnboardingSteps =
        [
            new StepItem(_l10n.Get("Home.Empty.Step.Discover"), StepStatus.Process, 1),
            new StepItem(_l10n.Get("Home.Empty.Step.Downloads"), StepStatus.Wait, 2),
            new StepItem(_l10n.Get("Home.Empty.Step.Library"), StepStatus.Wait, 3, showConnector: false)
        ];
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(EmptyWorkflowHint));
        OnPropertyChanged(nameof(EmptyHintShortcut));
        OnPropertyChanged(nameof(EmptyActionDiscoverText));
        OnPropertyChanged(nameof(EmptyActionDownloadsText));
        OnPropertyChanged(nameof(EmptyActionLibraryText));
        OnPropertyChanged(nameof(EmptyActionSettingsText));
        OnPropertyChanged(nameof(EmptyActionAddLibraryText));
        OnPropertyChanged(nameof(EmptyActionAddLibraryToolTip));
        OnPropertyChanged(nameof(EmptyActionLibraryListsText));
        OnPropertyChanged(nameof(HistoryEmptyTitle));
        OnPropertyChanged(nameof(HistoryEmptyDescription));
        OnPropertyChanged(nameof(HistoryLoadMoreText));
        OnPropertyChanged(nameof(HistoryClearText));
        OnPropertyChanged(nameof(HistoryClearToolTip));
        OnPropertyChanged(nameof(HistoryColTitle));
        OnPropertyChanged(nameof(HistoryColArtist));
        OnPropertyChanged(nameof(HistoryColAlbum));
        OnPropertyChanged(nameof(HistoryColDuration));
        OnPropertyChanged(nameof(HistoryColFavorite));
        OnPropertyChanged(nameof(FrequentHeader));
        OnPropertyChanged(nameof(FrequentPlayAllLabel));
        OnPropertyChanged(nameof(FrequentShuffleLabel));
        OnPropertyChanged(nameof(FrequentColPlays));
        OnPropertyChanged(nameof(RecommendHeader));
        OnPropertyChanged(nameof(RecommendPlayAllLabel));
        OnPropertyChanged(nameof(RecommendShuffleLabel));
        OnPropertyChanged(nameof(RecommendColRecent));
        OnPropertyChanged(nameof(OnboardingSteps));
        OnPropertyChanged(nameof(QueueHeader));
        OnPropertyChanged(nameof(NowPlayingTitle));
        OnPropertyChanged(nameof(ListTabSegmentLabels));
        OnPropertyChanged(nameof(ListTabToolTip));
        NotifyListTabUi();
        RebuildFrequentRowLabels();
        RebuildRecommendRowLabels();
    }

    partial void OnActiveListTabChanged(HomeListTab value) => NotifyListTabUi();
    public async Task InitializeAsync()
    {
        await _player.EnsurePlayHistoryRestoredAsync();
        await RefreshFavoriteFlagsAsync();
        await RefreshFrequentAndRecommendationsAsync();
        NotifyPlaybackProps();
    }

    [RelayCommand]
    private void OpenAllTracks() => NavigateRequested?.Invoke(this, NavigationSection.AllTracks);

    [RelayCommand]
    private void OpenLibraries() => NavigateRequested?.Invoke(this, NavigationSection.Libraries);

    [RelayCommand]
    private void OpenOnlineSources() => NavigateRequested?.Invoke(this, NavigationSection.Search);

    [RelayCommand]
    private void OpenDownloads() => NavigateRequested?.Invoke(this, NavigationSection.Downloads);

    [RelayCommand]
    private void OpenSettings() => NavigateRequested?.Invoke(this, NavigationSection.Settings);

    [RelayCommand]
    private void OpenFullscreen() => _player.OpenNowPlayingFullscreenCommand.Execute(null);

    [RelayCommand]
    private void OpenQueuePanel() => _player.ToggleQueueCommand.Execute(null);

    [RelayCommand]
    private void ClearPlayHistory()
    {
        PreferLibraryLists = false;
        _player.ClearPlayHistory();
        RefreshQueue();
    }

    [RelayCommand]
    private void OpenLibraryLists()
    {
        PreferLibraryLists = true;
        ActiveListTab = HasFrequentRows
            ? HomeListTab.Frequent
            : HasRecommendRows ? HomeListTab.Recommend : HomeListTab.Recent;
        NotifyHomeLayoutProps();
        NotifyListTabUi();
    }

    partial void OnPreferLibraryListsChanged(bool value) => NotifyHomeLayoutProps();

    [RelayCommand]
    private async Task PlayFrequentItemAsync(QueueRowViewModel? row)
    {
        if (row == null)
            return;

        await _player.PlayTrackAsync(row.Track, queueSourceLabel: _l10n.Get("Home.Frequent.Header"));
    }

    [RelayCommand]
    private async Task PlayAllFrequentAsync()
    {
        var tracks = FrequentRows.Select(r => r.Track).ToList();
        if (tracks.Count == 0)
            return;

        await _player.PlayTrackAsync(
            tracks[0],
            tracks,
            queueSourceLabel: _l10n.Get("Home.Frequent.Header"));
    }

    [RelayCommand]
    private async Task ShuffleFrequentAsync()
    {
        var tracks = FrequentRows.Select(r => r.Track).OrderBy(_ => Random.Shared.Next()).ToList();
        if (tracks.Count == 0)
            return;

        _player.IsShuffleOn = true;
        await _player.PlayTrackAsync(
            tracks[0],
            tracks,
            queueSourceLabel: _l10n.Get("Home.Frequent.Header"));
    }

    [RelayCommand]
    private async Task PlayRecommendItemAsync(QueueRowViewModel? row)
    {
        if (row == null)
            return;

        await _player.PlayTrackAsync(row.Track, queueSourceLabel: _l10n.Get("Home.Recommend.Header"));
    }

    [RelayCommand]
    private async Task PlayAllRecommendAsync()
    {
        var tracks = RecommendRows.Select(r => r.Track).ToList();
        if (tracks.Count == 0)
            return;

        await _player.PlayTrackAsync(
            tracks[0],
            tracks,
            queueSourceLabel: _l10n.Get("Home.Recommend.Header"));
    }

    [RelayCommand]
    private async Task ShuffleRecommendAsync()
    {
        var tracks = RecommendRows.Select(r => r.Track).OrderBy(_ => Random.Shared.Next()).ToList();
        if (tracks.Count == 0)
            return;

        _player.IsShuffleOn = true;
        await _player.PlayTrackAsync(
            tracks[0],
            tracks,
            queueSourceLabel: _l10n.Get("Home.Recommend.Header"));
    }

    [RelayCommand]
    private async Task PlayQueueItemAsync(QueueRowViewModel? row)
    {
        if (row == null) return;
        var index = row.SourceQueueIndex >= 0 ? row.SourceQueueIndex : row.DisplayIndex - 1;
        await _player.PlayHistoryItemAtAsync(index);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(QueueRowViewModel? row)
    {
        if (row == null || row.Track.Id <= 0)
            return;

        try
        {
            var added = await _services.Favorites.ToggleAsync(row.Track.Id);
            if (added)
                _favoriteTrackIds.Add(row.Track.Id);
            else
                _favoriteTrackIds.Remove(row.Track.Id);
            row.IsFavorite = added;
        }
        catch
        {
            // 忽略
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerBarViewModel.Title)
            or nameof(PlayerBarViewModel.Artist)
            or nameof(PlayerBarViewModel.IsPlaying)
            or nameof(PlayerBarViewModel.CurrentTrack)
            or nameof(PlayerBarViewModel.HasActivePlayback)
            or nameof(PlayerBarViewModel.IsOnlinePreview)
            or nameof(PlayerBarViewModel.CurrentOnlinePreviewTrack)
            or nameof(PlayerBarViewModel.CoverImage)
            or nameof(PlayerBarViewModel.CoverImageLarge)
            or nameof(PlayerBarViewModel.HasCover)
            or nameof(PlayerBarViewModel.HasCoverLarge)
            or nameof(PlayerBarViewModel.Position)
            or nameof(PlayerBarViewModel.Duration)
            or nameof(PlayerBarViewModel.PositionText)
            or nameof(PlayerBarViewModel.DurationText)
            or nameof(PlayerBarViewModel.QueueSourceLabel))
            NotifyPlaybackProps();
    }

    partial void OnSelectedQueueRowChanged(QueueRowViewModel? value)
    {
        // 单击仅高亮选中行；点播由双击触发，避免与 DoubleTapped 重复播放导致布局闪动。
    }

    private async Task RefreshFrequentAndRecommendationsAsync()
    {
        await RefreshFrequentTracksAsync().ConfigureAwait(true);
        await RefreshRecommendationsAsync().ConfigureAwait(true);
        RefreshQueue();
    }

    private async Task RefreshFrequentTracksAsync()
    {
        var candidates = await _services.Tracks
            .GetTopPlayedAsync(MaxFrequentTracks, MinFrequentPlayCount)
            .ConfigureAwait(true);

        FrequentRows.Clear();
        var index = 1;
        foreach (var track in candidates)
        {
            if (!IsTrackReachable(track))
                continue;

            FrequentRows.Add(new QueueRowViewModel
            {
                DisplayIndex = index++,
                Track = track,
                PlayCountLabel = track.PlayCount.ToString(),
                PlayCountDetailLabel = string.Format(_l10n.Get("Home.Frequent.PlayCountFormat"), track.PlayCount)
            });
        }

        ApplyFavoriteFlags();
        NotifyFrequentUi();
    }

    private async Task RefreshRecommendationsAsync()
    {
        var frequentIds = FrequentRows
            .Select(r => r.Track.Id)
            .Where(id => id > 0)
            .ToHashSet();

        var excludeIds = new HashSet<long>(frequentIds);
        var candidates = await _services.Tracks
            .GetRecentlyPlayedAsync(MaxRecommendTracks, excludeIds)
            .ConfigureAwait(true);

        RecommendRows.Clear();
        var index = 1;
        foreach (var track in candidates)
        {
            if (!IsTrackReachable(track))
                continue;

            RecommendRows.Add(CreateRecommendRow(index++, track, RecommendRowKind.Recent));
            excludeIds.Add(track.Id);
        }

        if (RecommendRows.Count < MaxRecommendTracks && FrequentRows.Count > 0)
        {
            var artists = FrequentRows
                .Select(r => r.Track.Artist)
                .Where(a => !string.IsNullOrWhiteSpace(a));

            var filler = await _services.Tracks
                .GetRecentByArtistsExcludingAsync(
                    artists,
                    excludeIds,
                    MaxRecommendTracks - RecommendRows.Count)
                .ConfigureAwait(true);

            foreach (var track in filler)
            {
                if (!IsTrackReachable(track))
                    continue;

                RecommendRows.Add(CreateRecommendRow(index++, track, RecommendRowKind.SameArtist));
                excludeIds.Add(track.Id);
                if (RecommendRows.Count >= MaxRecommendTracks)
                    break;
            }
        }

        if (RecommendRows.Count < MaxRecommendTracks && FrequentRows.Count > 0)
        {
            foreach (var seed in FrequentRows.Select(r => r.Track))
            {
                if (RecommendRows.Count >= MaxRecommendTracks)
                    break;

                if (string.IsNullOrWhiteSpace(seed.AcoustId))
                    continue;

                var similar = await _services.Tracks
                    .GetSimilarByAcoustIdAsync(
                        seed.AcoustId,
                        excludeIds,
                        MaxRecommendTracks - RecommendRows.Count)
                    .ConfigureAwait(true);

                foreach (var track in similar)
                {
                    if (!IsTrackReachable(track))
                        continue;

                    RecommendRows.Add(CreateRecommendRow(index++, track, RecommendRowKind.SimilarAcoustId));
                    excludeIds.Add(track.Id);
                    if (RecommendRows.Count >= MaxRecommendTracks)
                        break;
                }
            }
        }

        if (RecommendRows.Count < MaxRecommendTracks && FrequentRows.Count > 0)
        {
            foreach (var seed in FrequentRows.Select(r => r.Track))
            {
                if (RecommendRows.Count >= MaxRecommendTracks)
                    break;

                if (string.IsNullOrWhiteSpace(seed.Album))
                    continue;

                var albumTracks = await _services.Tracks
                    .GetSimilarByAlbumAsync(
                        seed.Artist,
                        seed.Album,
                        excludeIds,
                        MaxRecommendTracks - RecommendRows.Count)
                    .ConfigureAwait(true);

                foreach (var track in albumTracks)
                {
                    if (!IsTrackReachable(track))
                        continue;

                    RecommendRows.Add(CreateRecommendRow(index++, track, RecommendRowKind.SameAlbum));
                    excludeIds.Add(track.Id);
                    if (RecommendRows.Count >= MaxRecommendTracks)
                        break;
                }
            }
        }

        ApplyFavoriteFlags();
        NotifyRecommendUi();
    }

    private QueueRowViewModel CreateRecommendRow(int index, Track track, RecommendRowKind kind) =>
        new()
        {
            DisplayIndex = index,
            Track = track,
            RecommendKind = kind,
            PlayCountLabel = FormatRecommendLabel(track, kind)
        };

    private string FormatRecommendLabel(Track track, RecommendRowKind kind) => kind switch
    {
        RecommendRowKind.SameArtist => _l10n.Get("Home.Recommend.SameArtistLabel"),
        RecommendRowKind.SimilarAcoustId => _l10n.Get("Home.Recommend.SimilarLabel"),
        RecommendRowKind.SameAlbum => _l10n.Get("Home.Recommend.SameAlbumLabel"),
        _ => FormatRecommendTimeLabel(track)
    };

    private string FormatRecommendTimeLabel(Track track)
    {
        if (track.LastPlayedAt is not { } playedAt)
            return _l10n.Get("Home.Recommend.RecentLabel");

        var delta = DateTime.UtcNow - playedAt.ToUniversalTime();
        if (delta.TotalMinutes < 1)
            return _l10n.Get("Home.Recommend.JustNow");
        if (delta.TotalHours < 1)
            return string.Format(_l10n.Get("Home.Recommend.MinutesAgo"), Math.Max(1, (int)delta.TotalMinutes));
        if (delta.TotalDays < 1)
            return string.Format(_l10n.Get("Home.Recommend.HoursAgo"), Math.Max(1, (int)delta.TotalHours));
        if (delta.TotalDays < 7)
            return string.Format(_l10n.Get("Home.Recommend.DaysAgo"), Math.Max(1, (int)delta.TotalDays));

        return playedAt.ToLocalTime().ToString("MM-dd");
    }

    private static bool IsTrackReachable(Track track) =>
        NasPathHelper.IsRemoteLibraryPath(track.FilePath) || File.Exists(track.FilePath);

    private void RebuildFrequentRowLabels()
    {
        foreach (var row in FrequentRows)
        {
            var count = row.Track.PlayCount;
            row.PlayCountLabel = count.ToString();
            row.PlayCountDetailLabel = string.Format(_l10n.Get("Home.Frequent.PlayCountFormat"), count);
        }

        OnPropertyChanged(nameof(FrequentRows));
    }

    private void RebuildRecommendRowLabels()
    {
        foreach (var row in RecommendRows)
            row.PlayCountLabel = FormatRecommendLabel(row.Track, row.RecommendKind);

        OnPropertyChanged(nameof(RecommendRows));
    }

    private void NotifyRecommendUi()
    {
        OnPropertyChanged(nameof(HasRecommendRows));
        OnPropertyChanged(nameof(ShowRecommendTab));
        OnPropertyChanged(nameof(RecommendRows));
        OnPropertyChanged(nameof(ShowOpenLibraryListsAction));
        OnPropertyChanged(nameof(ListTabSegmentLabels));
        ApplyListTabDefaultIfNeeded();
        NotifyHomeLayoutProps();
        NotifyListTabUi();
    }

    private void NotifyFrequentUi()
    {
        OnPropertyChanged(nameof(HasFrequentRows));
        OnPropertyChanged(nameof(FrequentRows));
        OnPropertyChanged(nameof(ShowOpenLibraryListsAction));
        ApplyListTabDefaultIfNeeded();
        NotifyHomeLayoutProps();
        NotifyListTabUi();
    }

    private void RefreshQueue()
    {
        var queue = _player.Queue;
        var current = _player.CurrentQueueIndex;
        var rows = new List<QueueRowViewModel>();
        QueueRowViewModel? currentRow = null;
        var displayIndex = 1;
        for (var i = 0; i < queue.Count; i++)
        {
            var track = queue[i];
            var row = new QueueRowViewModel
            {
                DisplayIndex = displayIndex++,
                SourceQueueIndex = i,
                Track = track,
                IsCurrent = i == current
            };
            rows.Add(row);
            if (row.IsCurrent)
                currentRow = row;
        }

        _historyPager.ResetBuffer(rows);
        var start = 0;
        if (currentRow != null)
        {
            var idx = rows.IndexOf(currentRow);
            if (idx >= ResultsListPager<QueueRowViewModel>.DefaultPageSize)
                start = idx / ResultsListPager<QueueRowViewModel>.DefaultPageSize
                      * ResultsListPager<QueueRowViewModel>.DefaultPageSize;
        }

        _historyPager.ResetDisplayedFrom(QueueRows, start);
        SelectedQueueRow = currentRow;

        ApplyFavoriteFlags();
        NotifyHistoryPagingUi();
        OnPropertyChanged(nameof(QueueRows));
        OnPropertyChanged(nameof(QueuePositionText));
        ApplyListTabDefaultIfNeeded();
        NotifyHomeLayoutProps();
        NotifyListTabUi();
        NotifyPlaybackProps();
    }

    private string BuildQueueHeader()
    {
        var label = _l10n.Get("Home.History.Header");
        var total = _historyPager.TotalCount;
        if (total <= 0)
            return label;

        var shown = QueueRows.Count;
        if (shown < total)
            return string.Format(_l10n.Get("Home.Queue.TotalPaging"), label, total, shown);

        return string.Format(_l10n.Get("Home.Queue.TotalOnly"), label, total);
    }

    private void NotifyHistoryPagingUi()
    {
        OnPropertyChanged(nameof(HistoryTotalCount));
        OnPropertyChanged(nameof(HasQueueRows));
        OnPropertyChanged(nameof(HasMoreHistoryRows));
        OnPropertyChanged(nameof(QueueHeader));
        OnPropertyChanged(nameof(ActiveTabSummary));
        OnPropertyChanged(nameof(ShowHistoryLoadMoreSkeleton));
        OnPropertyChanged(nameof(ShowLoadMoreButton));
        NotifyHomeLayoutProps();
    }

    private void NotifyHomeLayoutProps()
    {
        OnPropertyChanged(nameof(ShowNowPlayingPane));
        OnPropertyChanged(nameof(ShowNowPlayingSplit));
        OnPropertyChanged(nameof(ShowHistoryOnlyLayout));
        OnPropertyChanged(nameof(ShowMainHomeContent));
        OnPropertyChanged(nameof(ShowEmptyOnboarding));
        OnPropertyChanged(nameof(ShowOpenLibraryListsAction));
        OnPropertyChanged(nameof(HistoryPanelColumn));
        OnPropertyChanged(nameof(HistoryPanelColumnSpan));
    }

    private static HomeListTab MapSegmentIndexToTab(int index)
    {
        if (index <= 0)
            return HomeListTab.Recent;
        if (index == 1)
            return HomeListTab.Frequent;
        return HomeListTab.Recommend;
    }

    private void ApplyListTabDefaultIfNeeded()
    {
        if (!_listTabDefaultApplied)
        {
            _listTabDefaultApplied = true;
            if (HasQueueRows)
                ActiveListTab = HomeListTab.Recent;
            else if (HasFrequentRows)
                ActiveListTab = HomeListTab.Frequent;
            else if (HasRecommendRows)
                ActiveListTab = HomeListTab.Recommend;
            return;
        }

        if (ActiveListTab == HomeListTab.Recommend && !HasRecommendRows)
        {
            ActiveListTab = HasQueueRows
                ? HomeListTab.Recent
                : HasFrequentRows ? HomeListTab.Frequent : HomeListTab.Recent;
        }
        else if (ActiveListTab == HomeListTab.Frequent && !HasFrequentRows)
        {
            ActiveListTab = HasQueueRows
                ? HomeListTab.Recent
                : HasRecommendRows ? HomeListTab.Recommend : HomeListTab.Recent;
        }
    }

    private void NotifyListTabUi()
    {
        OnPropertyChanged(nameof(ListTabSegmentIndex));
        OnPropertyChanged(nameof(IsRecentTabActive));
        OnPropertyChanged(nameof(IsFrequentTabActive));
        OnPropertyChanged(nameof(IsRecommendTabActive));
        OnPropertyChanged(nameof(ActiveListRows));
        OnPropertyChanged(nameof(HasActiveListRows));
        OnPropertyChanged(nameof(ActiveLeadingColumnHeader));
        OnPropertyChanged(nameof(ActiveTabSummary));
        OnPropertyChanged(nameof(ShowLoadMoreButton));
        OnPropertyChanged(nameof(ShowClearHistoryButton));
        OnPropertyChanged(nameof(ShowFrequentPlayActions));
        OnPropertyChanged(nameof(ShowRecommendPlayActions));
        OnPropertyChanged(nameof(ShowActiveTabEmpty));
        OnPropertyChanged(nameof(ActiveTabEmptyTitle));
        OnPropertyChanged(nameof(ActiveTabEmptyDescription));
        OnPropertyChanged(nameof(ShowHistoryLoadMoreSkeleton));
    }

    [RelayCommand]
    private async Task LoadMoreHistoryRowsAsync()
    {
        if (!_historyPager.HasMore || IsLoadingMoreHistory)
            return;

        IsLoadingMoreHistory = true;
        try
        {
            await Task.Yield();
            _historyPager.AppendNext(QueueRows);
            ApplyFavoriteFlags();
            NotifyHistoryPagingUi();
        }
        finally
        {
            IsLoadingMoreHistory = false;
        }
    }

    private async Task RefreshFavoriteFlagsAsync()
    {
        _favoriteTrackIds.Clear();
        foreach (var id in await _services.Favorites.GetFavoriteTrackIdsAsync())
            _favoriteTrackIds.Add(id);
        ApplyFavoriteFlags();
    }

    private async Task OnFavoritesChangedAsync()
    {
        await RefreshFavoriteFlagsAsync();
    }

    private void ApplyFavoriteFlags()
    {
        var rows = _historyPager.TotalCount > 0 ? _historyPager.All : QueueRows;
        foreach (var row in rows)
            row.IsFavorite = _favoriteTrackIds.Contains(row.Track.Id);

        foreach (var row in FrequentRows)
            row.IsFavorite = _favoriteTrackIds.Contains(row.Track.Id);

        foreach (var row in RecommendRows)
            row.IsFavorite = _favoriteTrackIds.Contains(row.Track.Id);
    }

    private void NotifyPlaybackProps()
    {
        OnPropertyChanged(nameof(HasCurrentTrack));
        NotifyHomeLayoutProps();
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(NowPlayingTitle));
        OnPropertyChanged(nameof(NowPlayingMeta));
        OnPropertyChanged(nameof(HasNowPlayingMeta));
        OnPropertyChanged(nameof(QueueHeader));
        OnPropertyChanged(nameof(QueuePositionText));
        OnPropertyChanged(nameof(ActiveTabSummary));
    }
}
