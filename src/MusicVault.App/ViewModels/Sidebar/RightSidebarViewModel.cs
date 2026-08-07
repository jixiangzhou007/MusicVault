using System.Collections.ObjectModel;
using System.ComponentModel;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using MusicVault.Core.Enums;

using MusicVault.Core.Models;

using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.ViewModels.Player;
using MusicVault.Services;

using MusicVault.Services.Metadata;
using MusicVault.Services.Search;



namespace MusicVault.App.ViewModels.Sidebar;



/// <summary>右侧边栏：同步滚动歌词 / 元数据编辑 / 播放队列</summary>

public partial class RightSidebarViewModel : ViewModelBase

{

    private readonly AppServices _services;
    private readonly ILocalizationService _l10n = null!;
    private PlayerBarViewModel? _queuePlayer;
    private readonly HashSet<long> _favoriteTrackIds = [];

    private IReadOnlyList<LrcLine> _parsedLines = [];

    private string? _cachedTrackPath;

    private string? _loadingTrackPath;

    private bool _hasSyncedTimeline;

    private double _lastAlignedDuration;

    private CancellationTokenSource? _loadCts;



    [ObservableProperty] private RightPanelMode _mode = RightPanelMode.Closed;

    [ObservableProperty] private bool _isOpen;

    [ObservableProperty] private double _panelWidth = 360;

    [ObservableProperty] private string _lyricsText = string.Empty;

    [ObservableProperty] private int _activeLineIndex = -1;



    public ObservableCollection<LyricsLineViewModel> LyricLines { get; } = [];

    public MetadataEditorViewModel MetadataEditor { get; }

    public ObservableCollection<QueueRowViewModel> QueueRows { get; } = [];

    public bool HasQueueRows => QueueRows.Count > 0;

    public string QueueTitle => _l10n.Get("Sidebar.Queue.Title");

    public string QueueCountText
    {
        get
        {
            var total = QueueRows.Count;
            return total <= 0
                ? _l10n.Get("Sidebar.Queue.Empty")
                : string.Format(_l10n.Get("Sidebar.Queue.Count"), total);
        }
    }

    [ObservableProperty] private bool _isQueueBatchMode;

    public int SelectedQueueCount => QueueRows.Count(r => r.IsSelected);

    public bool HasSelectedQueueItems => SelectedQueueCount > 0;

    public bool CanRemoveSelectedQueueItems => HasSelectedQueueItems;

    public string QueueBatchToolTip => IsQueueBatchMode
        ? _l10n.Get("Sidebar.Queue.ExitBatch")
        : _l10n.Get("Sidebar.Queue.Batch");

    public string QueueClearToolTip => _l10n.Get("Sidebar.Queue.ClearToolTip");
    public string FullscreenToolTip => _l10n.Get("Sidebar.Lyrics.FullscreenToolTip");
    public string SwitchMetadataToolTip => _l10n.Get("Sidebar.Lyrics.SwitchMetadataToolTip");
    public string CloseToolTip => _l10n.Get("Sidebar.CloseToolTip");
    public string QueueClearTitle => _l10n.Get("Sidebar.Queue.ClearTitle");
    public string QueueClearDescription => _l10n.Get("Sidebar.Queue.ClearDescription");
    public string QueueClearConfirm => _l10n.Get("Sidebar.Queue.ClearConfirm");
    public string QueueSelectAllText => _l10n.Get("Sidebar.Queue.SelectAll");
    public string QueueDeselectAllText => _l10n.Get("Sidebar.Queue.DeselectAll");
    public string QueueRemoveSelectedText => _l10n.Get("Sidebar.Queue.RemoveSelected");
    public string QueueEmptyHistoryTitle => _l10n.Get("Sidebar.Queue.EmptyHistoryTitle");
    public string QueueEmptyHistoryDescription => _l10n.Get("Sidebar.Queue.EmptyHistoryDescription");
    public string LyricsDoubleClickToolTip => _l10n.Get("Sidebar.Lyrics.DoubleClickToolTip");

    public string QueueSelectedCountText =>
        string.Format(_l10n.Get("Sidebar.Queue.SelectedCount"), SelectedQueueCount);

    public string SidebarLyricsTitle => _l10n.Get("Sidebar.Lyrics");
    public string SidebarMetadataTitle => _l10n.Get("Sidebar.Metadata");
    public string SidebarQueueTitle => _l10n.Get("Sidebar.Queue.Title");

    public bool IsLyricsMode => Mode == RightPanelMode.Lyrics;

    public bool IsMetadataMode => Mode == RightPanelMode.Metadata;

    public bool IsQueueMode => Mode == RightPanelMode.Queue;

    public bool HasLyricLines => LyricLines.Count > 0;

    public string LyricsEmptyTitle => _l10n.Get("Home.Lyrics.Empty.Title");

    public string LyricsEmptyDescription => _l10n.Get("Home.Lyrics.Empty.Description");



    public event EventHandler? ActiveLineIndexChanged;

    public event EventHandler? EnterFullscreenRequested;

    public event EventHandler? LyricsContentUpdated;



    private Action<double>? _seekPlayback;



    public RightSidebarViewModel(AppServices services, ILocalizationService l10n)

    {
        _l10n = l10n;

        _services = services;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();

        MetadataEditor = new MetadataEditorViewModel(services, l10n);
        LyricsText = _l10n.Get("Sidebar.Lyrics.NoLyrics");

        Mode = RightPanelMode.Closed;

        IsOpen = false;

        _services.Favorites.FavoritesChanged += (_, _) => _ = RefreshFavoriteFlagsAsync();

    }

    public void BindQueuePlayer(PlayerBarViewModel player)
    {
        _queuePlayer = player;
        player.PlayHistoryChanged += (_, _) =>
        {
            if (IsQueueMode)
                RefreshQueueFromPlayer();
        };
    }



    partial void OnModeChanged(RightPanelMode value)
    {
        if (value != RightPanelMode.Queue && IsQueueBatchMode)
            SetQueueBatchMode(false);

        IsOpen = value != RightPanelMode.Closed;

        OnPropertyChanged(nameof(IsLyricsMode));

        OnPropertyChanged(nameof(IsMetadataMode));

        OnPropertyChanged(nameof(IsQueueMode));

    }



    partial void OnIsOpenChanged(bool value)

    {

        if (value && PanelWidth < 220)

            PanelWidth = 320;

    }



    partial void OnActiveLineIndexChanged(int value) =>

        ActiveLineIndexChanged?.Invoke(this, EventArgs.Empty);



    /// <summary>立即打开/关闭歌词面板；关闭不触发 IO。</summary>

    public void ToggleLyrics(Track track, double currentPositionSeconds)

    {

        if (IsOpen && IsLyricsMode)

        {

            CloseLyrics();

            return;

        }



        if (_cachedTrackPath == track.FilePath && LyricLines.Count > 0)

        {

            Mode = RightPanelMode.Lyrics;

            UpdateLyricsPosition(currentPositionSeconds);

            return;

        }



        Mode = RightPanelMode.Lyrics;

        _ = LoadLyricsInBackgroundAsync(track, currentPositionSeconds);

    }



    public void CloseLyrics() => Mode = RightPanelMode.Closed;



    public void BindSeekPlayback(Action<double> seekHandler) => _seekPlayback = seekHandler;



    /// <summary>双击歌词行：跳转播放进度并同步高亮。</summary>

    public void SeekToLyricTime(double timeSeconds)

    {

        if (timeSeconds < 0 || _parsedLines.Count == 0)

            return;



        _seekPlayback?.Invoke(timeSeconds);

        UpdateLyricsPosition(timeSeconds, forceScrollNotify: true, useExactPosition: true);

    }



    /// <summary>根据播放进度更新歌词高亮行</summary>

    public void UpdateLyricsPosition(

        double? positionSeconds = null,

        bool forceScrollNotify = false,

        bool useExactPosition = false)

    {

        if (_parsedLines.Count == 0) return;



        var pos = positionSeconds ?? _services.Player.PositionSeconds;

        if (_hasSyncedTimeline && !useExactPosition)

            pos = LrcParser.AdjustPlaybackPositionForLyrics(pos);



        var idx = LrcParser.GetActiveLineIndex(_parsedLines, pos);

        if (idx == ActiveLineIndex && !forceScrollNotify) return;



        void Apply()

        {

            if (idx != ActiveLineIndex)

            {

                if (ActiveLineIndex >= 0 && ActiveLineIndex < LyricLines.Count)

                    LyricLines[ActiveLineIndex].IsActive = false;

                ActiveLineIndex = idx;

                if (idx >= 0 && idx < LyricLines.Count)

                    LyricLines[idx].IsActive = true;

            }

            else if (forceScrollNotify)

            {

                ActiveLineIndexChanged?.Invoke(this, EventArgs.Empty);

            }

        }



        if (Dispatcher.UIThread.CheckAccess())

            Apply();

        else

            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);

    }



    [RelayCommand]

    private void Close() => Mode = RightPanelMode.Closed;



    [RelayCommand]

    private void ToggleMetadataLyrics() =>

        Mode = Mode == RightPanelMode.Lyrics ? RightPanelMode.Metadata : RightPanelMode.Lyrics;



    [RelayCommand]

    private void EnterFullscreen() => EnterFullscreenRequested?.Invoke(this, EventArgs.Empty);



    /// <summary>预加载歌词（不打开侧栏），供全屏页使用。</summary>

    public Task EnsureLyricsForTrackAsync(Track track, double currentPositionSeconds)

    {

        if (_cachedTrackPath == track.FilePath && LyricLines.Count > 0)

        {

            UpdateLyricsPosition(currentPositionSeconds);

            return Task.CompletedTask;

        }



        return LoadLyricsInBackgroundAsync(track, currentPositionSeconds);

    }



    /// <summary>预加载在线试听歌词（不打开侧栏），供全屏页使用。</summary>

    public Task EnsureLyricsForOnlinePreviewAsync(OnlineTrack online, double currentPositionSeconds)

    {

        var key = BuildOnlineCacheKey(online);

        if (_cachedTrackPath == key && LyricLines.Count > 0)

        {

            UpdateLyricsPosition(currentPositionSeconds);

            return Task.CompletedTask;

        }



        return LoadOnlineLyricsInBackgroundAsync(online, currentPositionSeconds);

    }



    /// <summary>发现页在线试听：打开/关闭歌词面板。</summary>

    public void ToggleLyricsForOnlinePreview(OnlineTrack online, double currentPositionSeconds)

    {

        if (IsOpen && IsLyricsMode)

        {

            CloseLyrics();

            return;

        }



        var key = BuildOnlineCacheKey(online);

        if (_cachedTrackPath == key && LyricLines.Count > 0)

        {

            Mode = RightPanelMode.Lyrics;

            UpdateLyricsPosition(currentPositionSeconds);

            return;

        }



        Mode = RightPanelMode.Lyrics;

        _ = LoadOnlineLyricsInBackgroundAsync(online, currentPositionSeconds);

    }



    /// <summary>在线试听开始时刷新歌词（侧栏/全屏共用）。</summary>

    public void OnOnlinePreviewPlaybackChanged(OnlineTrack online, double currentPositionSeconds)

    {

        var key = BuildOnlineCacheKey(online);

        if (_cachedTrackPath == key && LyricLines.Count > 0)

        {

            UpdateLyricsPosition(currentPositionSeconds);

            return;

        }



        ClearLyricsForTrackChange();

        _ = LoadOnlineLyricsInBackgroundAsync(online, currentPositionSeconds);

    }



    public Task LoadLyricsForTrackAsync(Track track, double currentPositionSeconds = 0)

    {

        ToggleLyrics(track, currentPositionSeconds);

        return Task.CompletedTask;

    }



    public void InvalidateLyricsCache(string? exceptPath = null)

    {

        if (exceptPath != null && _cachedTrackPath == exceptPath) return;

        ClearLyricsForTrackChange();

    }



    /// <summary>元数据保存后刷新歌词缓存；若正在播放该曲则强制重载。</summary>

    public void RefreshLyricsForTrackIfPlaying(Track track, double position, string? playingFilePath)

    {

        if (string.IsNullOrWhiteSpace(track.FilePath))

            return;



        if (!string.Equals(playingFilePath, track.FilePath, StringComparison.OrdinalIgnoreCase))

        {

            InvalidateLyricsCache();

            return;

        }



        ClearLyricsForTrackChange();

        _ = LoadLyricsInBackgroundAsync(track, position);

    }



    /// <summary>切歌时刷新歌词（侧栏/全屏共用，不依赖侧栏是否打开）。</summary>

    public void OnPlaybackTrackChanged(Track track, double currentPositionSeconds)

    {

        if (_cachedTrackPath == track.FilePath && LyricLines.Count > 0)

        {

            UpdateLyricsPosition(currentPositionSeconds);

            return;

        }



        ClearLyricsForTrackChange();

        _ = LoadLyricsInBackgroundAsync(track, currentPositionSeconds);

    }



    /// <summary>音频时长就绪后，为无时间轴歌词重新分配时间轴。</summary>

    public void OnPlaybackDurationUpdated(double durationSeconds)

    {

        if (durationSeconds < 1) return;

        RealignTimelineIfNeeded(durationSeconds);

        UpdateLyricsPosition();

    }



    private void ClearLyricsForTrackChange()

    {

        _loadCts?.Cancel();

        _cachedTrackPath = null;

        _loadingTrackPath = null;

        _lastAlignedDuration = 0;

        _parsedLines = [];

        _hasSyncedTimeline = false;

        LyricLines.Clear();

        ActiveLineIndex = -1;

        OnPropertyChanged(nameof(HasLyricLines));
        NotifyLyricsEmptyStrings();

        LyricsContentUpdated?.Invoke(this, EventArgs.Empty);

    }



    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(QueueTitle));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(QueueBatchToolTip));
        OnPropertyChanged(nameof(QueueClearToolTip));
        OnPropertyChanged(nameof(FullscreenToolTip));
        OnPropertyChanged(nameof(SwitchMetadataToolTip));
        OnPropertyChanged(nameof(CloseToolTip));
        OnPropertyChanged(nameof(QueueClearTitle));
        OnPropertyChanged(nameof(QueueClearDescription));
        OnPropertyChanged(nameof(QueueClearConfirm));
        OnPropertyChanged(nameof(QueueSelectAllText));
        OnPropertyChanged(nameof(QueueDeselectAllText));
        OnPropertyChanged(nameof(QueueRemoveSelectedText));
        OnPropertyChanged(nameof(QueueSelectedCountText));
        OnPropertyChanged(nameof(QueueEmptyHistoryTitle));
        OnPropertyChanged(nameof(QueueEmptyHistoryDescription));
        OnPropertyChanged(nameof(LyricsDoubleClickToolTip));
        OnPropertyChanged(nameof(SidebarLyricsTitle));
        OnPropertyChanged(nameof(SidebarMetadataTitle));
        OnPropertyChanged(nameof(SidebarQueueTitle));
        OnPropertyChanged(nameof(LyricsEmptyTitle));
        OnPropertyChanged(nameof(LyricsEmptyDescription));
        MetadataEditor.RefreshLocalizedStrings();
    }



    private void NotifyLyricsEmptyStrings() => RefreshLocalizedStrings();



    private void ApplyLyricsOnUiThread(Track track, string? raw, double durationSec,

        double currentPositionSeconds, CancellationToken ct)

    {

        void Apply()

        {

            if (ct.IsCancellationRequested || _loadingTrackPath != track.FilePath) return;

            ApplyLyricsContent(track, raw, durationSec);

            RealignTimelineIfNeeded(ResolveDurationSeconds(track, currentPositionSeconds));

            UpdateLyricsPosition(currentPositionSeconds);

        }



        if (Dispatcher.UIThread.CheckAccess())

            Apply();

        else

            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Normal);

    }



    private async Task LoadLyricsInBackgroundAsync(Track track, double currentPositionSeconds)

    {

        _loadCts?.Cancel();

        _loadCts = new CancellationTokenSource();

        var ct = _loadCts.Token;

        var path = track.FilePath;

        _loadingTrackPath = path;



        try

        {

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));



            var raw = await Task.Run(async () =>

            {

                var local = await LyricsReader.ReadAsync(path).ConfigureAwait(false);

                if (LyricsQuality.IsUsableContent(local, track.Title, track.Artist))

                    return local;

                var durationMs = track.DurationMs > 0

                    ? track.DurationMs

                    : (int)(ResolveDurationSeconds(track, currentPositionSeconds) * 1000);

                var online = await OnlineLyricsFetcher.DownloadAsync(

                    [], track.Artist, track.Title, durationMs, timeoutCts.Token).ConfigureAwait(false);

                if (LyricsQuality.IsUsableContent(online, track.Title, track.Artist))

                    return online;

                return null;

            }, timeoutCts.Token).ConfigureAwait(false);



            if (ct.IsCancellationRequested || _loadingTrackPath != path) return;



            var durationSec = ResolveDurationSeconds(track, currentPositionSeconds);

            ApplyLyricsOnUiThread(track, raw, durationSec, currentPositionSeconds, ct);

        }

        catch (OperationCanceledException) { }

        catch (Exception)

        {

            if (ct.IsCancellationRequested || _loadingTrackPath != path) return;

            ApplyLyricsOnUiThread(track, null, track.DurationMs / 1000.0, currentPositionSeconds, ct);

        }

    }



    private async Task LoadOnlineLyricsInBackgroundAsync(OnlineTrack online, double currentPositionSeconds)

    {

        _loadCts?.Cancel();

        _loadCts = new CancellationTokenSource();

        var ct = _loadCts.Token;

        var key = BuildOnlineCacheKey(online);

        _loadingTrackPath = key;



        try

        {

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));



            var raw = await Task.Run(async () =>

            {

                var durationMs = online.DurationMs > 0

                    ? online.DurationMs

                    : (int)(ResolveOnlineDurationSeconds(online, currentPositionSeconds) * 1000);

                var fetched = await OnlineLyricsFetcher.DownloadAsync(

                    [online], online.Artist, online.Title, durationMs, timeoutCts.Token).ConfigureAwait(false);

                if (LyricsQuality.IsUsableContent(fetched, online.Title, online.Artist))

                    return fetched;

                return null;

            }, timeoutCts.Token).ConfigureAwait(false);



            if (ct.IsCancellationRequested || _loadingTrackPath != key) return;



            var durationSec = ResolveOnlineDurationSeconds(online, currentPositionSeconds);

            ApplyOnlineLyricsOnUiThread(key, online.Title, online.Artist, raw, durationSec, currentPositionSeconds, ct);

        }

        catch (OperationCanceledException) { }

        catch (Exception)

        {

            if (ct.IsCancellationRequested || _loadingTrackPath != key) return;

            ApplyOnlineLyricsOnUiThread(

                key,

                online.Title,

                online.Artist,

                null,

                ResolveOnlineDurationSeconds(online, currentPositionSeconds),

                currentPositionSeconds,

                ct);

        }

    }



    private void ApplyOnlineLyricsOnUiThread(

        string cacheKey,

        string title,

        string artist,

        string? raw,

        double durationSec,

        double currentPositionSeconds,

        CancellationToken ct)

    {

        void Apply()

        {

            if (ct.IsCancellationRequested || _loadingTrackPath != cacheKey) return;

            ApplyLyricsContent(cacheKey, title, artist, raw, durationSec);

            RealignTimelineIfNeeded(durationSec);

            UpdateLyricsPosition(currentPositionSeconds);

        }



        if (Dispatcher.UIThread.CheckAccess())

            Apply();

        else

            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Normal);

    }



    private static string BuildOnlineCacheKey(OnlineTrack online) =>

        $"online:{online.Platform}:{online.Id}";



    private double ResolveOnlineDurationSeconds(OnlineTrack? online, double currentPositionSeconds)

    {

        var fromPlayer = _services.Player.DurationSeconds;

        var fromMeta = online?.DurationMs > 0 ? online.DurationMs / 1000.0 : 0;

        var resolved = Math.Max(Math.Max(fromPlayer, fromMeta), currentPositionSeconds * 2);

        return resolved < 1 ? 180 : resolved;

    }



    /// <summary>切歌后用真实音频时长重新对齐无时间轴歌词。</summary>

    public void RealignTimelineIfNeeded(double durationSeconds)

    {

        if (_parsedLines.Count == 0 || _hasSyncedTimeline) return;

        if (durationSeconds < 1) return;



        if (_lastAlignedDuration > 0 && Math.Abs(durationSeconds - _lastAlignedDuration) < 3)

            return;



        _lastAlignedDuration = durationSeconds;

        _parsedLines = LrcParser.EnsureSyncedTimeline(_parsedLines, durationSeconds);

        _hasSyncedTimeline = LrcParser.HasSyncedTimestamps(_parsedLines);

        LyricLines.Clear();

        foreach (var line in _parsedLines)

            LyricLines.Add(new LyricsLineViewModel(line));

        ActiveLineIndex = -1;

        LyricsContentUpdated?.Invoke(this, EventArgs.Empty);

    }



    private double ResolveDurationSeconds(Track track, double currentPositionSeconds)

    {

        var fromPlayer = _services.Player.DurationSeconds;

        var fromMeta = track.DurationMs > 0 ? track.DurationMs / 1000.0 : 0;

        var resolved = Math.Max(Math.Max(fromPlayer, fromMeta), currentPositionSeconds * 2);

        return resolved < 1 ? 180 : resolved;

    }



    private void ApplyLyricsContent(Track track, string? raw, double durationSec) =>

        ApplyLyricsContent(track.FilePath, track.Title, track.Artist, raw, durationSec);



    private void ApplyLyricsContent(string cacheKey, string title, string artist, string? raw, double durationSec)

    {

        _cachedTrackPath = cacheKey;

        LyricsText = raw ?? _l10n.Get("Sidebar.Lyrics.NoLyrics");

        var parsed = raw != null ? LrcParser.Parse(raw) : [];

        parsed = LrcParser.FilterMetadataLines(parsed, title, artist);
        parsed = LrcParser.CollapseDuplicateTextLines(parsed);

        _parsedLines = LrcParser.EnsureSyncedTimeline(parsed, durationSec);

        _hasSyncedTimeline = LrcParser.HasSyncedTimestamps(_parsedLines);

        _lastAlignedDuration = durationSec;



        LyricLines.Clear();

        foreach (var line in _parsedLines)

            LyricLines.Add(new LyricsLineViewModel(line));



        ActiveLineIndex = -1;

        OnPropertyChanged(nameof(HasLyricLines));
        NotifyLyricsEmptyStrings();

        LyricsContentUpdated?.Invoke(this, EventArgs.Empty);

    }



    public void ToggleQueue(IReadOnlyList<Track> tracks)
    {
        if (IsOpen && IsQueueMode)
        {
            Close();
            return;
        }

        LoadQueue(tracks);
    }

    public void LoadQueue(IReadOnlyList<Track> tracks)
    {
        QueueRows.Clear();
        var current = _queuePlayer?.CurrentQueueIndex ?? -1;
        for (var i = 0; i < tracks.Count; i++)
        {
            var row = new QueueRowViewModel
            {
                DisplayIndex = i + 1,
                Track = tracks[i],
                IsCurrent = i == current,
                IsBatchMode = IsQueueBatchMode
            };
            row.PropertyChanged += OnQueueRowPropertyChanged;
            QueueRows.Add(row);
        }

        _ = ApplyFavoriteFlagsAsync();
        NotifyQueueUi();
        Mode = RightPanelMode.Queue;
    }

    private void RefreshQueueFromPlayer()
    {
        if (_queuePlayer == null)
            return;

        LoadQueue(_queuePlayer.Queue);
    }

    [RelayCommand]
    private async Task PlayQueueItemAsync(QueueRowViewModel? row)
    {
        if (row == null || _queuePlayer == null)
            return;

        await _queuePlayer.PlayHistoryItemAtAsync(row.DisplayIndex - 1);
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

    private async Task RefreshFavoriteFlagsAsync()
    {
        _favoriteTrackIds.Clear();
        foreach (var id in await _services.Favorites.GetFavoriteTrackIdsAsync())
            _favoriteTrackIds.Add(id);
        ApplyFavoriteFlags();
    }

    private async Task ApplyFavoriteFlagsAsync()
    {
        if (_favoriteTrackIds.Count == 0)
            await RefreshFavoriteFlagsAsync();
        else
            ApplyFavoriteFlags();
    }

    private void ApplyFavoriteFlags()
    {
        foreach (var row in QueueRows)
            row.IsFavorite = _favoriteTrackIds.Contains(row.Track.Id);
    }

    [RelayCommand]
    private void ToggleQueueBatchMode() => SetQueueBatchMode(!IsQueueBatchMode);

    [RelayCommand]
    private void ClearQueue()
    {
        _queuePlayer?.ClearPlayHistory();
        SetQueueBatchMode(false);
        RefreshQueueFromPlayer();
    }

    [RelayCommand]
    private void SelectAllQueueItems()
    {
        foreach (var row in QueueRows)
            row.IsSelected = true;
        NotifyQueueSelectionUi();
    }

    [RelayCommand]
    private void DeselectAllQueueItems()
    {
        foreach (var row in QueueRows)
            row.IsSelected = false;
        NotifyQueueSelectionUi();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedQueueItems))]
    private void RemoveSelectedQueueItems()
    {
        if (_queuePlayer == null || !HasSelectedQueueItems)
            return;

        var indices = QueueRows
            .Where(r => r.IsSelected)
            .Select(r => r.DisplayIndex - 1)
            .ToList();
        _queuePlayer.RemovePlayHistoryAtIndices(indices);
        SetQueueBatchMode(false);
        RefreshQueueFromPlayer();
    }

    private void OnQueueRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueueRowViewModel.IsSelected))
            NotifyQueueSelectionUi();
    }

    private void SetQueueBatchMode(bool enabled)
    {
        if (IsQueueBatchMode == enabled)
            return;

        IsQueueBatchMode = enabled;
        foreach (var row in QueueRows)
        {
            row.IsBatchMode = enabled;
            if (!enabled)
                row.IsSelected = false;
        }

        OnPropertyChanged(nameof(QueueBatchToolTip));
        NotifyQueueSelectionUi();
    }

    private void NotifyQueueSelectionUi()
    {
        OnPropertyChanged(nameof(SelectedQueueCount));
        OnPropertyChanged(nameof(QueueSelectedCountText));
        OnPropertyChanged(nameof(HasSelectedQueueItems));
        OnPropertyChanged(nameof(CanRemoveSelectedQueueItems));
        RemoveSelectedQueueItemsCommand.NotifyCanExecuteChanged();
    }

    private void NotifyQueueUi()
    {
        OnPropertyChanged(nameof(HasQueueRows));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(QueueBatchToolTip));
        NotifyQueueSelectionUi();
    }

}


