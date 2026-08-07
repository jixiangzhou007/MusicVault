#if WINDOWS
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicVault.App.ViewModels.Player;
using Windows.Media;
using Windows.Storage.Streams;

namespace MusicVault.App.Services;

/// <summary>
/// 将播放状态同步到 Windows 系统媒体控件（任务栏、锁屏、耳机媒体键）。
/// </summary>
public sealed class WindowsSystemMediaTransportService : IDisposable
{
    private SystemMediaTransportControls? _smtc;
    private PlayerBarViewModel? _player;
    private Window? _window;
    private bool _disposed;
    private DateTime _lastTimelineSyncUtc = DateTime.MinValue;
    private int _metadataVersion;
    private CancellationTokenSource? _metadataCts;

    public void Attach(Window window, PlayerBarViewModel player)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsSystemMediaTransportService));

        Detach();
        _window = window;
        _player = player;

        if (!TryCreateControls())
        {
            window.Opened += OnWindowOpened;
            return;
        }

        WirePlayer();
        SyncAll();
    }

    public void Detach()
    {
        _metadataCts?.Cancel();
        _metadataCts = null;

        if (_window != null)
            _window.Opened -= OnWindowOpened;

        if (_player != null)
            _player.PropertyChanged -= OnPlayerPropertyChanged;

        if (_smtc != null)
        {
            _smtc.ButtonPressed -= OnButtonPressed;
            _smtc.PlaybackPositionChangeRequested -= OnPlaybackPositionChangeRequested;
            _smtc.IsEnabled = false;
            _smtc = null;
        }

        _player = null;
        _window = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Detach();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_window == null)
            return;

        _window.Opened -= OnWindowOpened;
        if (!TryCreateControls())
            return;

        WirePlayer();
        SyncAll();
    }

    private bool TryCreateControls()
    {
        if (_window == null || _smtc != null)
            return _smtc != null;

        var hwnd = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
            return false;

        try
        {
            _smtc = SystemMediaTransportControlsInterop.GetForWindow(hwnd);
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.ButtonPressed += OnButtonPressed;
            _smtc.PlaybackPositionChangeRequested += OnPlaybackPositionChangeRequested;
            return true;
        }
        catch
        {
            _smtc = null;
            return false;
        }
    }

    private void WirePlayer()
    {
        if (_player == null)
            return;

        _player.PropertyChanged -= OnPlayerPropertyChanged;
        _player.PropertyChanged += OnPlayerPropertyChanged;
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_smtc == null || _player == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(PlayerBarViewModel.IsPlaying):
            case nameof(PlayerBarViewModel.HasActivePlayback):
                SyncPlaybackStatus();
                break;
            case nameof(PlayerBarViewModel.Title):
            case nameof(PlayerBarViewModel.Artist):
            case nameof(PlayerBarViewModel.CoverImage):
            case nameof(PlayerBarViewModel.CoverImageLarge):
                QueueMetadataSync();
                break;
            case nameof(PlayerBarViewModel.Position):
            case nameof(PlayerBarViewModel.Duration):
                SyncTimelineThrottled();
                break;
        }
    }

    private void SyncAll()
    {
        SyncPlaybackStatus();
        SyncTimeline(force: true);
        QueueMetadataSync();
    }

    private void SyncPlaybackStatus()
    {
        if (_smtc == null || _player == null)
            return;

        if (!_player.HasActivePlayback)
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
            return;
        }

        _smtc.PlaybackStatus = _player.IsPlaying
            ? MediaPlaybackStatus.Playing
            : MediaPlaybackStatus.Paused;
    }

    private void SyncTimelineThrottled()
    {
        if (_player == null || !_player.HasActivePlayback)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastTimelineSyncUtc).TotalMilliseconds < 500 && _player.IsPlaying)
            return;

        SyncTimeline(force: false);
    }

    private void SyncTimeline(bool force)
    {
        if (_smtc == null || _player == null)
            return;

        if (!_player.HasActivePlayback)
            return;

        var duration = Math.Max(_player.Duration, 0);
        if (duration <= 0)
            return;

        var position = Math.Clamp(_player.Position, 0, duration);
        if (!force)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastTimelineSyncUtc).TotalMilliseconds < 500)
                return;
            _lastTimelineSyncUtc = now;
        }
        else
        {
            _lastTimelineSyncUtc = DateTime.UtcNow;
        }

        try
        {
            _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
            {
                StartTime = TimeSpan.Zero,
                MinSeekTime = TimeSpan.Zero,
                MaxSeekTime = TimeSpan.FromSeconds(duration),
                EndTime = TimeSpan.FromSeconds(duration),
                Position = TimeSpan.FromSeconds(position)
            });
        }
        catch
        {
            /* 非关键路径 */
        }
    }

    private void QueueMetadataSync()
    {
        if (_smtc == null || _player == null)
            return;

        var version = Interlocked.Increment(ref _metadataVersion);
        _metadataCts?.Cancel();
        _metadataCts = new CancellationTokenSource();
        var token = _metadataCts.Token;
        _ = UpdateMetadataAsync(version, token);
    }

    private async Task UpdateMetadataAsync(int version, CancellationToken token)
    {
        if (_smtc == null || _player == null)
            return;

        try
        {
            await Task.Delay(80, token).ConfigureAwait(false);
            if (token.IsCancellationRequested || version != _metadataVersion)
                return;

            var player = _player;
            var smtc = _smtc;
            if (player == null || smtc == null)
                return;

            if (!player.HasActivePlayback)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_smtc == null)
                        return;
                    _smtc.DisplayUpdater.ClearAll();
                });
                return;
            }

            var title = player.Title;
            var artist = player.Artist;
            var album = player.CurrentTrack?.Album
                ?? player.CurrentOnlinePreviewTrack?.Album
                ?? string.Empty;
            var cover = player.CoverImageLarge ?? player.CoverImage;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (token.IsCancellationRequested || version != _metadataVersion || _smtc == null)
                    return;

                try
                {
                    var updater = _smtc.DisplayUpdater;
                    updater.Type = MediaPlaybackType.Music;
                    updater.MusicProperties.Title = title;
                    updater.MusicProperties.Artist = artist;
                    updater.MusicProperties.AlbumTitle = album;
                    if (cover is { Length: > 0 })
                    {
                        var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync(cover.AsBuffer()).AsTask().ConfigureAwait(true);
                        stream.Seek(0);
                        updater.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    }
                    else
                    {
                        updater.Thumbnail = null;
                    }

                    updater.Update();
                }
                catch
                {
                    /* 非关键路径 */
                }
            });
        }
        catch (OperationCanceledException)
        {
            /* debounce */
        }
    }

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        if (_player == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_player == null)
                return;

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    if (_player.HasActivePlayback)
                        _player.TogglePlayPauseCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    if (_player.HasActivePlayback)
                        _player.NextCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    if (_player.HasActivePlayback)
                        _player.PreviousCommand.Execute(null);
                    break;
            }
        });
    }

    private void OnPlaybackPositionChangeRequested(
        SystemMediaTransportControls sender,
        PlaybackPositionChangeRequestedEventArgs args)
    {
        if (_player == null)
            return;

        var seconds = args.RequestedPlaybackPosition.TotalSeconds;
        Dispatcher.UIThread.Post(() =>
        {
            if (_player == null || !_player.HasActivePlayback)
                return;

            _player.SeekTo(seconds);
            SyncTimeline(force: true);
        });
    }
}
#endif
