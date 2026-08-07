using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Browse;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Search;

namespace MusicVault.App.Browse;

/// <summary>合并库内曲目与平台专辑曲目，生成详情页行（已下载/未下载）。</summary>
public static class AlbumDetailTrackCatalogBuilder
{
    public static IReadOnlyList<ArtistAlbumTrackItemViewModel> Build(
        string artistName,
        string albumName,
        IReadOnlyList<Track> libraryForAlbum,
        IReadOnlyList<OnlineTrack> onlineTracks,
        AppServices services,
        PlayerBarViewModel player,
        Func<OnlineTrack, AudioQuality> resolveDownloadQuality,
        Action<string>? onStatusMessage = null,
        AppModalHostViewModel? appModal = null,
        OnlineFavoritesService? onlineFavorites = null)
    {
        var items = new List<ArtistAlbumTrackItemViewModel>();
        var matchedLibraryIds = new HashSet<long>();

        if (onlineTracks.Count > 0)
        {
            foreach (var online in onlineTracks
                         .OrderBy(t => t.TrackNumber ?? 999)
                         .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                online.Album = string.IsNullOrWhiteSpace(online.Album) ? albumName : online.Album;
                if (string.IsNullOrWhiteSpace(online.Artist))
                    online.Artist = artistName;

                var library = LibraryTrackMatcher.FindMatch(libraryForAlbum, online, artistName);
                if (library?.Id > 0)
                    matchedLibraryIds.Add(library.Id);

                OnlineTrackQuality.ApplyDownloadCapabilities(online);

                OnlineTrackViewModel? onlineVm = null;
                if (library == null)
                {
                    onlineVm = new OnlineTrackViewModel(
                        online,
                        services,
                        player,
                        () => resolveDownloadQuality(online),
                        appModal: appModal,
                        onlineFavorites: onlineFavorites);
                    onlineVm.OnTrackFeedback = fb => onStatusMessage?.Invoke(fb.Message);
                    onlineVm.IsOnlineFavorite = onlineFavorites?.IsFavorite(online) == true;
                }

                items.Add(new ArtistAlbumTrackItemViewModel(
                    items.Count + 1,
                    online.Title,
                    ResolveTrackArtist(online, library, artistName),
                    FormatDurationMs(online.DurationMs),
                    library != null,
                    library,
                    onlineVm,
                    player));
            }

            foreach (var library in libraryForAlbum
                         .Where(t => t.Id <= 0 || !matchedLibraryIds.Contains(t.Id))
                         .OrderBy(t => t.TrackNumber ?? 999)
                         .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                if (library.Id > 0 && matchedLibraryIds.Contains(library.Id))
                    continue;

                items.Add(new ArtistAlbumTrackItemViewModel(
                    items.Count + 1,
                    library.Title,
                    ResolveTrackArtist(null, library, artistName),
                    FormatDurationMs(library.DurationMs),
                    true,
                    library,
                    null,
                    player));
            }
        }
        else
        {
            foreach (var library in libraryForAlbum
                         .OrderBy(t => t.TrackNumber ?? 999)
                         .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                items.Add(new ArtistAlbumTrackItemViewModel(
                    items.Count + 1,
                    library.Title,
                    ResolveTrackArtist(null, library, artistName),
                    FormatDurationMs(library.DurationMs),
                    true,
                    library,
                    null,
                    player));
            }
        }

        return items;
    }

    private static string FormatDurationMs(int durationMs) =>
        durationMs > 0
            ? TimeSpan.FromMilliseconds(durationMs).ToString(@"m\:ss")
            : "--:--";

    private static string ResolveTrackArtist(OnlineTrack? online, Track? library, string albumArtist)
    {
        if (!string.IsNullOrWhiteSpace(online?.Artist))
            return online.Artist.Trim();
        if (!string.IsNullOrWhiteSpace(library?.Artist))
            return library.Artist.Trim();
        return albumArtist.Trim();
    }
}
