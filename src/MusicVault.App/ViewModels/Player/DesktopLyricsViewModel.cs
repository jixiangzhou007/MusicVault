using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Icons;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Sidebar;
using System.Windows.Input;

namespace MusicVault.App.ViewModels.Player;

/// <summary>桌面歌词浮窗：当前行 + 下一行，悬浮工具栏（参考 QQ 音乐）。</summary>
public partial class DesktopLyricsViewModel : ViewModelBase
{
    private readonly PlayerBarViewModel _player;
    private readonly RightSidebarViewModel _lyrics;
    private readonly ILocalizationService _l10n;
    private readonly Action _close;
    private readonly Action _openSettings;
    private readonly Action<bool> _persistLock;
    private readonly Action<int> _adjustFontSize;
    private string _noLyricsFallback = string.Empty;
    private string _notPlayingFallback = string.Empty;

    [ObservableProperty] private string _currentLine = string.Empty;
    [ObservableProperty] private string _nextLine = string.Empty;
    [ObservableProperty] private bool _hasNextLine;
    [ObservableProperty] private bool _hasLyrics;
    [ObservableProperty] private double _fontSize = 24;
    [ObservableProperty] private double _windowOpacity = 0.88;
    [ObservableProperty] private bool _isHovering;
    [ObservableProperty] private bool _isLocked;

    [ObservableProperty] private string _songTitle = string.Empty;
    [ObservableProperty] private string _songArtist = string.Empty;
    [ObservableProperty] private byte[]? _coverImage;
    [ObservableProperty] private bool _hasCover;

    public double NextFontSize => Math.Max(14, FontSize * 0.82);

    public bool IsToolbarVisible => IsHovering;

    public ICommand TogglePlayPauseCommand => _player.TogglePlayPauseCommand;
    public ICommand PreviousCommand => _player.PreviousCommand;
    public ICommand NextCommand => _player.NextCommand;

    public bool IsPlaying => _player.IsPlaying;
    public string PlayPauseToolTip => _player.PlayPauseToolTip;
    public string WindowTitle => _l10n.Get("Player.DesktopLyrics.WindowTitle");
    public string DecreaseFontToolTip => _l10n.Get("Player.DesktopLyrics.DecreaseFontToolTip");
    public string IncreaseFontToolTip => _l10n.Get("Player.DesktopLyrics.IncreaseFontToolTip");
    public string PreviousToolTip => _l10n.Get("Player.DesktopLyrics.PreviousToolTip");
    public string NextToolTip => _l10n.Get("Player.DesktopLyrics.NextToolTip");
    public string SettingsToolTip => _l10n.Get("Player.DesktopLyrics.SettingsToolTip");
    public string CloseToolTip => _l10n.Get("Player.DesktopLyricsOff");
    public string LockToolTip => IsLocked
        ? _l10n.Get("Player.DesktopLyrics.UnlockToolTip")
        : _l10n.Get("Player.DesktopLyrics.LockToolTip");
    public string LockIconGlyph => IsLocked ? IconGlyphs.Lock : IconGlyphs.Unlock;

    public DesktopLyricsViewModel(
        PlayerBarViewModel player,
        RightSidebarViewModel lyrics,
        ILocalizationService l10n,
        Action close,
        Action openSettings,
        Action<bool> persistLock,
        Action<int> adjustFontSize)
    {
        _player = player;
        _lyrics = lyrics;
        _l10n = l10n;
        _close = close;
        _openSettings = openSettings;
        _persistLock = persistLock;
        _adjustFontSize = adjustFontSize;

        _noLyricsFallback = _l10n.Get("Sidebar.Lyrics.NoLyrics");
        _notPlayingFallback = _l10n.Get("Player.NotPlaying");

        _lyrics.ActiveLineIndexChanged += (_, _) => RefreshLines();
        _lyrics.LyricsContentUpdated += (_, _) => RefreshLines();
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerBarViewModel.Title)
                or nameof(PlayerBarViewModel.Artist)
                or nameof(PlayerBarViewModel.CurrentTrack)
                or nameof(PlayerBarViewModel.CurrentOnlinePreviewTrack)
                or nameof(PlayerBarViewModel.IsPlaying)
                or nameof(PlayerBarViewModel.CoverImage)
                or nameof(PlayerBarViewModel.HasCover))
            {
                SyncTrackInfo();
                if (e.PropertyName is not nameof(PlayerBarViewModel.IsPlaying))
                    RefreshLines();
                else
                {
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(PlayPauseToolTip));
                }
            }
        };

        SyncTrackInfo();
        RefreshLines();
    }

    public void RefreshLocalizedStrings()
    {
        _noLyricsFallback = _l10n.Get("Sidebar.Lyrics.NoLyrics");
        _notPlayingFallback = _l10n.Get("Player.NotPlaying");

        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(DecreaseFontToolTip));
        OnPropertyChanged(nameof(IncreaseFontToolTip));
        OnPropertyChanged(nameof(PreviousToolTip));
        OnPropertyChanged(nameof(NextToolTip));
        OnPropertyChanged(nameof(SettingsToolTip));
        OnPropertyChanged(nameof(CloseToolTip));
        OnPropertyChanged(nameof(LockToolTip));

        SyncTrackInfo();
        RefreshLines();
    }

    public void SetLocked(bool locked)
    {
        if (IsLocked == locked)
            return;
        IsLocked = locked;
        OnPropertyChanged(nameof(LockToolTip));
        OnPropertyChanged(nameof(LockIconGlyph));
    }

    public void SetHovering(bool hovering)
    {
        if (IsHovering == hovering)
            return;
        IsHovering = hovering;
        OnPropertyChanged(nameof(IsToolbarVisible));
    }

    public void RefreshLines()
    {
        var lines = _lyrics.LyricLines;
        var idx = _lyrics.ActiveLineIndex;
        HasLyrics = lines.Count > 0 && idx >= 0;

        if (HasLyrics)
        {
            CurrentLine = lines[idx].Text;
            if (idx + 1 < lines.Count)
            {
                NextLine = lines[idx + 1].Text;
                HasNextLine = !string.IsNullOrWhiteSpace(NextLine);
            }
            else
            {
                NextLine = string.Empty;
                HasNextLine = false;
            }
            return;
        }

        var title = _player.Title?.Trim() ?? string.Empty;
        var artist = _player.Artist?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(title) && title != _notPlayingFallback)
        {
            CurrentLine = title;
            NextLine = artist;
            HasNextLine = !string.IsNullOrWhiteSpace(artist);
            return;
        }

        CurrentLine = _noLyricsFallback;
        NextLine = string.Empty;
        HasNextLine = false;
    }

    private void SyncTrackInfo()
    {
        var title = _player.Title?.Trim();
        SongTitle = string.IsNullOrWhiteSpace(title) ? _notPlayingFallback : title;
        SongArtist = _player.Artist?.Trim() ?? string.Empty;
        CoverImage = _player.CoverImage;
        HasCover = _player.HasCover;
    }

    partial void OnFontSizeChanged(double value) => OnPropertyChanged(nameof(NextFontSize));

    partial void OnIsLockedChanged(bool value)
    {
        _persistLock(value);
        OnPropertyChanged(nameof(LockToolTip));
        OnPropertyChanged(nameof(LockIconGlyph));
    }

    [RelayCommand]
    private void Close() => _close();

    [RelayCommand]
    private void OpenSettings() => _openSettings();

    [RelayCommand]
    private void ToggleLock() => IsLocked = !IsLocked;

    [RelayCommand]
    private void DecreaseFont() => _adjustFontSize(-2);

    [RelayCommand]
    private void IncreaseFont() => _adjustFontSize(2);
}
