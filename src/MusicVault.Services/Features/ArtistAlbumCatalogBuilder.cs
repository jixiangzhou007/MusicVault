using MusicVault.Core.Browse;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Features;

/// <summary>合并库内曲目聚合与网易云官方专辑目录。</summary>
public static class ArtistAlbumCatalogBuilder
{
    public static IReadOnlyList<ArtistAlbumCatalogItem> Build(
        IReadOnlyList<Track> libraryTracks,
        string artistName,
        IReadOnlyList<OnlineArtistAlbum> officialOnline)
    {
        var libraryByAlbum = libraryTracks
            .Where(t => !string.IsNullOrWhiteSpace(t.Album))
            .GroupBy(t => t.Album, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var officialByName = new Dictionary<string, OnlineArtistAlbum>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in officialOnline)
        {
            if (!ArtistAlbumBrowseFilter.IsDisplayableOnlineAlbum(o.Name))
                continue;
            if (!NetEaseOfficialAlbumFilter.IsOfficialRelease(
                    o.Name, o.AlbumType, o.SubType, o.TrackCount, o.Year))
                continue;

            officialByName[o.Name] = o;
        }

        var items = new List<ArtistAlbumCatalogItem>();

        foreach (var o in officialByName.Values
                     .OrderByDescending(a => a.Year ?? 0)
                     .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            libraryByAlbum.TryGetValue(o.Name, out var libTracks);
            var trackCount = libTracks?.Count ?? 0;
            var sample = libTracks?
                .OrderBy(t => t.TrackNumber ?? 999)
                .ThenBy(t => t.Title)
                .FirstOrDefault();
            int? year = o.Year;
            if (year is null or <= 0 && libTracks != null)
            {
                var y = libTracks.Select(t => t.Year).Where(v => v is > 0).DefaultIfEmpty().Max();
                if (y > 0) year = y;
            }

            items.Add(new ArtistAlbumCatalogItem(
                o.Name,
                trackCount,
                year,
                MusicPlatform.NetEase,
                o.PlatformAlbumId,
                sample,
                o.PicUrl));
        }

        foreach (var kv in libraryByAlbum.OrderByDescending(kv => kv.Value.Max(t => t.Year ?? 0))
                     .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (officialByName.ContainsKey(kv.Key))
                continue;

            if (!ArtistAlbumBrowseFilter.IsDisplayableLibraryAlbum(kv.Key, artistName, kv.Value))
                continue;

            var g = kv.Value;
            var y = g.Select(t => t.Year).Where(v => v is > 0).DefaultIfEmpty().Max();
            var sample = g.OrderBy(t => t.TrackNumber ?? 999).ThenBy(t => t.Title).FirstOrDefault();
            items.Add(new ArtistAlbumCatalogItem(
                kv.Key,
                g.Count,
                y > 0 ? y : null,
                null,
                null,
                sample,
                null));
        }

        return items;
    }

    public static string ResolveSourceHint(IReadOnlyList<OnlineArtistAlbum> officialOnline) =>
        officialOnline.Count > 0
            ? ServiceMessageCodec.Key("Album.Source.NetEaseMatched")
            : ServiceMessageCodec.Key("Album.Source.LibraryAggregated");
}
