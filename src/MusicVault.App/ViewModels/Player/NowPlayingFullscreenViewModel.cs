using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Sidebar;

namespace MusicVault.App.ViewModels.Player;

/// <summary>全屏正在播放详情页</summary>
public partial class NowPlayingFullscreenViewModel : ViewModelBase
{
    private const double MinLyricsFont = 16;
    private const double MaxLyricsFont = 40;
    private const double FontStep = 2;

    private readonly ILocalizationService _l10n;

    public PlayerBarViewModel Player { get; }
    public RightSidebarViewModel Lyrics { get; }

    [ObservableProperty] private double _lyricsFontSize = 26;

    public string DragToolTip => _l10n.Get("Player.Fullscreen.DragToolTip");
    public string BackToolTip => _l10n.Get("Player.Fullscreen.BackToolTip");
    public string DecreaseLyricsFontToolTip => _l10n.Get("Player.Fullscreen.DecreaseLyricsFontToolTip");
    public string IncreaseLyricsFontToolTip => _l10n.Get("Player.Fullscreen.IncreaseLyricsFontToolTip");
    public string CoverCloseToolTip => _l10n.Get("Player.Fullscreen.CoverCloseToolTip");

    public NowPlayingFullscreenViewModel(
        PlayerBarViewModel player,
        RightSidebarViewModel lyrics,
        ILocalizationService l10n)
    {
        Player = player;
        Lyrics = lyrics;
        _l10n = l10n;
        Lyrics.LyricsContentUpdated += (_, _) => TrackChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CloseRequested;
    public event EventHandler? TrackChanged;

    public void NotifyTrackChanged() => TrackChanged?.Invoke(this, EventArgs.Empty);

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(DragToolTip));
        OnPropertyChanged(nameof(BackToolTip));
        OnPropertyChanged(nameof(DecreaseLyricsFontToolTip));
        OnPropertyChanged(nameof(IncreaseLyricsFontToolTip));
        OnPropertyChanged(nameof(CoverCloseToolTip));
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void IncreaseLyricsFont() =>
        LyricsFontSize = Math.Min(MaxLyricsFont, LyricsFontSize + FontStep);

    [RelayCommand]
    private void DecreaseLyricsFont() =>
        LyricsFontSize = Math.Max(MinLyricsFont, LyricsFontSize - FontStep);

    public bool CanIncreaseLyricsFont => LyricsFontSize < MaxLyricsFont;
    public bool CanDecreaseLyricsFont => LyricsFontSize > MinLyricsFont;

    partial void OnLyricsFontSizeChanged(double value)
    {
        OnPropertyChanged(nameof(CanIncreaseLyricsFont));
        OnPropertyChanged(nameof(CanDecreaseLyricsFont));
    }
}
