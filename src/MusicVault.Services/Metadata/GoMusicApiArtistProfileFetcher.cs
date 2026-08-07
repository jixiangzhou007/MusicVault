using MusicVault.Core.Artists;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;

namespace MusicVault.Services.Metadata;

/// <summary>通过 go-music-api 解析歌手 ID 与专辑目录（专辑搜索按 creator 过滤 + 歌曲聚合兜底）。</summary>
public sealed class GoMusicApiArtistProfileFetcher
{
    public bool IsAvailable => GoMusicApiClient.IsConfigured;

    public async Task<string?> ResolveNeteaseArtistIdAsync(string artistName, CancellationToken ct = default)
    {
        var neteaseApi = MusicServiceEndpoints.ResolveNetEaseApi();
        if (!string.IsNullOrWhiteSpace(neteaseApi))
        {
            var fromApi = await NetEaseArtistIdResolver.SearchArtistIdAsync(neteaseApi, artistName, ct)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromApi))
                return fromApi;
        }

        var matched = await SearchMatchedSongsAsync(artistName, ct).ConfigureAwait(false);
        return matched
            .Where(t => t.Platform == MusicPlatform.NetEase && !string.IsNullOrWhiteSpace(t.ArtistId))
            .Where(t => ArtistNameMatcher.Similarity(artistName, t.Artist) >= 85)
            .OrderByDescending(t => ArtistNameMatcher.Similarity(artistName, t.Artist))
            .Select(t => t.ArtistId)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<OnlineArtistAlbum>> FetchAlbumsAsync(string artistName, CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured)
            return [];

        var fromAlbumSearch = await GoMusicApiAlbumSearch.SearchByArtistAsync(artistName, ct).ConfigureAwait(false);
        if (fromAlbumSearch.Count >= 2)
            return fromAlbumSearch;

        var matched = await SearchMatchedSongsAsync(artistName, ct).ConfigureAwait(false);
        if (matched.Count == 0)
            return fromAlbumSearch;

        var fromSongs = BuildAlbumCatalog(matched);
        if (fromSongs.Count >= fromAlbumSearch.Count)
            return fromSongs;

        return fromAlbumSearch.Count > 0 ? fromAlbumSearch : fromSongs;
    }

    private static async Task<List<OnlineTrack>> SearchMatchedSongsAsync(string artistName, CancellationToken ct)
    {
        var (tracks, _) = await GoMusicApiClient.SearchAsync(artistName, MusicPlatform.NetEase, ct)
            .ConfigureAwait(false);

        return tracks
            .Where(t => IsPrimaryArtist(t, artistName))
            .OrderByDescending(t => ArtistNameMatcher.Similarity(artistName, t.Artist))
            .ToList();
    }

    private static bool IsPrimaryArtist(OnlineTrack track, string artistName)
    {
        if (ArtistNameMatcher.IsCreatorMatch(artistName, track.Artist))
            return true;
        if (!string.IsNullOrWhiteSpace(track.AlbumArtist)
            && ArtistNameMatcher.IsCreatorMatch(artistName, track.AlbumArtist))
            return true;
        return false;
    }

    private static IReadOnlyList<OnlineArtistAlbum> BuildAlbumCatalog(IEnumerable<OnlineTrack> matched)
    {
        var map = new Dictionary<string, OnlineArtistAlbum>(StringComparer.OrdinalIgnoreCase);

        foreach (var song in matched)
        {
            if (string.IsNullOrWhiteSpace(song.Album))
                continue;

            var albumName = song.Album.Trim();
            if (map.TryGetValue(albumName, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.PicUrl) && !string.IsNullOrWhiteSpace(song.CoverUrl))
                    map[albumName] = existing with { PicUrl = song.CoverUrl };
                continue;
            }

            map[albumName] = new OnlineArtistAlbum(
                albumName,
                song.CoverUrl,
                song.Year,
                string.IsNullOrWhiteSpace(song.AlbumId) ? null : song.AlbumId);
        }

        return map.Values
            .OrderByDescending(a => a.Year ?? 0)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
