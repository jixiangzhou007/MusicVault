using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>艺术家列表行（库内统计 + 百科摘要/头像）。</summary>
public sealed partial class ArtistListItemViewModel : ObservableObject
{
    public ArtistListItemViewModel(ArtistGroup group)
    {
        Name = group.Name;
        TrackCount = group.TrackCount;
        AlbumCount = group.AlbumCount;
    }

    public string Name { get; }
    public int TrackCount { get; }
    public int AlbumCount { get; }
    public string StatsLine { get; private set; } = string.Empty;

    public void RefreshStatsLine(string format)
    {
        StatsLine = string.Format(format, AlbumCount, TrackCount);
        OnPropertyChanged(nameof(StatsLine));
    }

    [ObservableProperty] private string? _summaryLine;
    [ObservableProperty] private byte[]? _photoThumb;
    [ObservableProperty] private bool _hasPhoto;
    [ObservableProperty] private bool _hasSummary;

    public void ApplyProfile(ArtistProfile? profile, byte[]? photoThumb)
    {
        PhotoThumb = photoThumb;
        HasPhoto = photoThumb is { Length: > 0 };
        var summary = profile?.Summary;
        SummaryLine = string.IsNullOrWhiteSpace(summary) ? null : Truncate(summary, 72);
        HasSummary = !string.IsNullOrWhiteSpace(SummaryLine);
    }

    private static string Truncate(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }
}
