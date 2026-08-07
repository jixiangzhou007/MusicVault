using MusicVault.Core.Models;

namespace MusicVault.Core.Browse;

/// <summary>艺术家详情页专辑展示过滤（库内聚合与线上目录共用占位规则）。</summary>
public static class ArtistAlbumBrowseFilter
{
    private static readonly string[] PlaceholderNames =
    [
        "unknown", "untitled", "various artists", "various", "va", "n/a", "na", "none",
        "no album", "not available", "non-album", "non album",
        "未知", "未知专辑", "无", "无专辑", "未分类", "未命名", "其他", "杂项",
        "—", "-", "?", "."
    ];

    private static readonly string[] DiscLikePatterns =
    [
        "disc", "disk", "cd", "dvd"
    ];

    /// <summary>库内按曲目 Album 字段聚合的条目。</summary>
    public static bool IsDisplayableLibraryAlbum(string album, string artistName, IReadOnlyList<Track> tracks)
    {
        if (!IsDisplayableName(album))
            return false;

        if (AlbumEqualsArtist(album, artistName))
            return false;

        if (IsDiscLikeLabel(album))
            return false;

        if (IsTitlePseudoAlbum(album, tracks))
            return false;

        return true;
    }

    /// <summary>线上官方专辑目录条目。</summary>
    public static bool IsDisplayableOnlineAlbum(string album) =>
        IsDisplayableName(album) && !IsDiscLikeLabel(album);

    public static bool IsDisplayableName(string? album)
    {
        if (string.IsNullOrWhiteSpace(album))
            return false;

        var trimmed = album.Trim();
        if (trimmed.Length < 2)
            return false;

        var norm = Normalize(trimmed);
        if (norm.Length < 2)
            return false;

        foreach (var placeholder in PlaceholderNames)
        {
            if (norm.Equals(Normalize(placeholder), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool AlbumEqualsArtist(string album, string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return false;

        var a = Normalize(album);
        var artist = Normalize(artistName);
        return a.Length > 0 && a == artist;
    }

    private static bool IsDiscLikeLabel(string album)
    {
        var norm = Normalize(album);
        if (norm.Length == 0)
            return false;

        foreach (var prefix in DiscLikePatterns)
        {
            if (norm == prefix)
                return true;
            if (norm.StartsWith(prefix, StringComparison.Ordinal)
                && norm.Length <= prefix.Length + 3
                && norm[prefix.Length..].All(char.IsDigit))
                return true;
        }

        return false;
    }

    /// <summary>每首曲目的 Album 与 Title 相同，多为误标或单曲盘占位。</summary>
    private static bool IsTitlePseudoAlbum(string album, IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0)
            return false;

        var albumNorm = Normalize(album);
        if (albumNorm.Length == 0)
            return true;

        foreach (var track in tracks)
        {
            var titleNorm = Normalize(track.Title);
            if (titleNorm.Length == 0 || titleNorm != albumNorm)
                return false;
        }

        return true;
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
