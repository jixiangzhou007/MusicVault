using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Sidebar;

/// <summary>歌词行 ViewModel（支持高亮当前行）</summary>
public partial class LyricsLineViewModel : ViewModelBase
{
    public double TimeSeconds { get; }
    public string Text { get; }
    public string? TranslationText { get; }
    public bool HasTranslation { get; }

    [ObservableProperty] private bool _isActive;

    public LyricsLineViewModel(LrcLine line)
    {
        TimeSeconds = line.TimeSeconds;
        Text = line.Text;
        TranslationText = line.TranslationText;
        HasTranslation = line.HasTranslation;
        IsActive = line.IsActive;
    }
}
