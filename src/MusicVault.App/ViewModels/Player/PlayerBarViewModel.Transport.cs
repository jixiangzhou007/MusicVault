using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Player;

/// <summary>播放栏传输控制：播放/暂停/切歌/Seek/循环随机/静音/恢复提示动作。</summary>
public partial class PlayerBarViewModel
{
    [RelayCommand(CanExecute = nameof(CanTogglePlayPause))]
    private async Task TogglePlayPauseAsync()
    {
        if (_queueIndex < 0 && !_isOnlinePreview) return;
        if (_services.Player.IsPlaying)
        {
            _services.Player.Pause();
            _uiProgressTimer.Stop();
        }
        else
        {
            if (_queueIndex >= 0 && !_isOnlinePreview && _services.Player.DurationSeconds <= 0.01)
            {
                await PlayCurrentAsync();
                return;
            }

            _services.Player.Resume();
            if (_queueIndex >= 0 || _isOnlinePreview)
                _uiProgressTimer.Start();
        }
        IsPlaying = _services.Player.IsPlaying;
        SyncProgressFromPlayer();
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (HasActiveAutoplaySource)
        {
            await AdvanceAutoplaySourceAsync(forward: true, restartCurrentIfNearStart: false);
            return;
        }

        if (_isOnlinePreview)
            return;

        if (_queue.Count == 0) return;
        await SaveCurrentPositionAsync();

        if (RepeatMode == RepeatMode.One)
        {
            _startFromBeginning = true;
            await PlayCurrentAsync();
            return;
        }

        if (IsShuffleOn)
            _queueIndex = PickRandomNextIndex();
        else if (_queueIndex < _queue.Count - 1)
            _queueIndex++;
        else if (RepeatMode == RepeatMode.All)
            _queueIndex = 0;
        else
            return;

        _startFromBeginning = true;
        await PlayCurrentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousAsync()
    {
        if (HasActiveAutoplaySource)
        {
            await AdvanceAutoplaySourceAsync(forward: false);
            return;
        }

        if (_isOnlinePreview)
            return;

        if (_queue.Count == 0) return;
        await SaveCurrentPositionAsync();
        if (Position > 3)
        {
            Seek(0);
            return;
        }
        _queueIndex = _queueIndex <= 0 ? _queue.Count - 1 : _queueIndex - 1;
        _startFromBeginning = true;
        await PlayCurrentAsync();
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };
    }

    /// <summary>未循环：E8EE 白；列表循环：E8EE 蓝；单曲循环：E8ED 蓝。</summary>
    public string RepeatIconGlyph => RepeatMode == RepeatMode.One ? "\uE8ED" : "\uE8EE";

    public bool IsRepeatActive => RepeatMode != RepeatMode.Off;

    public string RepeatToolTip => RepeatMode switch
    {
        RepeatMode.All => _l10n.Get("Player.RepeatAll"),
        RepeatMode.One => _l10n.Get("Player.RepeatOne"),
        _ => _l10n.Get("Player.RepeatOff")
    };

    [RelayCommand]
    private void ToggleShuffle() => IsShuffleOn = !IsShuffleOn;

    /// <summary>随机：E8B1 蓝；顺序：E8CB 白。</summary>
    public string ShuffleIconGlyph => IsShuffleOn ? "\uE8B1" : "\uE8CB";

    public string ShuffleToolTip => IsShuffleOn ? _l10n.Get("Player.ShuffleOn") : _l10n.Get("Player.ShuffleOff");

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        OnPropertyChanged(nameof(RepeatIconGlyph));
        OnPropertyChanged(nameof(IsRepeatActive));
        OnPropertyChanged(nameof(RepeatToolTip));
        NotifyTransportCommandsChanged();
        PersistPlaybackPreferences();
    }

    partial void OnIsShuffleOnChanged(bool value)
    {
        OnPropertyChanged(nameof(ShuffleToolTip));
        OnPropertyChanged(nameof(ShuffleIconGlyph));
        NotifyTransportCommandsChanged();
        PersistPlaybackPreferences();
    }

    private int PickRandomNextIndex()
    {
        if (_queue.Count <= 1) return 0;
        var next = _queueIndex;
        while (next == _queueIndex)
            next = Random.Shared.Next(_queue.Count);
        return next;
    }

    /// <summary>兼容列表同步等仍使用 PlayMode 的调用方。</summary>
    public PlayMode PlayMode => IsShuffleOn
        ? PlayMode.Shuffle
        : RepeatMode switch
        {
            RepeatMode.One => PlayMode.RepeatOne,
            RepeatMode.All => PlayMode.RepeatAll,
            _ => PlayMode.Sequential
        };

    [RelayCommand]
    private void ToggleLyrics()
    {
        if (CanToggleLyrics)
            ToggleLyricsRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool CanToggleDesktopLyrics => IsDesktopLyricsOpen || CanToggleLyrics;

    [RelayCommand(CanExecute = nameof(CanToggleDesktopLyrics))]
    private void ToggleDesktopLyrics()
    {
        if (!IsDesktopLyricsSupported)
            return;
        ToggleDesktopLyricsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetDesktopLyricsOpen(bool isOpen)
    {
        if (IsDesktopLyricsOpen == isOpen)
            return;
        IsDesktopLyricsOpen = isOpen;
        OnPropertyChanged(nameof(DesktopLyricsToolTip));
        OnPropertyChanged(nameof(CanToggleDesktopLyrics));
        ToggleDesktopLyricsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenNowPlayingFullscreen))]
    private void OpenNowPlayingFullscreen()
    {
        OpenNowPlayingFullscreenRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleQueue()
    {
        if (CanToggleQueue)
            ToggleQueueRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(IsEqualizerSupported))]
    private void OpenEqualizer() => ToggleEqualizerRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>关闭恢复提示（播放已从保存位置继续）。</summary>
    [RelayCommand]
    private void ResumeFromPosition()
    {
        ShowResumePrompt = false;
        _resumePromptCts?.Cancel();
    }

    /// <summary>跳到曲目开头并清除保存位置。</summary>
    [RelayCommand]
    private void StartFromBeginning()
    {
        ShowResumePrompt = false;
        _resumePromptCts?.Cancel();
        _startFromBeginning = true;

        if (CurrentTrack is { } track)
        {
            track.LastPositionSeconds = 0;
            if (track.Id > 0)
                _ = _services.Tracks.UpdateLastPositionAsync(track.Id, 0);
        }

        if (_services.Player.DurationSeconds > 0.01 || _isOnlinePreview)
        {
            Seek(0);
            _startFromBeginning = false;
            return;
        }

        if (_queueIndex >= 0)
            _ = PlayCurrentAsync();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        var (newVolume, remembered) = PlayerVolumeMute.Toggle(IsMuted, Volume, _volumeBeforeMute);
        _volumeBeforeMute = remembered;
        Volume = newVolume;
    }

    /// <summary>播放开始后显示"已从 X:XX 恢复"提示（5 秒自动消失）。</summary>
    private void ShowResumePromptForTrack(Track track)
    {
        var savedSeconds = track.LastPositionSeconds;
        if (savedSeconds <= 5 || _startFromBeginning)
            return;

        var ts = TimeSpan.FromSeconds(savedSeconds);
        var timeStr = ts.Hours > 0
            ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
        ResumePromptText = string.Format(_l10n.Get("Player.ResumePrompt"), timeStr);
        ShowResumePrompt = true;

        _resumePromptCts?.Cancel();
        _resumePromptCts = new CancellationTokenSource();
        var cts = _resumePromptCts;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(5000, cts.Token); }
            catch (OperationCanceledException) { return; }
            await Dispatcher.UIThread.InvokeAsync(() => ShowResumePrompt = false);
        });
    }

    public int VolumePercent => (int)Math.Round(Volume * 100);

    partial void OnVolumeChanged(float value)
    {
        _services.Player.Volume = value;
        if (value > 0.001f)
            _volumeBeforeMute = value;
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(VolumeIconGlyph));
        OnPropertyChanged(nameof(VolumeToolTip));
        OnPropertyChanged(nameof(VolumeMuteToolTip));
        SchedulePersistVolume();
    }

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseToolTip));

    partial void OnIsPlaybackBusyChanged(bool value) => NotifyTransportCommandsChanged();

    /// <summary>从偏好设置应用默认音量、循环与随机（保存设置后由主窗口调用）。</summary>
    public void ApplyDefaultsFromSettings()
    {
        var s = _settings.Current;
        _suppressPlaybackPersist = true;
        try
        {
            Volume = Math.Clamp(s.DefaultVolume > 0 ? s.DefaultVolume : 0.8f, 0f, 1f);
            RepeatMode = s.DefaultRepeatModeIndex switch
            {
                1 => RepeatMode.All,
                2 => RepeatMode.One,
                _ => RepeatMode.Off
            };
            IsShuffleOn = s.DefaultShuffleOn;
            _services.Player.Volume = Volume;
        }
        finally
        {
            _suppressPlaybackPersist = false;
        }
    }

    private void SchedulePersistVolume()
    {
        if (_suppressPlaybackPersist) return;
        _volumePersistCts?.Cancel();
        _volumePersistCts?.Dispose();
        _volumePersistCts = new CancellationTokenSource();
        var token = _volumePersistCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token).ConfigureAwait(false);
                PersistPlaybackPreferences();
            }
            catch (OperationCanceledException) { /* debounced */ }
        }, token);
    }

    private void PersistPlaybackPreferences()
    {
        if (_suppressPlaybackPersist) return;
        var s = _settings.Current;
        s.DefaultVolume = Volume;
        s.DefaultRepeatModeIndex = RepeatMode switch
        {
            RepeatMode.All => 1,
            RepeatMode.One => 2,
            _ => 0
        };
        s.DefaultShuffleOn = IsShuffleOn;
        _ = _settings.SaveAsync();
    }

    partial void OnPositionChanged(double value)
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(ProgressAccessibilityHelp));
        NotifyProgressLayoutChanged();
        if (_isSeeking && _queueIndex >= 0)
            Seek(value);
    }

    partial void OnDurationChanged(double value)
    {
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(ProgressAccessibilityHelp));
        OnPropertyChanged(nameof(ShowPlaybackTime));
        NotifyProgressLayoutChanged();
    }

    public void NotifyProgressLayoutChanged()
    {
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(ProgressAccessibilityHelp));
        OnPropertyChanged(nameof(ShowPlaybackTime));
    }

    public string PositionText => FormatTime(Position);
    public string DurationText => FormatTime(Duration);

    public string ProgressAccessibilityName => _l10n.Get("Player.Progress.Name");

    public string PlaybackTimeText =>
        ShowPlaybackTime ? $"{PositionText} / {DurationText}" : string.Empty;

    public string ProgressAccessibilityHelp => PlaybackTimeText;

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public void BeginSeek() => _isSeeking = true;

    public bool IsSeeking => _isSeeking;

    public void EndSeek()
    {
        _isSeeking = false;
        Seek(Position);
    }

    public void SeekToRatio(double ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        Seek(ratio * Duration);
    }

    public void SeekTo(double seconds) => Seek(seconds);

    private void Seek(double seconds)
    {
        _services.Player.Seek(seconds);
        var dur = Math.Max(_services.Player.DurationSeconds, Duration);
        Position = Math.Clamp(seconds, 0, dur);
        PositionUpdated?.Invoke(this, Position);
    }
}
