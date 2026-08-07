using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>艺术家详情页专辑卡片。</summary>
public sealed partial class ArtistAlbumItemViewModel : ObservableObject
{
    private ILocalizationService? _l10n;

    public string Album { get; init; } = string.Empty;
    public int? Year { get; init; }
    public MusicPlatform Platform { get; init; } = MusicPlatform.NetEase;
    public string? PlatformAlbumId { get; init; }

    [ObservableProperty] private byte[]? _coverThumb;
    [ObservableProperty] private int _libraryTrackCount;
    [ObservableProperty] private int? _onlineTrackCount;
    [ObservableProperty] private int? _missingTrackCount;

    public bool HasCover => CoverThumb is { Length: > 0 };
    public bool HasOnlineCatalog => !string.IsNullOrWhiteSpace(PlatformAlbumId);

    public string TrackCountText
    {
        get
        {
            if (_l10n == null)
            {
                if (OnlineTrackCount is int onlineTotalFallback && onlineTotalFallback > 0)
                {
                    if (MissingTrackCount is int missingFallback && missingFallback > 0)
                        return $"库内 {LibraryTrackCount}/{onlineTotalFallback} · 缺 {missingFallback}";

                    return LibraryTrackCount > 0
                        ? $"库内 {LibraryTrackCount}/{onlineTotalFallback} 首"
                        : $"共 {onlineTotalFallback} 首";
                }

                if (LibraryTrackCount > 0)
                    return $"库内 {LibraryTrackCount} 首";

                if (Year is > 0)
                    return $"{Year} 年";

                return HasOnlineCatalog ? "点击查看曲目" : "—";
            }

            if (OnlineTrackCount is int onlineTotal && onlineTotal > 0)
            {
                if (MissingTrackCount is int missing && missing > 0)
                    return string.Format(
                        _l10n.Get("Library.Artists.AlbumCard.LibraryRatioMissing"),
                        LibraryTrackCount,
                        onlineTotal,
                        missing);

                return LibraryTrackCount > 0
                    ? string.Format(
                        _l10n.Get("Library.Artists.AlbumCard.LibraryRatio"),
                        LibraryTrackCount,
                        onlineTotal)
                    : string.Format(_l10n.Get("Library.Artists.AlbumCard.OnlineTotal"), onlineTotal);
            }

            if (LibraryTrackCount > 0)
                return string.Format(
                    _l10n.Get("Library.Artists.AlbumTracksSummary.LibraryOnly"),
                    LibraryTrackCount);

            if (Year is > 0)
                return string.Format(_l10n.Get("Library.Artists.AlbumCard.Year"), Year);

            return HasOnlineCatalog ? _l10n.Get("Library.Artists.AlbumCard.ViewTracks") : "—";
        }
    }

    public void RefreshLocalizedStrings(ILocalizationService l10n)
    {
        _l10n = l10n;
        OnPropertyChanged(nameof(TrackCountText));
    }

    partial void OnCoverThumbChanged(byte[]? value) => OnPropertyChanged(nameof(HasCover));

    partial void OnLibraryTrackCountChanged(int value) => OnPropertyChanged(nameof(TrackCountText));

    partial void OnOnlineTrackCountChanged(int? value) => OnPropertyChanged(nameof(TrackCountText));

    partial void OnMissingTrackCountChanged(int? value) => OnPropertyChanged(nameof(TrackCountText));
}
