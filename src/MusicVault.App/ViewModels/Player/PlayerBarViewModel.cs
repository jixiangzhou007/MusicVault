using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Icons;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Audio;
using MusicVault.Services.Download;
using MusicVault.Services.Features;
using MusicVault.Services.Metadata;
using MusicVault.Services.Playback;

namespace MusicVault.App.ViewModels.Player;

/// <summary>底部播放控制栏：播放/暂停/切歌/进度/音量/播放模式/记忆播放位置</summary>
public partial class PlayerBarViewModel : ViewModelBase, IDisposable
{
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly ILocalizationService _l10n;
    private string _notPlayingTitle = string.Empty;
    private string _idleStoppedTitle = string.Empty;
    private string _cannotPlayTitle = string.Empty;
    private AppModalHostViewModel? _appModal;
    private readonly HashSet<string> _declinedAudioRepairPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _repairedAudioPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Track> _queue = [];
    private int _queueIndex = -1;
    private List<Track>? _autoplaySource;
    private int _autoplaySourceIndex = -1;
    private string? _lastHistoryPlaybackKey;
    private bool _isSeeking;
    private bool _startFromBeginning;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private readonly DispatcherTimer _uiProgressTimer;
    private int _playSession;
    private bool _isOnlinePreview;
    private OnlineTrack? _onlinePreviewTrack;
    private OnlinePreviewQueue _onlinePreviewQueue = OnlinePreviewQueue.Empty;
    private string _onlinePlaybackLabelKey = string.Empty;
    private volatile bool _exitSaveDone;
    private bool _suppressPlaybackPersist;
    private CancellationTokenSource? _volumePersistCts;
    private float _volumeBeforeMute = 0.8f;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private double _position;
    [ObservableProperty] private double _duration;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isPlaybackBusy;
    [ObservableProperty] private float _volume = 0.8f;
    [ObservableProperty] private RepeatMode _repeatMode = RepeatMode.Off;
    [ObservableProperty] private bool _isShuffleOn;
    [ObservableProperty] private byte[]? _coverImage;
    [ObservableProperty] private byte[]? _coverImageLarge;
    /// <summary>连续播放来源（歌单/专辑等）；列表区展示的是会话播放历史。</summary>
    [ObservableProperty] private string _queueSourceLabel = string.Empty;

    /// <summary>底部栏等小尺寸 UI。</summary>
    public bool HasCover => CoverImage is { Length: > 0 };

    /// <summary>正在播放页、全屏歌词等大尺寸 UI。</summary>
    public bool HasCoverLarge => CoverImageLarge is { Length: > 0 };

    public bool ShowPlaybackTime => _queueIndex >= 0 || _isOnlinePreview;

    /// <summary>本地曲库队列或发现页在线试听/播放时均可打开全屏。</summary>
    public bool CanOpenNowPlayingFullscreen => CurrentTrack != null || _isOnlinePreview;
    public string FullscreenToolTip => CanOpenNowPlayingFullscreen ? _l10n.Get("Player.Fullscreen") : _l10n.Get("Player.FullscreenBlocked");
    public string TitleAreaToolTip => _l10n.Get("Player.TitleArea.ToolTip");
    public bool CanToggleQueue => _queue.Count > 0 || _isOnlinePreview;
    public bool CanToggleLyrics => CurrentTrack != null || _isOnlinePreview;
    public string QueueToolTip => CanToggleQueue ? _l10n.Get("Player.QueueActive") : _l10n.Get("Player.QueueEmpty");
    public string LyricsToolTip => CanToggleLyrics ? _l10n.Get("Player.LyricsActive") : _l10n.Get("Player.LyricsUnavailable");
    public bool IsDesktopLyricsSupported => OperatingSystem.IsWindows();
    public string DesktopLyricsToolTip => !IsDesktopLyricsSupported
        ? _l10n.Get("Player.DesktopLyricsWindowsOnly")
        : IsDesktopLyricsOpen
            ? _l10n.Get("Player.DesktopLyricsOff")
            : CanToggleLyrics
                ? _l10n.Get("Player.DesktopLyricsOn")
                : _l10n.Get("Player.DesktopLyricsUnavailable");
    public string PreviousToolTip => _l10n.Get("Player.Previous");
    public string NextToolTip => _l10n.Get("Player.Next");
    public string PlayPauseToolTip => IsPlaying ? _l10n.Get("Player.Pause") : _l10n.Get("Player.Play");
    public bool IsEqualizerSupported =>
        PlaybackEngineSupport.IsEqualizerSupported(_services.Player);
    public string EqualizerToolTip => IsEqualizerSupported ? _l10n.Get("Player.Equalizer") : _l10n.Get("Player.EqualizerUnsupported");
    public bool IsMuted => Volume <= 0.001f;
    public string VolumeIconGlyph => IsMuted ? IconGlyphs.VolumeMute : IconGlyphs.Volume;
    public string VolumeToolTip => IsMuted ? _l10n.Get("Player.Unmute") : _l10n.Get("Player.Volume");
    public string VolumeMuteToolTip => IsMuted ? _l10n.Get("Player.Unmute") : _l10n.Get("Player.Mute");
    public string VolumeLabel => _l10n.Get("Player.Volume");

    public bool HasActivePlayback => CurrentTrack != null || _isOnlinePreview;
    public bool CanTogglePlayPause => !IsPlaybackBusy && (_queueIndex >= 0 || _isOnlinePreview);
    public bool CanGoPrevious =>
        !IsPlaybackBusy
        && (HasActiveAutoplaySource
            || (_isOnlinePreview && _onlinePreviewQueue.CanSkip)
            || (!_isOnlinePreview && _queue.Count > 0));
    public bool CanGoNext =>
        !IsPlaybackBusy
        && (HasActiveAutoplaySource
            || (_isOnlinePreview && _onlinePreviewQueue.CanSkip)
            || (!_isOnlinePreview
                && _queue.Count > 0
                && (IsShuffleOn
                    || RepeatMode != RepeatMode.Off
                    || _queueIndex < _queue.Count - 1)));
    public bool CanSeekPlayback => HasActivePlayback && !IsPlaybackBusy;

    /// <summary>发现页在线试听/播放中（无本地队列当前曲）。</summary>
    public bool IsOnlinePreview => _isOnlinePreview;

    /// <summary>发现页在线试听/播放中的曲目（无本地 <see cref="CurrentTrack"/>）。</summary>
    public OnlineTrack? CurrentOnlinePreviewTrack => _isOnlinePreview ? _onlinePreviewTrack : null;

    /// <summary>在线播放时的平台与播放方式（如「MyFreeMP3 · 缓存播放」）。</summary>
    public string PlaybackSourceHint =>
        _isOnlinePreview && _onlinePreviewTrack != null && !string.IsNullOrWhiteSpace(_onlinePlaybackLabelKey)
            ? $"{MusicPlatformCatalog.GetLabel(_onlinePreviewTrack.Platform, _l10n.Get("Player.Online.PlatformFallback"))} · {_l10n.Get(_onlinePlaybackLabelKey)}"
            : string.Empty;

    public bool HasPlaybackSourceHint => !string.IsNullOrWhiteSpace(PlaybackSourceHint);

    [ObservableProperty] private bool _isDesktopLyricsOpen;

    /// <summary>是否显示"从上次位置继续"提示。</summary>
    [ObservableProperty] private bool _showResumePrompt;

    /// <summary>提示文本（含格式化时间）。</summary>
    [ObservableProperty] private string _resumePromptText = string.Empty;

    public string ResumeButtonText => _l10n.Get("Player.Resume");
    public string StartOverButtonText => _l10n.Get("Player.StartOver");

    /// <summary>提示自动消失的 CTS。</summary>
    private CancellationTokenSource? _resumePromptCts;

    public event EventHandler? ToggleLyricsRequested;
    public event EventHandler? ToggleDesktopLyricsRequested;
    public event EventHandler? OpenNowPlayingFullscreenRequested;
    public event EventHandler? ToggleQueueRequested;
    public event EventHandler? ToggleEqualizerRequested;
    public event EventHandler<Track>? TrackStarted;
    public event EventHandler<TrackFilePathChangedEventArgs>? TrackFilePathChanged;
    public event EventHandler<OnlineTrack>? OnlinePreviewStarted;
    public event EventHandler? OnlinePreviewEnded;
    public event EventHandler<double>? PositionUpdated;

    public PlayerBarViewModel(AppServices services, AppSettingsService settings, ILocalizationService l10n)
    {
        _services = services;
        _settings = settings;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        ApplyDefaultsFromSettings();
        RefreshLocalizedStrings();

        _uiProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _uiProgressTimer.Tick += (_, _) => SyncProgressFromPlayer();

        _services.Player.PositionChanged += OnPlayerPositionChanged;
        _services.Player.PlaybackStateChanged += OnPlayerPlaybackStateChanged;
        _services.Player.PlaybackFailed += OnPlayerPlaybackFailed;
        _services.Player.TrackEnded += (_, _) =>
        {
            if (_isOnlinePreview)
            {
                Dispatcher.UIThread.Post(() => _ = OnOnlinePreviewEndedOrAdvanceAsync());
                return;
            }

            Dispatcher.UIThread.Post(() => _ = OnPlaybackEndedAsync());
        };
    }

    public void RefreshLocalizedStrings()
    {
        var prevNotPlaying = _notPlayingTitle;
        var prevIdle = _idleStoppedTitle;
        var prevCannot = _cannotPlayTitle;
        _notPlayingTitle = _l10n.Get("Player.NotPlaying");
        _idleStoppedTitle = _l10n.Get("Player.IdleStopped");
        _cannotPlayTitle = _l10n.Get("Player.CannotPlay");

        if (string.IsNullOrWhiteSpace(Title)
            || Title == prevNotPlaying
            || Title == prevIdle)
            Title = _notPlayingTitle;
        else if (Title == prevCannot)
            Title = _cannotPlayTitle;

        OnPropertyChanged(nameof(FullscreenToolTip));
        OnPropertyChanged(nameof(TitleAreaToolTip));
        OnPropertyChanged(nameof(QueueToolTip));
        OnPropertyChanged(nameof(LyricsToolTip));
        OnPropertyChanged(nameof(DesktopLyricsToolTip));
        OnPropertyChanged(nameof(PreviousToolTip));
        OnPropertyChanged(nameof(NextToolTip));
        OnPropertyChanged(nameof(PlayPauseToolTip));
        OnPropertyChanged(nameof(EqualizerToolTip));
        OnPropertyChanged(nameof(VolumeToolTip));
        OnPropertyChanged(nameof(VolumeLabel));
        OnPropertyChanged(nameof(RepeatToolTip));
        OnPropertyChanged(nameof(ShuffleToolTip));
        OnPropertyChanged(nameof(ProgressAccessibilityName));
        NotifyPlaybackSourceHintChanged();
    }

    public void BindAppModal(AppModalHostViewModel appModal) => _appModal = appModal;

    private async Task OnPlaybackEndedAsync()
    {
        if (await TryAdvanceAutoplaySourceAsync())
            return;

        await NextAsync();
    }

    private async Task OnOnlinePreviewEndedOrAdvanceAsync()
    {
        if (await TryPlayOnlinePreviewNeighborAsync(forward: true, restartCurrentIfNearStart: false))
            return;

        ClearOnlinePreviewState();
        ClearOnlinePreviewQueue();
        IsPlaying = false;
        _uiProgressTimer.Stop();
    }

    private async Task<bool> TryPlayOnlinePreviewNeighborAsync(bool forward, bool restartCurrentIfNearStart)
    {
        if (!_isOnlinePreview || !_onlinePreviewQueue.CanSkip)
            return false;

        if (!forward && restartCurrentIfNearStart && Position > 3)
        {
            Seek(0);
            return true;
        }

        Func<int, int, int>? pickRandom = IsShuffleOn
            ? (cur, count) => ListPlaybackAdvance.PickRandomIndex(cur, count, Random.Shared)
            : null;

        int target;
        if (forward)
        {
            if (!_onlinePreviewQueue.TryGetNext(RepeatMode, IsShuffleOn, pickRandom, out target))
                return false;
        }
        else if (!_onlinePreviewQueue.TryGetPrevious(IsShuffleOn, pickRandom, out target))
        {
            return false;
        }

        var previous = _onlinePreviewTrack;
        var previousIndex = _onlinePreviewQueue.CurrentIndex;
        var track = _onlinePreviewQueue.Tracks[target];
        _onlinePreviewQueue = _onlinePreviewQueue.WithIndex(target);
        await PlayOnlinePreviewQueueItemAsync(track, previous, previousIndex).ConfigureAwait(true);
        return true;
    }

    private async Task PlayOnlinePreviewQueueItemAsync(
        OnlineTrack track,
        OnlineTrack? previous,
        int previousIndex)
    {
        try
        {
            IsPlaybackBusy = true;
            await PlayOnlineStreamAsync(track).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _onlinePreviewQueue = _onlinePreviewQueue.WithIndex(previousIndex);
            if (previous != null)
            {
                BeginOnlinePreview(previous);
                OnlinePreviewStarted?.Invoke(this, previous);
            }

            MessageService.Instance.ShowError(
                string.Format(
                    _l10n.Get("Player.Online.SkipFailed"),
                    OnlinePlaybackStatusText.ResolveReason(_l10n, ex)));
        }
        finally
        {
            IsPlaybackBusy = false;
            NotifyTransportCommandsChanged();
        }
    }

    private void OnPlayerPositionChanged(object? sender, double pos)
    {
        if (_isSeeking) return;
        var session = _playSession;
        Dispatcher.UIThread.Post(() =>
        {
            if (session != _playSession || _isSeeking) return;
            ApplyProgress(pos);
        });
    }

    private void OnPlayerPlaybackStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = _services.Player.IsPlaying;
            if (_services.Player.IsPlaying)
                _uiProgressTimer.Start();
            else
                _uiProgressTimer.Stop();
            SyncProgressFromPlayer();
        });
    }

    private void OnPlayerPlaybackFailed(object? sender, string detail)
    {
        var session = _playSession;
        Dispatcher.UIThread.Post(() =>
        {
            if (session != _playSession)
                return;

            _uiProgressTimer.Stop();
            try { _services.Player.Stop(); } catch { /* ignore */ }
            IsPlaying = false;
            Title = _cannotPlayTitle;
            if (!string.IsNullOrWhiteSpace(detail))
                Artist = detail;
            OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
            OnPropertyChanged(nameof(FullscreenToolTip));

            PlaybackDiagnostics.Log("Play", $"async fail · {detail}");
            ShellStatusService.Instance.Error(
                string.Format(_l10n.Get("Shell.Playback.CannotPlay"), CurrentTrack?.Title ?? Title),
                _l10n.Get("Shell.Category.Playback"));
        });
    }

    private void SyncProgressFromPlayer()
    {
        if ((_queueIndex < 0 && !_isOnlinePreview) || _isSeeking) return;

        var playerDuration = _services.Player.DurationSeconds;
        if (!_services.Player.IsPlaying && playerDuration <= 0.01)
            return;

        ApplyProgress(_services.Player.PositionSeconds);
    }

    private void ApplyProgress(double pos)
    {
        var playerDuration = _services.Player.DurationSeconds;
        var dur = playerDuration > 0.01
            ? playerDuration
            : Duration > 0
                ? Duration
                : 1;

        if (playerDuration > 0.01 && Math.Abs(Duration - dur) > 0.01)
            Duration = dur;

        var clamped = Math.Clamp(pos, 0, dur);
        if (Math.Abs(Position - clamped) < 0.001) return;

        Position = clamped;
        PositionUpdated?.Invoke(this, Position);
        SavePositionIfNeeded(Position);
    }

    public async Task PlayOnlineCachedAsync(
        OnlineTrack online,
        string cachedPath,
        CancellationToken ct = default)
    {
        BeginOnlinePreview(online);
        try
        {
            _autoplaySource = null;
            _autoplaySourceIndex = -1;
            _queueIndex = -1;
            var session = ++_playSession;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    PlayOnlineCachedOnUiAsync(online, cachedPath, session, ct, "Player.Online.Mode.Cached"));
                return;
            }

            await PlayOnlineCachedOnUiAsync(online, cachedPath, session, ct, "Player.Online.Mode.Cached");
            var cachedQuality = ResolveCachedQuality(
                online,
                cachedPath,
                OnlineTrackDownloadQualityResolver.ResolvePreviewQuality(online));
            _services.OnlinePreviewCache.RecordListen(online, cachedQuality, cachedPath);
        }
        catch (Exception ex) when (OnlinePlaybackStatusText.IsCommonPlaybackFailure(ex)
                                   || ex is InvalidOperationException { Message: var msg } && OnlinePlaybackStatusText.IsNoPlayLinkMessage(msg))
        {
            ClearOnlinePreviewState();
            throw new InvalidOperationException(OnlinePlaybackStatusText.ResolveReason(_l10n, ex), ex);
        }
        catch
        {
            ClearOnlinePreviewState();
            throw;
        }
    }

    public async Task PlayOnlineStreamAsync(
        OnlineTrack online,
        AudioQuality? quality = null,
        CancellationToken ct = default,
        IOnlinePreviewProgressReporter? progress = null,
        Action<string>? trace = null)
    {
        BeginOnlinePreview(online);
        try
        {
            _autoplaySource = null;
            _autoplaySourceIndex = -1;
            _queueIndex = -1;
            var session = ++_playSession;

            var resolvedQuality = quality
                ?? OnlineTrackDownloadQualityResolver.ResolvePreviewQuality(online);
            var qualityOrder = BuildOnlineStreamQualityOrder(online, resolvedQuality);

            var cachedPath = _services.OnlinePreviewCache.TryGetBestCachedPath(online, qualityOrder);
            if (cachedPath != null)
            {
                progress?.ReportStage(_l10n.Get("Player.Online.Stage.PlayingLocalCache"));
                progress?.ReportDownloadProgress(1);
                var cachedQuality = ResolveCachedQuality(online, cachedPath, resolvedQuality);
                if (!Dispatcher.UIThread.CheckAccess())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        PlayOnlineCachedOnUiAsync(online, cachedPath, session, ct, "Player.Online.Mode.Cached"));
                }
                else
                {
                    await PlayOnlineCachedOnUiAsync(online, cachedPath, session, ct, "Player.Online.Mode.Cached");
                }

                _services.OnlinePreviewCache.RecordListen(online, cachedQuality, cachedPath);
                return;
            }

            if (GoMusicApiClient.IsConfigured)
            {
                progress?.ReportStage(_l10n.Get("Player.Online.Stage.ConnectingApi"));
                await MusicApiSessionHelper.EnsureReadyAsync(ct).ConfigureAwait(false);
            }

            progress?.ReportStage(_l10n.Get("Player.Online.Stage.ResolvingLink"));
            var downloadProgress = progress != null
                ? new Progress<double>(fraction => progress.ReportDownloadProgress(fraction))
                : null;

            var (filePath, _) = await _services.OnlinePreviewCache.EnsureCachedAsync(
                online,
                resolvedQuality,
                () => ResolveOnlineStreamUrlAsync(online, resolvedQuality, ct, progress, trace),
                ct,
                downloadProgress).ConfigureAwait(false);

            progress?.ReportStage(_l10n.Get("Player.Online.Stage.StartingPlayer"));
            progress?.ReportDownloadProgress(1);

            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    PlayOnlineCachedOnUiAsync(online, filePath, session, ct, "Player.Online.Mode.Stream"));
            }
            else
            {
                await PlayOnlineCachedOnUiAsync(online, filePath, session, ct, "Player.Online.Mode.Stream");
            }

            _services.OnlinePreviewCache.RecordListen(online, resolvedQuality, filePath);
        }
        catch (Exception ex) when (OnlinePlaybackStatusText.IsCommonPlaybackFailure(ex)
                                   || ex is InvalidOperationException { Message: var msg } && OnlinePlaybackStatusText.IsNoPlayLinkMessage(msg))
        {
            ClearOnlinePreviewState();
            throw new InvalidOperationException(OnlinePlaybackStatusText.ResolveReason(_l10n, ex), ex);
        }
        catch
        {
            ClearOnlinePreviewState();
            throw;
        }
    }

    public async Task PlayOnlinePreviewAsync(OnlineTrack online, CancellationToken ct = default)
    {
        var previewQuality = OnlineTrackDownloadQualityResolver.ResolvePreviewQuality(online);
        var cachedPath = _services.OnlinePreviewCache.TryGetCachedPath(online, previewQuality)
                         ?? _services.OnlinePreviewCache.TryGetBestCachedPath(online, [previewQuality]);
        if (cachedPath != null)
        {
            await PlayOnlineCachedAsync(online, cachedPath, ct).ConfigureAwait(false);
            return;
        }

        BeginOnlinePreview(online);
        try
        {
            _autoplaySource = null;
            _autoplaySourceIndex = -1;
            _queueIndex = -1;
            var session = ++_playSession;

            var provider = _services.Search.GetProvider(online.Platform);
            var (filePath, _) = await _services.OnlinePreviewCache.EnsureCachedAsync(
                online,
                previewQuality,
                async () =>
                {
                    var url = await AggregatorPlaybackResolver.ResolveStreamUrlAsync(provider, online, previewQuality, ct)
                        .ConfigureAwait(false);
                    return string.IsNullOrEmpty(url) ? null : url;
                },
                ct).ConfigureAwait(false);

            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    PlayOnlineCachedOnUiAsync(online, filePath, session, ct, "Player.Online.Mode.Preview"));
                return;
            }

            await PlayOnlineCachedOnUiAsync(online, filePath, session, ct, "Player.Online.Mode.Preview");
            _services.OnlinePreviewCache.RecordListen(online, previewQuality, filePath);
        }
        catch
        {
            ClearOnlinePreviewState();
            throw;
        }
    }

    private async Task<string?> ResolveOnlineStreamUrlAsync(
        OnlineTrack online,
        AudioQuality quality,
        CancellationToken ct,
        IOnlinePreviewProgressReporter? progress = null,
        Action<string>? trace = null)
    {
        if (!string.IsNullOrWhiteSpace(online.DirectDownloadUrl))
        {
            progress?.ReportStage(_l10n.Get("Player.Online.Stage.DirectLinkFound"));
            trace?.Invoke("direct-url");
            return online.DirectDownloadUrl;
        }

        progress?.ReportStage(_l10n.Get("Player.Online.Stage.ResolvingLink"));
        var rotated = await GoMusicApiClient.TryResolveDownloadWithSourceRotationAsync(online, quality, ct, trace)
            .ConfigureAwait(false);
        if (rotated != null)
        {
            var usedOriginal = rotated.Value.Track.Platform == online.Platform
                               && string.Equals(rotated.Value.Track.Id, online.Id, StringComparison.Ordinal);
            progress?.ReportStage(_l10n.Get(usedOriginal
                ? "Player.Online.Stage.ResolvingLink"
                : "Player.Online.Stage.FallbackLinkOk"));
            trace?.Invoke($"rotated={rotated.Value.Track.Platform}:{rotated.Value.Track.Id}");
            return rotated.Value.Url;
        }

        var provider = _services.Search.GetProvider(online.Platform);
        if (provider != null)
        {
            progress?.ReportStage(string.Format(
                _l10n.Get("Player.Online.Stage.RequestingPlatformLink"),
                MusicPlatformCatalog.GetLabel(online.Platform, _l10n.Get("Player.Online.PlatformFallback"))));
            var url = await AggregatorPlaybackResolver.ResolveStreamUrlAsync(provider, online, quality, ct)
                .ConfigureAwait(false);
            trace?.Invoke(string.IsNullOrWhiteSpace(url) ? "provider-empty" : "provider-ok");
            return url;
        }

        trace?.Invoke("provider-missing");
        return null;
    }

    private async Task PlayOnlineCachedOnUiAsync(
        OnlineTrack online,
        string filePath,
        int session,
        CancellationToken ct,
        string playbackLabelKey)
    {
        _uiProgressTimer.Stop();
        Position = 0;
        Duration = online.DurationMs > 0 ? online.DurationMs / 1000.0 : 0;
        IsPlaying = false;
        NotifyProgressLayoutChanged();

        var localTrack = new Track
        {
            FilePath = filePath,
            Title = online.Title,
            Artist = online.Artist,
            Album = online.Album ?? string.Empty,
            DurationMs = online.DurationMs,
            FileSizeBytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0
        };

        await Task.Run(() =>
                _services.Player.PlayAsync(localTrack, ct: ct).GetAwaiter().GetResult())
            .ConfigureAwait(true);
        ct.ThrowIfCancellationRequested();
        if (session != _playSession) return;

        var playerDuration = Math.Max(_services.Player.DurationSeconds, Math.Max(Duration, 1));
        Title = online.Title;
        _onlinePlaybackLabelKey = playbackLabelKey;
        Artist = online.Artist;
        NotifyPlaybackSourceHintChanged();
        Duration = playerDuration;
        Position = Math.Clamp(_services.Player.PositionSeconds, 0, playerDuration);
        IsPlaying = _services.Player.IsPlaying;
        PositionUpdated?.Invoke(this, Position);
        NotifyProgressLayoutChanged();

        if (IsPlaying)
            _uiProgressTimer.Start();

        CoverImage = null;
        CoverImageLarge = null;
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(HasCoverLarge));
        if (!string.IsNullOrEmpty(online.CoverUrl))
            _ = LoadCoverFromUrlAsync(online.CoverUrl, session);

        OnlinePreviewStarted?.Invoke(this, online);
    }

    private static IEnumerable<AudioQuality> BuildOnlineStreamQualityOrder(OnlineTrack online, AudioQuality primary)
    {
        yield return primary;
        if (online.SupportsMp3320)
            yield return AudioQuality.High320;
        if (online.SupportsMp3128)
            yield return AudioQuality.Standard128;
        if (online.HasFlac)
            yield return AudioQuality.Flac;
    }

    private AudioQuality ResolveCachedQuality(OnlineTrack online, string cachedPath, AudioQuality fallback)
    {
        foreach (var quality in BuildOnlineStreamQualityOrder(online, fallback))
        {
            var path = _services.OnlinePreviewCache.TryGetCachedPath(online, quality);
            if (string.Equals(path, cachedPath, StringComparison.OrdinalIgnoreCase))
                return quality;
        }

        return fallback;
    }

    public void SetOnlinePreviewQueue(IReadOnlyList<OnlineTrack> tracks, OnlineTrack current)
    {
        _onlinePreviewQueue = OnlinePreviewQueue.FromVisibleList(tracks, current);
        NotifyTransportCommandsChanged();
    }

    public void ClearOnlinePreviewQueue()
    {
        if (_onlinePreviewQueue.IsEmpty)
            return;
        _onlinePreviewQueue = OnlinePreviewQueue.Empty;
        NotifyTransportCommandsChanged();
    }

    /// <summary>将在线曲目排到下一首；在线预览中且队列非空时仅插入不切歌；无播放时立即试听；本地播放时改为单首在线预览队列。</summary>
    public async Task QueueOnlinePlayNext(OnlineTrack track)
    {
        if (track is null)
            return;

        if (_isOnlinePreview)
        {
            if (!_onlinePreviewQueue.IsEmpty)
                _onlinePreviewQueue = _onlinePreviewQueue.WithInsertedAfterCurrent(track);
            else if (_onlinePreviewTrack != null)
                _onlinePreviewQueue = OnlinePreviewQueue.FromVisibleList([_onlinePreviewTrack, track], _onlinePreviewTrack);
            NotifyTransportCommandsChanged();
            return;
        }

        if (!HasActivePlayback)
        {
            await PlayOnlinePreviewAsync(track).ConfigureAwait(true);
            return;
        }

        SetOnlinePreviewQueue([track], track);
        await PlayOnlinePreviewAsync(track).ConfigureAwait(true);
    }

    private void BeginOnlinePreview(OnlineTrack online)
    {
        _isOnlinePreview = true;
        _onlinePreviewTrack = online;
        _onlinePreviewQueue = _onlinePreviewQueue.AlignToCurrent(online);
        OnPropertyChanged(nameof(IsOnlinePreview));
        OnPropertyChanged(nameof(HasActivePlayback));
        OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
        OnPropertyChanged(nameof(FullscreenToolTip));
        NotifySideActionStateChanged();
        OnPropertyChanged(nameof(CurrentOnlinePreviewTrack));
    }

    private void ClearOnlinePreviewState()
    {
        if (!_isOnlinePreview && _onlinePreviewTrack == null)
            return;

        _isOnlinePreview = false;
        _onlinePreviewTrack = null;
        _onlinePlaybackLabelKey = string.Empty;
        NotifyPlaybackSourceHintChanged();
        OnPropertyChanged(nameof(IsOnlinePreview));
        OnPropertyChanged(nameof(HasActivePlayback));
        OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
        OnPropertyChanged(nameof(FullscreenToolTip));
        NotifySideActionStateChanged();
        OnPropertyChanged(nameof(CurrentOnlinePreviewTrack));
        OnlinePreviewEnded?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyPlaybackSourceHintChanged()
    {
        OnPropertyChanged(nameof(PlaybackSourceHint));
        OnPropertyChanged(nameof(HasPlaybackSourceHint));
    }

    private async Task LoadCoverFromUrlAsync(string url, int session)
    {
        try
        {
            var cacheKey = CoverArtCache.BuildUrlKey(url);
            if (CoverArtCache.TryGet(cacheKey, out var cached))
            {
                if (session != _playSession) return;
                ApplyCoverPair(cached.Large, cached.Thumb);
                return;
            }

            var thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(url, null).ConfigureAwait(false);
            if (session != _playSession) return;

            if (CoverArtCache.TryGet(cacheKey, out cached))
                ApplyCoverPair(cached.Large, cached.Thumb);
            else
                ApplyCoverPair(thumb, thumb);
        }
        catch
        {
            /* 封面可选 */
        }
    }

    /// <param name="queue">可选：连续播放来源（如「播放全部」），不写入列表，仅用于播完后自动下一首。</param>
    public async Task PlayTrackAsync(
        Track track,
        IReadOnlyList<Track>? queue = null,
        bool resumePosition = false,
        string? queueSourceLabel = null)
    {
        ClearOnlinePreviewState();
        ClearOnlinePreviewQueue();
        _startFromBeginning = !resumePosition;
        ConfigureAutoplaySource(track, queue, queueSourceLabel);
        AppendToPlayHistory(track);
        await PlayCurrentAsync();
    }

    /// <summary>从播放历史列表中点播（不调整历史顺序；并退出列表连续播放上下文）。</summary>
    public async Task PlayHistoryItemAtAsync(int index, bool resumePosition = false)
    {
        if (index < 0 || index >= _queue.Count) return;
        _autoplaySource = null;
        _autoplaySourceIndex = -1;
        ClearOnlinePreviewState();
        ClearOnlinePreviewQueue();
        _startFromBeginning = !resumePosition;
        _queueIndex = index;
        await PlayCurrentAsync();
    }

    /// <summary>写入元数据前释放本应用对该路径的播放占用，完成后按原状态恢复。</summary>
    public async Task RunWithSuspendedPlaybackForFileWriteAsync(
        string filePath,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            await PlaybackFileWriteCoordinator.RunWithRetryAsync(action, ct).ConfigureAwait(false);
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
                await RunWithSuspendedPlaybackForFileWriteAsync(filePath, action, ct).ConfigureAwait(false));
            return;
        }

        var current = CurrentTrack;
        var loadedPath = _services.Player.LoadedFilePath;
        var needsSuspend = PlaybackPathComparer.AreEqual(loadedPath, filePath)
                           || (current != null && PlaybackPathComparer.AreEqual(current.FilePath, filePath));

        var resumeIndex = _queueIndex;
        var resumeTrack = current;
        if (needsSuspend && resumeTrack == null)
        {
            resumeIndex = FindQueueIndexByPath(filePath);
            if (resumeIndex >= 0)
                resumeTrack = _queue[resumeIndex];
        }
        else if (needsSuspend && resumeTrack != null)
        {
            var idx = FindQueueIndex(resumeTrack);
            if (idx >= 0)
                resumeIndex = idx;
        }

        var wasPlaying = IsPlaying || _services.Player.IsPlaying;
        var resumePos = Position;

        if (needsSuspend)
        {
            _uiProgressTimer.Stop();
            try { await SaveCurrentPositionAsync().ConfigureAwait(true); } catch { /* ignore */ }
            try { _services.Player.Stop(); } catch { /* ignore */ }
            IsPlaying = false;
            await Task.Delay(150, ct).ConfigureAwait(true);
        }

        try
        {
            await PlaybackFileWriteCoordinator.RunWithRetryAsync(action, ct).ConfigureAwait(true);
        }
        finally
        {
            if (needsSuspend && resumeTrack != null && resumeIndex >= 0)
                _ = ResumePlaybackAfterFileWriteAsync(resumeTrack, resumeIndex, wasPlaying, resumePos);
        }
    }

    private async Task ResumePlaybackAfterFileWriteAsync(
        Track track,
        int resumeIndex,
        bool wasPlaying,
        double resumePos)
    {
        if (resumeIndex < 0 || resumeIndex >= _queue.Count)
            return;

        var atIndex = _queue[resumeIndex];
        if (track.Id > 0 && atIndex.Id > 0 && track.Id != atIndex.Id)
            return;
        if (!PlaybackPathComparer.AreEqual(atIndex.FilePath, track.FilePath))
            return;

        _queueIndex = resumeIndex;
        _startFromBeginning = false;
        try
        {
            await PlayCurrentAsync().ConfigureAwait(true);
            if (resumePos > 0.5)
                Seek(resumePos);

            if (!wasPlaying)
            {
                _services.Player.Pause();
                IsPlaying = false;
                _uiProgressTimer.Stop();
            }
        }
        catch
        {
            /* 恢复播放失败时保持已停止状态 */
        }
    }

    private int FindQueueIndexByPath(string filePath)
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            if (PlaybackPathComparer.AreEqual(_queue[i].FilePath, filePath))
                return i;
        }

        return -1;
    }

    private void ConfigureAutoplaySource(Track track, IReadOnlyList<Track>? queue, string? queueSourceLabel)
    {
        if (queue is { Count: > 0 })
        {
            _autoplaySource = queue.ToList();
            _autoplaySourceIndex = FindQueueIndex(track, _autoplaySource);
            if (_autoplaySourceIndex < 0)
                _autoplaySourceIndex = 0;
            if (!string.IsNullOrWhiteSpace(queueSourceLabel))
                QueueSourceLabel = queueSourceLabel.Trim();
            return;
        }

        _autoplaySource = null;
        _autoplaySourceIndex = -1;
        if (!string.IsNullOrWhiteSpace(queueSourceLabel))
            QueueSourceLabel = queueSourceLabel.Trim();
        else if (string.IsNullOrWhiteSpace(QueueSourceLabel))
            QueueSourceLabel = PlaybackConstants.PlayHistoryLabel;
    }

    /// <summary>将曲目写入会话播放历史：不重复条目；单曲循环时不因重播再次入列。</summary>
    private void AppendToPlayHistory(Track track)
    {
        var key = TrackPlaybackKey(track);
        if (RepeatMode == RepeatMode.One
            && key == _lastHistoryPlaybackKey
            && _queueIndex >= 0
            && _queueIndex < _queue.Count)
            return;

        var existing = FindQueueIndex(track);
        if (existing >= 0)
        {
            if (existing != _queue.Count - 1)
            {
                var entry = _queue[existing];
                _queue.RemoveAt(existing);
                _queue.Add(entry);
            }

            _queueIndex = _queue.Count - 1;
        }
        else
        {
            _queue.Add(track);
            _queueIndex = _queue.Count - 1;
            TrimPlayHistoryIfNeeded();
        }

        _lastHistoryPlaybackKey = key;
        SchedulePersistPlayHistory();
        NotifySideActionStateChanged();
    }

    private void TrimPlayHistoryIfNeeded()
    {
        while (_queue.Count > PlaybackConstants.MaxPlayHistoryCount)
        {
            _queue.RemoveAt(0);
            _queueIndex = Math.Max(0, _queueIndex - 1);
        }
    }

    /// <summary>清空播放历史，并停止当前播放、重置底栏展示（不保留当前曲目）。</summary>
    public void ClearPlayHistory()
    {
        _playSession++;
        _queue.Clear();
        _autoplaySource = null;
        _autoplaySourceIndex = -1;
        _queueIndex = -1;
        _lastHistoryPlaybackKey = null;
        if (QueueSourceLabel == PlaybackConstants.PlayHistoryLabel)
            QueueSourceLabel = string.Empty;

        try { _services.Player.Stop(); } catch { /* ignore */ }
        IsPlaying = false;
        Position = 0;
        Duration = 0;
        Title = string.IsNullOrEmpty(_idleStoppedTitle) ? _notPlayingTitle : _idleStoppedTitle;
        Artist = string.Empty;
        ApplyCoverPair(null, null);
        _uiProgressTimer.Stop();
        NotifyProgressLayoutChanged();

        _settings.Current.LastPlayback = null;

        PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
        _ = PersistPlayHistoryAsync();
        NotifySideActionStateChanged();
    }

    /// <summary>从播放历史中移除指定索引（不删文件；默认跳过正在播放的条目）。</summary>
    public int RemovePlayHistoryAtIndices(IEnumerable<int> indices, bool skipCurrent = true)
    {
        var removed = 0;
        foreach (var index in indices.Distinct().OrderByDescending(i => i))
        {
            if (index < 0 || index >= _queue.Count)
                continue;
            if (skipCurrent && index == _queueIndex)
                continue;

            _queue.RemoveAt(index);
            if (index < _queueIndex)
                _queueIndex--;
            removed++;
        }

        if (removed <= 0)
            return 0;

        if (_queueIndex >= _queue.Count)
            _queueIndex = Math.Max(-1, _queue.Count - 1);

        PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
        _ = PersistPlayHistoryAsync();
        NotifySideActionStateChanged();
        return removed;
    }

    /// <summary>曲目从库中删除后：移出播放历史/列表上下文，若正在播放则停止或切到下一首。</summary>
    public void RemoveDeletedTrack(Track track)
    {
        static bool Matches(Track a, Track b) =>
            (a.Id > 0 && b.Id > 0 && a.Id == b.Id) ||
            (!string.IsNullOrEmpty(a.FilePath) &&
             !string.IsNullOrEmpty(b.FilePath) &&
             string.Equals(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));

        var wasCurrent = CurrentTrack != null && Matches(CurrentTrack, track);

        for (var i = _queue.Count - 1; i >= 0; i--)
        {
            if (!Matches(_queue[i], track))
                continue;
            _queue.RemoveAt(i);
            if (i < _queueIndex)
                _queueIndex--;
        }

        if (_autoplaySource is { Count: > 0 })
        {
            var nextSource = _autoplaySource.Where(t => !Matches(t, track)).ToList();
            _autoplaySource = nextSource.Count > 0 ? nextSource : null;
            if (_autoplaySource == null)
                _autoplaySourceIndex = -1;
            else if (_autoplaySourceIndex >= _autoplaySource.Count)
                _autoplaySourceIndex = _autoplaySource.Count - 1;
        }

        if (!wasCurrent)
        {
            if (_queueIndex >= _queue.Count)
                _queueIndex = Math.Max(-1, _queue.Count - 1);
            PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
            _ = PersistPlayHistoryAsync();
            NotifySideActionStateChanged();
            return;
        }

        try { _services.Player.Stop(); } catch { /* ignore */ }
        IsPlaying = false;
        Position = 0;

        if (_queue.Count == 0)
        {
            _queueIndex = -1;
            Title = _idleStoppedTitle;
            Artist = string.Empty;
            ApplyCoverPair(null, null);
            PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
            _ = PersistPlayHistoryAsync();
            return;
        }

        if (_queueIndex >= _queue.Count)
            _queueIndex = _queue.Count - 1;

        PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
        _ = PersistPlayHistoryAsync();
        NotifySideActionStateChanged();
        _ = PlayHistoryItemAtAsync(_queueIndex);
    }

    private CancellationTokenSource? _historyPersistCts;
    private Task? _playHistoryRestoreTask;

    /// <summary>确保播放历史已从数据库恢复（可多次调用，仅执行一次）。</summary>
    public Task EnsurePlayHistoryRestoredAsync() =>
        _playHistoryRestoreTask ??= RestorePlayHistoryAsync();

    private void SchedulePersistPlayHistory()
    {
        if (_queue.Count == 1)
        {
            _ = PersistPlayHistoryAsync();
            return;
        }

        _historyPersistCts?.Cancel();
        _historyPersistCts = new CancellationTokenSource();
        var token = _historyPersistCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                await PersistPlayHistoryAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                /* debounce 取消 */
            }
        });
    }

    private void FlushPlayHistoryPersistence()
    {
        _historyPersistCts?.Cancel();
        try
        {
            _services.PlayHistory.ReplaceSessionSync(BuildPlayHistorySnapshot());
            MirrorPlayHistoryToSettingsSync();
        }
        catch
        {
            /* ignore */
        }
    }

    private void MirrorPlayHistoryToSettingsSync()
    {
        _settings.Current.RecentPlayHistory = BuildPlayHistorySnapshot()
            .Select(e => new PlayHistoryEntryState
            {
                TrackId = e.TrackId,
                FilePath = e.FilePath ?? string.Empty
            })
            .ToList();
        try { _settings.SaveSync(); } catch { /* ignore */ }
    }

    private IReadOnlyList<PlayHistoryEntryRecord> BuildPlayHistorySnapshot() =>
        _queue.Select(t => new PlayHistoryEntryRecord
        {
            TrackId = t.Id,
            FilePath = t.FilePath
        }).ToList();

    private async Task PersistPlayHistoryAsync()
    {
        await _services.PlayHistory
            .ReplaceSessionAsync(BuildPlayHistorySnapshot())
            .ConfigureAwait(false);
        MirrorPlayHistoryToSettingsSync();
    }

    private async Task RestorePlayHistoryAsync()
    {
        var entries = await _services.PlayHistory.GetOrderedAsync().ConfigureAwait(false);
        if (entries.Count == 0)
        {
            var legacy = _settings.Current.RecentPlayHistory;
            if (legacy.Count == 0)
            {
                var legacyIds = _settings.Current.LastPlayback?.QueueTrackIds;
                if (legacyIds is { Count: > 0 })
                {
                    legacy = legacyIds
                        .TakeLast(PlaybackConstants.MaxPlayHistoryCount)
                        .Select(id => new PlayHistoryEntryState { TrackId = id })
                        .ToList();
                }
            }

            if (legacy.Count > 0)
            {
                var records = legacy.Select(e => new PlayHistoryEntryRecord
                {
                    TrackId = e.TrackId,
                    FilePath = e.FilePath
                }).ToList();
                await _services.PlayHistory.MigrateFromLegacyIfEmptyAsync(records).ConfigureAwait(false);
                entries = await _services.PlayHistory.GetOrderedAsync().ConfigureAwait(false);
            }
        }

        if (entries.Count == 0)
            return;

        _queue.Clear();
        foreach (var entry in entries)
        {
            var track = await ResolveHistoryTrackAsync(entry).ConfigureAwait(false);
            if (track != null)
                _queue.Add(track);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _queueIndex = -1;
            if (_queue.Count > 0)
                ApplyLastHistoryTrackDisplayIfIdle();
            PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>有播放历史但未在播时，在首页/底栏展示最近一首（不自动开播）。</summary>
    private void ApplyLastHistoryTrackDisplayIfIdle()
    {
        if (_queue.Count == 0 || _isOnlinePreview || IsPlaying)
            return;

        var track = _queue[^1];
        _queueIndex = _queue.Count - 1;
        _lastHistoryPlaybackKey = TrackPlaybackKey(track);
        QueueSourceLabel = PlaybackConstants.PlayHistoryLabel;

        Title = track.Title;
        Artist = track.Artist;
        var durationSec = track.DurationMs > 0 ? track.DurationMs / 1000.0 : 0;
        Duration = durationSec;
        Position = durationSec > 0
            ? Math.Clamp(track.LastPositionSeconds, 0, durationSec)
            : Math.Max(0, track.LastPositionSeconds);
        IsPlaying = false;
        _uiProgressTimer.Stop();
        NotifyProgressLayoutChanged();
        NotifySideActionStateChanged();
        OnPropertyChanged(nameof(CurrentTrack));
        _ = LoadCoverAsync(track);
    }

    /// <summary>恢复播放历史条目：库内曲目优先；文件暂不可用仍显示在列表中。</summary>
    private async Task<Track?> ResolveHistoryTrackAsync(PlayHistoryEntryRecord entry)
    {
        Track? track = null;
        if (entry.TrackId > 0)
            track = await _services.Tracks.GetByIdAsync(entry.TrackId).ConfigureAwait(false);

        if (track == null && !string.IsNullOrWhiteSpace(entry.FilePath))
        {
            var normalized = TrackPathNormalizer.Normalize(entry.FilePath);
            track = await _services.Tracks.GetByPathAsync(normalized).ConfigureAwait(false);
            if (track == null && !string.Equals(normalized, entry.FilePath, StringComparison.Ordinal))
                track = await _services.Tracks.GetByPathAsync(entry.FilePath).ConfigureAwait(false);
        }

        if (track != null)
            return track;

        if (string.IsNullOrWhiteSpace(entry.FilePath))
            return null;

        return new Track
        {
            Id = entry.TrackId > 0 ? entry.TrackId : 0,
            FilePath = entry.FilePath,
            Title = Path.GetFileNameWithoutExtension(entry.FilePath),
            Artist = string.Empty,
            Album = string.Empty
        };
    }

    public event EventHandler? PlayHistoryChanged;

    private async Task<bool> TryAdvanceAutoplaySourceAsync()
    {
        if (!HasActiveAutoplaySource || _autoplaySource == null)
            return false;

        if (!ListPlaybackAdvance.TryGetNextIndex(
                _autoplaySourceIndex,
                _autoplaySource.Count,
                IsShuffleOn,
                RepeatMode,
                PickRandomAutoplayIndex,
                out var nextIndex))
            return false;

        return await PlayAutoplayIndexAsync(nextIndex);
    }

    private int PickRandomAutoplayIndex(int currentIndex, int listCount) =>
        ListPlaybackAdvance.PickRandomIndex(currentIndex, listCount, Random.Shared);

    private async Task<bool> PlayAutoplayIndexAsync(int index)
    {
        if (_autoplaySource == null || index < 0 || index >= _autoplaySource.Count)
            return false;

        _autoplaySourceIndex = index;
        var next = _autoplaySource[index];
        _startFromBeginning = true;
        AppendToPlayHistory(next);
        await PlayCurrentAsync();
        return true;
    }

    private async Task AdvanceAutoplaySourceAsync(bool forward, bool restartCurrentIfNearStart = true)
    {
        if (!HasActiveAutoplaySource || _autoplaySource == null)
            return;

        await SaveCurrentPositionAsync();

        if (RepeatMode == RepeatMode.One)
        {
            _startFromBeginning = true;
            await PlayCurrentAsync();
            return;
        }

        if (!forward && restartCurrentIfNearStart && Position > 3)
        {
            Seek(0);
            return;
        }

        var moved = forward
            ? ListPlaybackAdvance.TryGetNextIndex(
                _autoplaySourceIndex,
                _autoplaySource.Count,
                IsShuffleOn,
                RepeatMode,
                PickRandomAutoplayIndex,
                out var nextIndex) && await PlayAutoplayIndexAsync(nextIndex)
            : ListPlaybackAdvance.TryGetPreviousIndex(
                _autoplaySourceIndex,
                _autoplaySource.Count,
                IsShuffleOn,
                PickRandomAutoplayIndex,
                out var prevIndex) && await PlayAutoplayIndexAsync(prevIndex);

        _ = moved;
    }

    private static string TrackPlaybackKey(Track track) =>
        track.Id > 0 ? $"id:{track.Id}" : $"path:{track.FilePath}";

    private int FindQueueIndex(Track track, IReadOnlyList<Track>? list = null)
    {
        list ??= _queue;
        for (var i = 0; i < list.Count; i++)
        {
            if (track.Id > 0 && list[i].Id == track.Id)
                return i;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private Task<Track> ResolveNasPlaybackTrackAsync(Track track)
    {
        if (!NasPathHelper.IsRemoteLibraryPath(track.FilePath))
            return Task.FromResult(track);

        var cachedPath = _services.NasTrackCache.TryGetCachedPath(track);
        if (!string.IsNullOrWhiteSpace(cachedPath))
            return Task.FromResult(CloneTrackWithPath(track, cachedPath));

        if (WebDavPathHelper.IsWebDavPath(track.FilePath))
            return ResolveWebDavPlaybackTrackAsync(track);

        if (_settings.Current.NasCacheOnPlay && File.Exists(track.FilePath))
            _services.NasTrackCache.StartBackgroundCache(track);

        return Task.FromResult(track);
    }

    private async Task<Track> ResolveWebDavPlaybackTrackAsync(Track track)
    {
        var cached = await _services.NasTrackCache.EnsureCachedAsync(track).ConfigureAwait(true);
        return string.IsNullOrWhiteSpace(cached) ? track : CloneTrackWithPath(track, cached);
    }

    private static Track CloneTrackWithPath(Track track, string filePath) => new()
    {
        Id = track.Id,
        LibraryId = track.LibraryId,
        FilePath = filePath,
        Title = track.Title,
        Artist = track.Artist,
        Album = track.Album,
        AlbumArtist = track.AlbumArtist,
        TrackNumber = track.TrackNumber,
        Year = track.Year,
        Genre = track.Genre,
        DurationMs = track.DurationMs,
        FileSizeBytes = track.FileSizeBytes,
        AcoustId = track.AcoustId,
        CoverHash = track.CoverHash,
        LyricsPath = track.LyricsPath,
        AddedAt = track.AddedAt,
        LastPlayedAt = track.LastPlayedAt,
        PlayCount = track.PlayCount,
        LastPositionSeconds = track.LastPositionSeconds
    };

    private async Task PlayCurrentAsync()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(PlayCurrentAsync);
            return;
        }

        var track = _queue[_queueIndex];
        var session = ++_playSession;
        IsPlaybackBusy = true;
        try
        {
            var playbackTrack = await ResolveNasPlaybackTrackAsync(track).ConfigureAwait(true);
            if (!File.Exists(playbackTrack.FilePath))
                throw new FileNotFoundException(_l10n.Get("Player.FileNotFound"), playbackTrack.FilePath);

            if (!await TryHandleAudioFormatMismatchBeforePlayAsync(playbackTrack, session).ConfigureAwait(true))
                return;

            if (session != _playSession)
                return;

            var resumeFromSaved = !_startFromBeginning
                                  && track.LastPositionSeconds > 3;
            if (_startFromBeginning)
            {
                track.LastPositionSeconds = 0;
                if (track.Id > 0)
                    _ = _services.Tracks.UpdateLastPositionAsync(track.Id, 0);
            }

            _startFromBeginning = false;

            _uiProgressTimer.Stop();
            Position = 0;
            IsPlaying = false;
            NotifyProgressLayoutChanged();

            await Task.Run(() =>
                    _services.Player.PlayAsync(playbackTrack, resumeFromSavedPosition: resumeFromSaved)
                        .GetAwaiter()
                        .GetResult())
                .ConfigureAwait(true);

            if (session != _playSession) return;

            var playerDuration = Math.Max(_services.Player.DurationSeconds, Math.Max(track.DurationMs / 1000.0, 1));
            var playerPosition = Math.Clamp(_services.Player.PositionSeconds, 0, playerDuration);

            Title = track.Title;
            Artist = track.Artist;
            Duration = playerDuration;
            Position = playerPosition;
            IsPlaying = _services.Player.IsPlaying;
            PositionUpdated?.Invoke(this, playerPosition);
            NotifyProgressLayoutChanged();
            OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
            OnPropertyChanged(nameof(FullscreenToolTip));

            if (resumeFromSaved && track.LastPositionSeconds > 5)
                ShowResumePromptForTrack(track);

            if (track.Id > 0)
            {
                await _services.Tracks.RecordTrackPlayedAsync(track.Id).ConfigureAwait(true);
                track.PlayCount++;
            }

            if (_services.Player.IsPlaying)
                _uiProgressTimer.Start();

            _ = LoadCoverAsync(track);
            TrackStarted?.Invoke(this, track);
            PlaybackDiagnostics.Log(
                "Play",
                ServiceMessageCodec.Format("PlaybackDiag.PlayOk", track.Title, playbackTrack.FilePath)
                + (PlaybackEngineSupport.IsLibVlcEngine(_services.Player)
                    ? " · engine=LibVLC"
                    : ""));
        }
        catch (Exception ex)
        {
            _services.Player.Stop();
            Title = _cannotPlayTitle;
            Artist = $"{track.Title} — {ex.Message}";
            IsPlaying = false;
            Position = 0;
            OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
            OnPropertyChanged(nameof(FullscreenToolTip));
            PlaybackDiagnostics.Log(
                "Play",
                ServiceMessageCodec.Format("PlaybackDiag.PlayFail", track.Title),
                ex);
            ShellStatusService.Instance.Error(
                string.Format(_l10n.Get("Shell.Playback.CannotPlay"), track.Title),
                _l10n.Get("Shell.Category.Playback"),
                ex);
        }
        finally
        {
            IsPlaybackBusy = false;
            NotifyTransportCommandsChanged();
        }
    }

    private async Task<bool> TryHandleAudioFormatMismatchBeforePlayAsync(Track track, int session)
    {
        if (AudioFormatMislabel.IsId3WrappedFlac(track.FilePath))
            return await TryRepairId3WrappedFlacBeforePlayAsync(track, session).ConfigureAwait(true);

        if (AudioFormatMislabel.IsMp3ContentWithFlacExtension(track.FilePath))
            return await TryRenameFlacToMp3BeforePlayAsync(track, session).ConfigureAwait(true);

        return true;
    }

    private async Task<bool> TryRepairId3WrappedFlacBeforePlayAsync(Track track, int session)
    {
        if (_declinedAudioRepairPaths.Contains(track.FilePath)
            || _repairedAudioPaths.Contains(track.FilePath))
            return true;

        var modal = _appModal ?? MusicApiSearchCoordinator.GetAppModal();
        if (modal == null)
            return true;

        var displayName = string.IsNullOrWhiteSpace(track.Title)
            ? Path.GetFileName(track.FilePath)
            : track.Title;
        var confirmed = await modal.ShowId3WrappedFlacRepairConfirmAsync(displayName, _l10n).ConfigureAwait(true);

        if (session != _playSession)
            return false;

        if (!confirmed)
        {
            _declinedAudioRepairPaths.Add(track.FilePath);
            return true;
        }

        var repaired = await ApplyTrackExtensionCorrectionAsync(
            track,
            session,
            _ => _services.TrackExtensionCorrection.RepairId3WrappedFlacAsync(track),
            successMessage: path => string.Format(_l10n.Get("Player.Repair.SuccessFlac"), Path.GetFileName(path))).ConfigureAwait(true);

        if (!repaired)
        {
            _declinedAudioRepairPaths.Add(track.FilePath);
            return false;
        }

        if (AudioFormatMislabel.IsId3WrappedFlac(track.FilePath))
        {
            ShellStatusService.Instance.Error(
                _l10n.Get("Shell.Playback.Id3StillBad"),
                _l10n.Get("Shell.Category.Playback"));
            _declinedAudioRepairPaths.Add(track.FilePath);
            return false;
        }

        return true;
    }

    private async Task<bool> TryRenameFlacToMp3BeforePlayAsync(Track track, int session)
    {
        if (_declinedAudioRepairPaths.Contains(track.FilePath))
            return true;

        var modal = _appModal ?? MusicApiSearchCoordinator.GetAppModal();
        if (modal == null)
            return true;

        var displayName = string.IsNullOrWhiteSpace(track.Title)
            ? Path.GetFileName(track.FilePath)
            : track.Title;
        var confirmed = await modal.ShowMislabeledFlacRenameConfirmAsync(displayName, _l10n).ConfigureAwait(true);

        if (session != _playSession)
            return false;

        if (!confirmed)
        {
            _declinedAudioRepairPaths.Add(track.FilePath);
            return true;
        }

        var repaired = await ApplyTrackExtensionCorrectionAsync(
            track,
            session,
            _ => _services.TrackExtensionCorrection.CorrectFlacToMp3Async(track),
            successMessage: path => string.Format(_l10n.Get("Player.Repair.RenamedMp3"), Path.GetFileName(path))).ConfigureAwait(true);

        if (!repaired)
            _declinedAudioRepairPaths.Add(track.FilePath);

        return repaired;
    }

    private async Task<bool> ApplyTrackExtensionCorrectionAsync(
        Track track,
        int session,
        Func<string, Task<TrackExtensionCorrectionResult>> correct,
        Func<string, string> successMessage)
    {
        _services.Player.Stop();

        try
        {
            var oldPath = track.FilePath;
            var result = await correct(oldPath).ConfigureAwait(true);

            if (session != _playSession)
                return false;

            if (result.Success && result.NewPath != null)
            {
                PropagateFilePathChange(oldPath, result.NewPath, track);
                _declinedAudioRepairPaths.Remove(oldPath);
                _repairedAudioPaths.Add(result.NewPath);
                ShellStatusService.Instance.Info(successMessage(result.NewPath), _l10n.Get("Shell.Category.Playback"));
                return true;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                var detail = ServiceMessageLocalizer.Resolve(_l10n, result.ErrorMessage);
                ShellStatusService.Instance.Error(
                    string.Format(_l10n.Get("Extension.RepairFailed"), detail),
                    _l10n.Get("Shell.Category.Playback"));
            }
        }
        catch (Exception ex)
        {
            ShellStatusService.Instance.Error(
                string.Format(_l10n.Get("Extension.RepairFailed"), ServiceMessageLocalizer.Resolve(_l10n, ex.Message)),
                _l10n.Get("Shell.Category.Playback"),
                ex);
        }

        return false;
    }

    private void PropagateFilePathChange(string oldPath, string newPath, Track sourceTrack)
    {
        foreach (var t in _queue)
        {
            if (string.Equals(t.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                t.FilePath = newPath;
        }

        if (_autoplaySource != null)
        {
            foreach (var t in _autoplaySource)
            {
                if (string.Equals(t.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                    t.FilePath = newPath;
            }
        }

        TrackFilePathChanged?.Invoke(this, new TrackFilePathChangedEventArgs(sourceTrack, oldPath, newPath));
    }

    private async Task LoadCoverAsync(Track track)
    {
        var key = CoverArtCache.BuildKey(track);
        if (CoverArtCache.TryGet(key, out var cached))
        {
            ApplyCoverPair(cached.Large, cached.Thumb);
            return;
        }

        ApplyCoverPair(null, null);

        byte[]? large = await LyricsReader.ReadEmbeddedCoverAsync(track.FilePath);
        byte[]? thumb;
        if (large is { Length: > 0 })
        {
            thumb = CoverImageScaler.ScaleToMaxEdge(large, CoverArtCache.ThumbEdge);
        }
        else
        {
            try
            {
                large = await _services.Metadata.DownloadCoverAsync(
                    track.Artist, track.Album, CoverArtCache.LargeEdge);
                thumb = large is { Length: > 0 }
                    ? CoverImageScaler.ScaleToMaxEdge(large, CoverArtCache.ThumbEdge)
                    : await _services.Metadata.DownloadCoverAsync(
                        track.Artist, track.Album, CoverArtCache.ThumbEdge);
            }
            catch
            {
                large = null;
                thumb = null;
            }
        }

        CoverArtCache.Set(key, thumb, large);
        ApplyCoverPair(large, thumb);
    }

    private void ApplyCoverPair(byte[]? large, byte[]? thumb)
    {
        CoverImageLarge = large;
        CoverImage = thumb ?? large;
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(HasCoverLarge));
    }

    partial void OnCoverImageChanged(byte[]? value) => OnPropertyChanged(nameof(HasCover));

    partial void OnCoverImageLargeChanged(byte[]? value) => OnPropertyChanged(nameof(HasCoverLarge));

    private void SavePositionIfNeeded(double pos)
    {
        if (_queueIndex < 0) return;
        if ((DateTime.UtcNow - _lastSaveTime).TotalSeconds < 5) return;
        _lastSaveTime = DateTime.UtcNow;
        var track = _queue[_queueIndex];
        if (track.Id > 0)
            _ = _services.Tracks.UpdateLastPositionAsync(track.Id, pos);
    }

    private async Task SaveCurrentPositionAsync()
    {
        if (_queueIndex < 0) return;
        var track = _queue[_queueIndex];
        if (track.Id > 0)
            await _services.Tracks.UpdateLastPositionAsync(track.Id, Position);
    }

    public Track? CurrentTrack => _queueIndex >= 0 && _queueIndex < _queue.Count ? _queue[_queueIndex] : null;
    public int CurrentQueueIndex => _queueIndex;
    public IReadOnlyList<Track> Queue => _queue;

    /// <summary>当前连续播放列表中的索引；无列表上下文时为 -1。</summary>
    public int CurrentAutoplaySourceIndex => _autoplaySourceIndex;

    public bool HasActiveAutoplaySource =>
        _autoplaySource is { Count: > 0 } && _autoplaySourceIndex >= 0 && _autoplaySourceIndex < _autoplaySource.Count;

    /// <summary>列表高亮同步：优先使用当前列表索引，否则回退到播放历史索引。</summary>
    public int CurrentListHighlightIndex => HasActiveAutoplaySource ? _autoplaySourceIndex : _queueIndex;

    public async Task SavePlaybackSessionAsync()
    {
        if (_exitSaveDone)
            return;

        var track = CurrentTrack;
        if (track == null)
        {
            _settings.Current.LastPlayback = null;
            await _settings.SaveAsync();
            return;
        }

        await SaveCurrentPositionAsync();

        await _services.PlayHistory.ReplaceSessionAsync(BuildPlayHistorySnapshot()).ConfigureAwait(false);
        MirrorPlayHistoryToSettingsSync();
        _settings.Current.LastPlayback = new PlaybackSessionState
        {
            TrackId = track.Id,
            FilePath = track.FilePath,
            PositionSeconds = Position,
            DurationSeconds = Duration,
            WasPlaying = IsPlaying,
            QueueSourceLabel = QueueSourceLabel
        };
        await _settings.SaveAsync();
    }

    /// <summary>窗口关闭时调用：同步停播 + 写 settings，绝不阻塞 UI 线程等待 async。</summary>
    public void StopAndSaveSessionForExit()
    {
        if (_exitSaveDone)
            return;
        _exitSaveDone = true;

        _playSession++;
        _uiProgressTimer.Stop();
        _services.Player.PositionChanged -= OnPlayerPositionChanged;
        _services.Player.PlaybackStateChanged -= OnPlayerPlaybackStateChanged;
        _services.Player.PlaybackFailed -= OnPlayerPlaybackFailed;

        try { _services.Player.Stop(); } catch { /* ignore */ }
        IsPlaying = false;

        var track = CurrentTrack;
        var pos = Position;
        FlushPlayHistoryPersistence();

        if (track == null)
        {
            _settings.Current.LastPlayback = null;
        }
        else
        {
            _settings.Current.LastPlayback = new PlaybackSessionState
            {
                TrackId = track.Id,
                FilePath = track.FilePath,
                PositionSeconds = pos,
                DurationSeconds = Duration,
                WasPlaying = false,
                QueueSourceLabel = QueueSourceLabel
            };

            if (track.Id > 0)
            {
                var trackId = track.Id;
                _ = Task.Run(async () =>
                {
                    try { await _services.Tracks.UpdateLastPositionAsync(trackId, pos); }
                    catch { /* ignore */ }
                });
            }
        }

        try { _settings.SaveSync(); } catch { /* ignore */ }
    }

    /// <summary>在 NAS/WebDAV 挂载完成后恢复播放历史与上一首的界面/进度，不阻塞解码。</summary>
    public async Task TryRestoreLastSessionAsync()
    {
        await EnsurePlayHistoryRestoredAsync().ConfigureAwait(false);

        if (!_settings.Current.RestorePlaybackOnStartup)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_queue.Count > 0 && _queueIndex < 0)
                    ApplyLastHistoryTrackDisplayIfIdle();
                PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
            });
            return;
        }

        var session = _settings.Current.LastPlayback;
        if (session == null || (session.TrackId <= 0 && string.IsNullOrWhiteSpace(session.FilePath)))
        {
            await Dispatcher.UIThread.InvokeAsync(() => PlayHistoryChanged?.Invoke(this, EventArgs.Empty));
            return;
        }

        Track? track = session.TrackId > 0
            ? await _services.Tracks.GetByIdAsync(session.TrackId).ConfigureAwait(false)
            : null;
        if (track == null && !string.IsNullOrWhiteSpace(session.FilePath))
            track = await _services.Tracks.GetByPathAsync(session.FilePath).ConfigureAwait(false);

        if (track == null || !File.Exists(track.FilePath))
        {
            PlaybackDiagnostics.Log("Restore", ServiceMessageCodec.Key("PlaybackDiag.RestoreSkipInvalid"));
            ShellStatusService.Instance.Warn(
                _l10n.Get("Shell.Playback.RestoreFileMissing"),
                _l10n.Get("Shell.Category.Playback"));
            await Dispatcher.UIThread.InvokeAsync(() => PlayHistoryChanged?.Invoke(this, EventArgs.Empty));
            return;
        }

        track.LastPositionSeconds = Math.Max(0, session.PositionSeconds);

        if (!Dispatcher.UIThread.CheckAccess())
            await Dispatcher.UIThread.InvokeAsync(() => ApplyRestoredTrackUi(track, session));
        else
            ApplyRestoredTrackUi(track, session);
        PlaybackDiagnostics.Log(
            "Restore",
            ServiceMessageCodec.Format(
                "PlaybackDiag.RestoreUi",
                track.Title,
                track.LastPositionSeconds.ToString("F0"),
                _queue.Count));

        _ = LoadCoverAsync(track);

        if (session.WasPlaying)
        {
            Dispatcher.UIThread.Post(
                () => _ = StartRestoredPlaybackAsync(),
                DispatcherPriority.Background);
        }
    }

    private void ApplyRestoredTrackUi(Track track, PlaybackSessionState session)
    {
        ClearOnlinePreviewState();
        ClearOnlinePreviewQueue();

        var idx = FindQueueIndex(track);
        if (idx >= 0)
        {
            _queueIndex = idx;
        }
        else
        {
            _queue.Add(track);
            TrimPlayHistoryIfNeeded();
            _queueIndex = _queue.Count - 1;
            SchedulePersistPlayHistory();
        }

        _lastHistoryPlaybackKey = TrackPlaybackKey(track);
        _startFromBeginning = false;
        QueueSourceLabel = string.IsNullOrWhiteSpace(session.QueueSourceLabel)
            ? PlaybackConstants.PlayHistoryLabel
            : session.QueueSourceLabel;

        try { _services.Player.Stop(); } catch { /* ignore */ }

        Title = track.Title;
        Artist = track.Artist;
        var durationSec = ResolveRestoredDurationSeconds(track, session);
        Duration = durationSec;
        Position = durationSec > 0
            ? Math.Clamp(session.PositionSeconds, 0, durationSec)
            : Math.Max(0, session.PositionSeconds);
        IsPlaying = false;
        _uiProgressTimer.Stop();
        NotifyProgressLayoutChanged();
        PlayHistoryChanged?.Invoke(this, EventArgs.Empty);
        TrackStarted?.Invoke(this, track);
        NotifySideActionStateChanged();
    }

    private void NotifySideActionStateChanged()
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(HasActivePlayback));
        OnPropertyChanged(nameof(CanToggleQueue));
        OnPropertyChanged(nameof(CanToggleLyrics));
        OnPropertyChanged(nameof(CanToggleDesktopLyrics));
        OnPropertyChanged(nameof(QueueToolTip));
        OnPropertyChanged(nameof(LyricsToolTip));
        OnPropertyChanged(nameof(DesktopLyricsToolTip));
        OnPropertyChanged(nameof(CanSeekPlayback));
        ToggleDesktopLyricsCommand.NotifyCanExecuteChanged();
        NotifyTransportCommandsChanged();
    }

    private void NotifyTransportCommandsChanged()
    {
        OnPropertyChanged(nameof(CanTogglePlayPause));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanOpenNowPlayingFullscreen));
        OnPropertyChanged(nameof(FullscreenToolTip));
        OnPropertyChanged(nameof(CanSeekPlayback));
        TogglePlayPauseCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        OpenNowPlayingFullscreenCommand.NotifyCanExecuteChanged();
    }

    private async Task StartRestoredPlaybackAsync()
    {
        if (_queueIndex < 0 || _queue.Count == 0) return;
        await PlayCurrentAsync();
    }

    private static double ResolveRestoredDurationSeconds(Track track, PlaybackSessionState session)
    {
        if (track.DurationMs > 0)
            return track.DurationMs / 1000.0;

        if (session.DurationSeconds > 0)
            return session.DurationSeconds;

        return 0;
    }

    public void Dispose() => StopAndSaveSessionForExit();
}
