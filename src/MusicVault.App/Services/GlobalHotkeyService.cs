using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MusicVault.App.ViewModels;

namespace MusicVault.App.Services;

/// <summary>
/// 全局快捷键服务。应用获得焦点时响应媒体键与自定义快捷键。
/// 在文本输入框内自动放行空格/方向键；可在设置中关闭。
/// </summary>
public sealed class GlobalHotkeyService
{
    private Window? _window;
    private MainWindowViewModel? _shell;
    private AppSettingsService? _settings;

    /// <summary>绑定主窗口快捷键</summary>
    public void Attach(Window window, MainWindowViewModel shell, AppSettingsService settings)
    {
        Detach();
        _window = window;
        _shell = shell;
        _settings = settings;
        window.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    public void Detach()
    {
        if (_window == null)
            return;

        _window.RemoveHandler(InputElement.KeyDownEvent, OnWindowKeyDown);
        _window = null;
        _shell = null;
        _settings = null;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_shell == null || _settings == null)
            return;

        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            _shell.ToggleCommandPaletteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var handled = AppKeyboardShortcuts.TryHandlePlaybackShortcut(
            e,
            _window,
            _settings.Current.EnableGlobalHotkeys,
            _ => _shell.Player.TogglePlayPauseCommand.Execute(null),
            _ => _shell.Player.PreviousCommand.Execute(null),
            _ => _shell.Player.NextCommand.Execute(null),
            _ => _shell.Player.ToggleLyricsCommand.Execute(null),
            _ => _shell.FocusLocalSearchCommand.Execute(null));

        if (handled)
            e.Handled = true;
    }
}
