using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

internal static class ArtistProfileResultBuilder
{
    public static ArtistExternalFetchResult? FromTracks(
        IReadOnlyList<OnlineTrack> songs,
        string artistName,
        string sourceLabel,
        Func<MusicPlatform, string?>? sourceUrlFactory = null)
    {
        if (songs.Count == 0)
            return null;

        var matched = songs
            .Where(t => ArtistNameMatcher.IsMatch(artistName, t.Artist)
                        || ContainsArtistToken(t.Artist, artistName))
            .OrderByDescending(t => ArtistNameMatcher.Similarity(artistName, t.Artist))
            .ToList();

        var anchor = matched.FirstOrDefault() ?? songs[0];
        var coverUrl = anchor.CoverUrl;
        if (string.IsNullOrWhiteSpace(coverUrl))
            coverUrl = matched.Select(t => t.CoverUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

        var albums = matched
            .Select(t => t.Album)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var summary = albums.Count > 0
            ? ServiceMessageCodec.Format(
                "Artist.Profile.AlbumsInclude",
                string.Join('|', albums))
            : !string.IsNullOrWhiteSpace(anchor.Album)
                ? ServiceMessageCodec.Format("Artist.Profile.AlbumSingle", anchor.Album)
                : ServiceMessageCodec.Format("Artist.Profile.PlatformFallback", artistName, sourceLabel);

        if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(coverUrl))
            return null;

        var sourceUrl = sourceUrlFactory?.Invoke(anchor.Platform)
            ?? DefaultSourceUrl(anchor, artistName);

        return new ArtistExternalFetchResult(
            summary,
            null,
            coverUrl,
            sourceUrl,
            $"{MapPlatformToSource(anchor.Platform)}:{anchor.Id}",
            sourceLabel);
    }

    private static bool ContainsArtistToken(string? artistField, string query)
    {
        if (string.IsNullOrWhiteSpace(artistField))
            return false;
        return artistField.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string? DefaultSourceUrl(OnlineTrack anchor, string artistName) => anchor.Platform switch
    {
        MusicPlatform.NetEase => $"https://music.163.com/#/song?id={anchor.Id}",
        MusicPlatform.QQMusic => $"https://y.qq.com/n/ryqq/song/{anchor.Id}.html",
        MusicPlatform.Kugou => $"https://www.kugou.com/song/#hash={anchor.Id}",
        _ => null
    };

    private static string? MapPlatformToSource(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "netease",
        MusicPlatform.QQMusic => "qq",
        MusicPlatform.Kugou => "kugou",
        MusicPlatform.Kuwo => "kuwo",
        MusicPlatform.Migu => "migu",
        _ => null
    };
}
