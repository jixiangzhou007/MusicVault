using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Sidebar;

namespace MusicVault.App.ViewModels.Player;

/// <summary>精简播放模式 UI：复用 <see cref="PlayerBarViewModel"/> 传输与封面，歌词行来自侧栏。</summary>
public partial class CompactPlayerViewModel : ViewModelBase
{
    private readonly PlayerBarViewModel _player;
    private readonly RightSidebarViewModel _lyrics;
    private readonly Action _exitCompact;
    private readonly ILocalizationService _l10n;

    [ObservableProperty] private string _currentLyricLine = string.Empty;

    public PlayerBarViewModel Player => _player;

    public string WindowMinimizeToolTip => _l10n.Get("Shell.Window.Minimize");

    public string WindowRestoreToolTip => _l10n.Get("Shell.Window.Restore");

    public string WindowCloseToolTip => _l10n.Get("Shell.Window.Close");

    /// <summary>歌词行与歌名不同时才显示歌名，避免重复两行相同文字。</summary>
    public bool ShowCompactSubtitle
    {
        get
        {
            var title = _player.Title?.Trim() ?? string.Empty;
            var line = CurrentLyricLine?.Trim() ?? string.Empty;
            return title.Length > 0
                   && line.Length > 0
                   && !string.Equals(title, line, StringComparison.Ordinal);
        }
    }

    public CompactPlayerViewModel(
        PlayerBarViewModel player,
        RightSidebarViewModel lyrics,
        Action exitCompact,
        ILocalizationService l10n)
    {
        _player = player;
        _lyrics = lyrics;
        _exitCompact = exitCompact;
        _l10n = l10n;

        _lyrics.ActiveLineIndexChanged += (_, _) => RefreshLyricLine();
        _lyrics.LyricsContentUpdated += (_, _) => RefreshLyricLine();
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerBarViewModel.Title)
                or nameof(PlayerBarViewModel.Artist)
                or nameof(PlayerBarViewModel.CurrentTrack)
                or nameof(PlayerBarViewModel.CurrentOnlinePreviewTrack))
                RefreshLyricLine();
        };

        RefreshLyricLine();
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(WindowMinimizeToolTip));
        OnPropertyChanged(nameof(WindowRestoreToolTip));
        OnPropertyChanged(nameof(WindowCloseToolTip));
        RefreshLyricLine();
    }

    public void RefreshLyricLine()
    {
        var lines = _lyrics.LyricLines;
        var idx = _lyrics.ActiveLineIndex;
        if (lines.Count > 0 && idx >= 0 && idx < lines.Count)
        {
            var text = lines[idx].Text?.Trim() ?? string.Empty;
            CurrentLyricLine = string.IsNullOrWhiteSpace(text) ? _player.Title ?? string.Empty : text;
            return;
        }

        CurrentLyricLine = _player.Title?.Trim() ?? _l10n.Get("Player.NotPlaying");
        OnPropertyChanged(nameof(ShowCompactSubtitle));
    }

    partial void OnCurrentLyricLineChanged(string value) =>
        OnPropertyChanged(nameof(ShowCompactSubtitle));

    [RelayCommand]
    private void ExitCompact() => _exitCompact();
}
