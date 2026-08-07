using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.ViewModels.Sidebar;
using MusicVault.App.Views.Player;
using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>Windows 桌面双行歌词浮窗生命周期与位置持久化。</summary>
public sealed class DesktopLyricsHost : IDisposable
{
    public const int DefaultWindowWidth = 840;
    private readonly AppSettingsService _settings;
    private readonly PlayerBarViewModel _player;
    private readonly RightSidebarViewModel _lyrics;
    private readonly DesktopLyricsViewModel _vm;
    private readonly Func<Window?> _getOwnerWindow;
    private readonly Action? _openSettings;
    private DesktopLyricsWindow? _window;
    private EventHandler<PixelPointEventArgs>? _positionChangedHandler;

    public DesktopLyricsHost(
        AppSettingsService settings,
        PlayerBarViewModel player,
        RightSidebarViewModel lyrics,
        ILocalizationService l10n,
        Func<Window?> getOwnerWindow,
        Action? openSettings = null)
    {
        _settings = settings;
        _player = player;
        _lyrics = lyrics;
        _getOwnerWindow = getOwnerWindow;
        _openSettings = openSettings;
        _vm = new DesktopLyricsViewModel(
            player,
            lyrics,
            l10n,
            close: Hide,
            openSettings: () => _openSettings?.Invoke(),
            persistLock: locked => _settings.Current.DesktopLyricsLocked = locked,
            adjustFontSize: delta =>
            {
                var size = Math.Clamp(_settings.Current.DesktopLyricsFontSize + delta, 16, 40);
                if (Math.Abs(size - _settings.Current.DesktopLyricsFontSize) < 0.01)
                    return;
                _settings.Current.DesktopLyricsFontSize = size;
                ApplyVisualSettings();
            });
    }

    public static bool IsSupported => OperatingSystem.IsWindows();

    public bool IsOpen => _window is { IsVisible: true };

    public void Toggle()
    {
        if (!IsSupported)
            return;
        if (IsOpen)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        if (!IsSupported)
            return;

        _window ??= CreateWindow();
        ApplyVisualSettings();
        _vm.RefreshLines();
        _ = EnsureLyricsAsync();

        if (_settings.Current.DesktopLyricsLeft >= 0 && _settings.Current.DesktopLyricsTop >= 0)
        {
            _window.Position = new PixelPoint(
                (int)_settings.Current.DesktopLyricsLeft,
                (int)_settings.Current.DesktopLyricsTop);
        }
        else
            CenterOnPrimaryScreen();

        var owner = _getOwnerWindow();
        if (owner != null)
            _window.Show(owner);
        else
            _window.Show();

        _player.SetDesktopLyricsOpen(true);
        _settings.Current.DesktopLyricsEnabled = true;
    }

    public void Hide()
    {
        if (_window == null)
            return;

        PersistPosition();
        _window.Hide();
        _player.SetDesktopLyricsOpen(false);
        _settings.Current.DesktopLyricsEnabled = false;
    }

    public void ApplySettings(bool persistShowState = false)
    {
        if (!IsSupported)
            return;

        ApplyVisualSettings();

        var enabled = _settings.Current.DesktopLyricsEnabled;
        if (enabled)
        {
            if (!IsOpen)
                Show();
        }
        else if (IsOpen)
            Hide();

        if (persistShowState)
            _ = _settings.SaveAsync();
    }

    public async Task EnsureLyricsAsync()
    {
        if (!IsOpen)
            return;

        if (_player.CurrentTrack is { } track)
            await _lyrics.EnsureLyricsForTrackAsync(track, _player.Position).ConfigureAwait(true);
        else if (_player.CurrentOnlinePreviewTrack is { } online)
            await _lyrics.EnsureLyricsForOnlinePreviewAsync(online, _player.Position).ConfigureAwait(true);

        _vm.RefreshLines();
        if (_lyrics.HasLyricLines)
            _lyrics.UpdateLyricsPosition(_player.Position);
    }

    public void OnTrackChanged()
    {
        if (!IsOpen)
            return;
        _ = EnsureLyricsAsync();
    }

    public void OnPositionUpdated()
    {
        if (!IsOpen)
            return;

        if (_lyrics.HasLyricLines)
            _lyrics.UpdateLyricsPosition(forceScrollNotify: _player.IsSeeking);
        else
            _vm.RefreshLines();
    }

    public void RefreshLocalizedStrings()
    {
        _vm.RefreshLocalizedStrings();
        if (_window != null)
            _window.Title = _vm.WindowTitle;
    }

    public void Dispose()
    {
        var window = _window;
        _window = null;
        if (window == null)
            return;

        if (_positionChangedHandler != null)
            window.PositionChanged -= _positionChangedHandler;

        try
        {
            if (window.IsVisible)
                PersistPositionFrom(window);
        }
        catch
        {
            /* ignore */
        }

        void CloseOnUi()
        {
            try
            {
                window.Close();
            }
            catch
            {
                /* ignore */
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            CloseOnUi();
        else
            Dispatcher.UIThread.InvokeAsync(CloseOnUi).GetAwaiter().GetResult();

        _player.SetDesktopLyricsOpen(false);
    }

    private DesktopLyricsWindow CreateWindow()
    {
        var window = new DesktopLyricsWindow
        {
            DataContext = _vm,
            Title = _vm.WindowTitle,
            Width = DefaultWindowWidth,
            ShowActivated = false
        };
        _positionChangedHandler = (_, _) => PersistPosition();
        window.PositionChanged += _positionChangedHandler;
        window.Closed += OnDesktopWindowClosed;
        return window;
    }

    private void OnDesktopWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not DesktopLyricsWindow window)
            return;

        window.Closed -= OnDesktopWindowClosed;
        if (_positionChangedHandler != null)
            window.PositionChanged -= _positionChangedHandler;

        if (ReferenceEquals(_window, window))
            _window = null;
        _player.SetDesktopLyricsOpen(false);
    }

    private void ApplyVisualSettings()
    {
        var s = _settings.Current;
        _vm.FontSize = Math.Clamp(s.DesktopLyricsFontSize, 16, 40);
        _vm.WindowOpacity = Math.Clamp(s.DesktopLyricsOpacity, 0.45, 1);
        _vm.SetLocked(s.DesktopLyricsLocked);
        _vm.RefreshLines();
    }

    private void CenterOnPrimaryScreen()
    {
        if (_window == null)
            return;

        var screens = _window.Screens;
        var screen = screens.ScreenFromWindow(_window) ?? screens.Primary;
        if (screen == null)
            return;

        var w = DefaultWindowWidth;
        var x = screen.WorkingArea.X + (screen.WorkingArea.Width - w) / 2;
        var y = screen.WorkingArea.Y + (int)(screen.WorkingArea.Height * 0.72);
        _window.Position = new PixelPoint(x, y);
    }

    private void PersistPosition() => PersistPositionFrom(_window);

    private void PersistPositionFrom(Window? window)
    {
        if (window == null || !window.IsVisible)
            return;

        var pos = window.Position;
        _settings.Current.DesktopLyricsLeft = pos.X;
        _settings.Current.DesktopLyricsTop = pos.Y;
    }
}
