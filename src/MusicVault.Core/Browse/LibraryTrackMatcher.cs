using MusicVault.Core.Models;

namespace MusicVault.Core.Browse;

/// <summary>库内曲目与在线曲目比对（标题/歌手启发式）。</summary>
public static class LibraryTrackMatcher
{
    public static bool IsSameTrack(Track library, OnlineTrack online, string? expectedArtist = null)
    {
        if (!TitleMatches(library.Title, online.Title))
            return false;

        if (!string.IsNullOrWhiteSpace(expectedArtist)
            && !ArtistMatches(library.Artist, expectedArtist)
            && !ArtistMatches(library.Artist, online.Artist))
            return false;

        if (!string.IsNullOrWhiteSpace(online.Album)
            && !string.IsNullOrWhiteSpace(library.Album)
            && !AlbumMatches(library.Album, online.Album))
            return false;

        return true;
    }

    public static Track? FindMatch(IEnumerable<Track> libraryTracks, OnlineTrack online, string? expectedArtist = null)
    {
        Track? fuzzy = null;
        foreach (var library in libraryTracks)
        {
            if (!TitleMatches(library.Title, online.Title))
                continue;

            if (IsSameTrack(library, online, expectedArtist))
                return library;

            fuzzy ??= library;
        }

        return fuzzy;
    }

    public static bool TitleMatches(string left, string right) =>
        Normalize(left) == Normalize(right);

    public static bool ArtistMatches(string left, string right) =>
        Normalize(left) == Normalize(right)
        || Normalize(left).Contains(Normalize(right), StringComparison.Ordinal)
        || Normalize(right).Contains(Normalize(left), StringComparison.Ordinal);

    /// <summary>发现页仅填歌手：按歌手字段分段匹配，避免标题命中或无关主唱。</summary>
    public static bool ArtistQueryMatches(string queryArtist, string? artistField, string? albumArtistField = null)
    {
        if (string.IsNullOrWhiteSpace(queryArtist))
            return true;

        var query = Normalize(queryArtist);
        if (query.Length == 0)
            return true;

        foreach (var field in new[] { artistField, albumArtistField })
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;

            if (ArtistFieldContainsQuery(field, query))
                return true;
        }

        return false;
    }

    public static bool AlbumMatches(string left, string right) =>
        Normalize(left) == Normalize(right);

    private static bool ArtistFieldContainsQuery(string artistField, string queryNorm)
    {
        foreach (var segment in SplitArtistSegments(artistField))
        {
            var seg = Normalize(segment);
            if (seg.Length == 0)
                continue;

            if (seg == queryNorm || seg.Contains(queryNorm, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> SplitArtistSegments(string artistField)
    {
        var normalized = artistField
            .Replace(" feat. ", "/", StringComparison.OrdinalIgnoreCase)
            .Replace(" feat ", "/", StringComparison.OrdinalIgnoreCase)
            .Replace(" ft. ", "/", StringComparison.OrdinalIgnoreCase)
            .Replace(" ft ", "/", StringComparison.OrdinalIgnoreCase)
            .Replace(" featuring ", "/", StringComparison.OrdinalIgnoreCase)
            .Replace(" with ", "/", StringComparison.OrdinalIgnoreCase);

        return normalized.Split(
            ['/', '&', '、', ',', '，', ';', '|', '+'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        Span<char> buffer = stackalloc char[value.Length];
        var count = 0;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c) || c is '-' or '—' or '_' or '·' or '.')
                continue;
            buffer[count++] = char.ToLowerInvariant(c);
        }

        return count == 0 ? string.Empty : new string(buffer[..count]);
    }
}
