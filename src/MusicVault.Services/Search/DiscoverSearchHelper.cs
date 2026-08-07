using MusicVault.Core.Browse;
using MusicVault.Core.Models;

namespace MusicVault.Services.Search;

/// <summary>发现页歌名/歌手双字段搜索：组合 API 关键字与结果过滤。</summary>
public static class DiscoverSearchHelper
{
    public static bool HasCriteria(string? songName, string? artist) =>
        !string.IsNullOrWhiteSpace(songName) || !string.IsNullOrWhiteSpace(artist);

    /// <summary>统一 API 与各平台源仅支持单一 q，双字段时组合为「歌手 歌名」。</summary>
    public static string BuildApiKeyword(string? songName, string? artist)
    {
        var title = songName?.Trim() ?? string.Empty;
        var artistName = artist?.Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(artistName))
            return $"{artistName} {title}";
        if (!string.IsNullOrEmpty(title))
            return title;
        return artistName;
    }

    /// <summary>双字段时客户端再筛一层，避免同名不同歌手；仅歌手时按歌手字段严格匹配。</summary>
    public static IReadOnlyList<OnlineTrack> FilterResults(
        IReadOnlyList<OnlineTrack> tracks,
        string? songName,
        string? artist)
    {
        var title = songName?.Trim() ?? string.Empty;
        var artistName = artist?.Trim() ?? string.Empty;
        var hasTitle = !string.IsNullOrEmpty(title);
        var hasArtist = !string.IsNullOrEmpty(artistName);

        if (!hasTitle && !hasArtist)
            return tracks;

        if (hasTitle && hasArtist)
        {
            return tracks
                .Where(t => LibraryTrackMatcher.TitleMatches(title, t.Title)
                         && LibraryTrackMatcher.ArtistQueryMatches(artistName, t.Artist, t.AlbumArtist))
                .ToList();
        }

        if (hasArtist)
        {
            return tracks
                .Where(t => LibraryTrackMatcher.ArtistQueryMatches(artistName, t.Artist, t.AlbumArtist))
                .ToList();
        }

        return tracks;
    }
}
