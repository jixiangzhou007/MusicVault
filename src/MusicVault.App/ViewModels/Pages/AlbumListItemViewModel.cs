using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>专辑列表行（库内统计 + 封面缩略图）。</summary>
public sealed partial class AlbumListItemViewModel : ObservableObject
{
    private ILocalizationService? _l10n;

    public AlbumListItemViewModel(AlbumGroup group)
    {
        Album = group.Album;
        Artist = group.Artist;
        TrackCount = group.TrackCount;
        Year = group.Year;
    }

    public string Album { get; }
    public string Artist { get; }
    public int TrackCount { get; }
    public int Year { get; }

    public string StatsLine
    {
        get
        {
            if (_l10n == null)
                return Year > 0 ? $"{TrackCount} 首 · {Year}" : $"{TrackCount} 首";

            return Year > 0
                ? string.Format(_l10n.Get("Library.Albums.ListStatsWithYear"), TrackCount, Year)
                : string.Format(_l10n.Get("Library.Albums.ListStats"), TrackCount);
        }
    }

    public void RefreshLocalizedStrings(ILocalizationService l10n)
    {
        _l10n = l10n;
        OnPropertyChanged(nameof(StatsLine));
    }

    [ObservableProperty] private byte[]? _coverThumb;

    public bool HasCover => CoverThumb is { Length: > 0 };

    partial void OnCoverThumbChanged(byte[]? value) => OnPropertyChanged(nameof(HasCover));

    public AlbumGroup ToGroup() => new()
    {
        Album = Album,
        Artist = Artist,
        TrackCount = TrackCount,
        Year = Year
    };
}
