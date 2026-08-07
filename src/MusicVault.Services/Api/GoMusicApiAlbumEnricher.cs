using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Http;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Api;

/// <summary>通过 go-music-api 专辑详情补全年份、曲目号、风格（不依赖网易云 3000 端口）。</summary>
public static class GoMusicApiAlbumEnricher
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static async Task<OnlineTrack> TryEnrichFromAlbumDetailAsync(
        OnlineTrack track,
        CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured)
            return track;

        var albumId = track.AlbumId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(albumId) && !string.IsNullOrWhiteSpace(track.Album))
            albumId = await TryResolveAndApplyAlbumSearchAsync(track, ct).ConfigureAwait(false) ?? string.Empty;

        if (string.IsNullOrEmpty(albumId))
            return track;

        var baseUrl = MusicApiConfig.Resolve();
        var source = MapPlatformToSource(track.Platform);
        if (string.IsNullOrEmpty(baseUrl) || source == null)
            return track;

        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/v1/album/detail?source={Uri.EscapeDataString(source)}&id={Uri.EscapeDataString(albumId)}";
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return track;

            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                return track;

            if (!root.TryGetProperty("data", out var data))
                data = root;

            ApplyAlbumFields(track, data, albumId);

            if (track.Year is not > 0 && !string.IsNullOrWhiteSpace(track.Album))
                await TryResolveAndApplyAlbumSearchAsync(track, ct).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        return track;
    }

    private static void ApplyAlbumFields(OnlineTrack track, JsonElement data, string albumId)
    {
        if (string.IsNullOrWhiteSpace(track.AlbumId))
            track.AlbumId = albumId;

        JsonElement? albumNode = null;
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
            albumNode = album;

        if (albumNode.HasValue)
        {
            if (track.Year is not > 0)
            {
                var year = TryReadYear(albumNode.Value);
                if (year is > 0)
                    track.Year = year;
            }

            if (string.IsNullOrWhiteSpace(track.CoverUrl)
                && albumNode.Value.TryGetProperty("picUrl", out var pic))
                track.CoverUrl = pic.GetString();
            if (string.IsNullOrWhiteSpace(track.CoverUrl)
                && albumNode.Value.TryGetProperty("cover", out var cov))
                track.CoverUrl = cov.GetString();

            if (string.IsNullOrWhiteSpace(track.Genre))
            {
                if (albumNode.Value.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    var tokens = tags.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => MetadataGenreHeuristic.Sanitize(x!.Trim(), track.Title))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(4)
                        .ToList();
                    if (tokens.Count > 0)
                        track.Genre = string.Join(" / ", tokens);
                }
                else if (albumNode.Value.TryGetProperty("genre", out var genreEl))
                {
                    var g = genreEl.GetString();
                    if (!string.IsNullOrWhiteSpace(g))
                        track.Genre = MetadataGenreHeuristic.Sanitize(g.Trim(), track.Title);
                }
            }
        }

        if (track.Year is not > 0 && data.ValueKind == JsonValueKind.Object)
        {
            var y = TryReadYear(data);
            if (y is > 0)
                track.Year = y;
        }

        if (track.Year is not > 0)
            TryApplyYearFromSongList(data, track);

        if (track.TrackNumber is not > 0 || string.IsNullOrWhiteSpace(track.Genre))
            TryApplyTrackIndexFromSongList(track, data);
    }

    private static void TryApplyYearFromSongList(JsonElement data, OnlineTrack track)
    {
        if (!TryGetSongsArray(data, out var songs) || songs.GetArrayLength() == 0)
            return;

        var first = songs[0];
        var year = TryReadYear(first);
        if (year is not > 0 && first.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
            year = TryReadYear(extra);
        if (year is > 0)
            track.Year = year;
    }

    private static void TryApplyTrackIndexFromSongList(OnlineTrack track, JsonElement data)
    {
        if (!TryGetSongsArray(data, out var songs))
            return;

        var index = 1;
        foreach (var song in songs.EnumerateArray())
        {
            var songId = ReadSongId(song);
            var songTitle = song.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

            var idMatch = TrackMatchesSongId(track, song, songId);
            var titleMatch = !string.IsNullOrEmpty(songTitle)
                && !string.IsNullOrEmpty(track.Title)
                && string.Equals(Normalize(songTitle), Normalize(track.Title), StringComparison.Ordinal);

            if (idMatch || titleMatch)
            {
                if (track.TrackNumber is not > 0)
                {
                    if (song.TryGetProperty("no", out var noEl) && noEl.TryGetInt32(out var no) && no > 0)
                        track.TrackNumber = no;
                    else
                        track.TrackNumber = index;
                }

                if (string.IsNullOrWhiteSpace(track.Genre) && song.TryGetProperty("genre", out var genreEl))
                {
                    var g = genreEl.GetString();
                    if (!string.IsNullOrWhiteSpace(g))
                        track.Genre = MetadataGenreHeuristic.Sanitize(g.Trim(), track.Title);
                }

                if (string.IsNullOrWhiteSpace(track.Composer))
                    track.Composer = ReadComposerFromSong(song);

                if (track.DiscNumber is not > 0 && song.TryGetProperty("cd", out var cd)
                    && cd.TryGetProperty("no", out var cdNo) && cdNo.TryGetInt32(out var disc) && disc > 0)
                    track.DiscNumber = disc;

                break;
            }

            index++;
        }
    }

    private static bool TryGetSongsArray(JsonElement data, out JsonElement songs)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            songs = data;
            return true;
        }

        if (data.TryGetProperty("songs", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;
        if (data.TryGetProperty("tracks", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;
        if (data.TryGetProperty("songList", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;
        songs = default;
        return false;
    }

    private static async Task<string?> TryResolveAndApplyAlbumSearchAsync(OnlineTrack track, CancellationToken ct)
    {
        var source = MapPlatformToSource(track.Platform);
        var baseUrl = MusicApiConfig.Resolve();
        if (source == null || string.IsNullOrEmpty(baseUrl))
            return null;

        var q = track.Album.Trim();
        if (!string.IsNullOrWhiteSpace(track.Artist))
            q = $"{track.Artist.Trim()} {q}";

        var url = $"{baseUrl.TrimEnd('/')}/api/v1/music/search?type=album&q={Uri.EscapeDataString(q)}&sources={Uri.EscapeDataString(source)}";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc?.RootElement.TryGetProperty("data", out var data) != true)
                return null;

            JsonElement list = default;
            if (data.TryGetProperty("albums", out var albums) && albums.ValueKind == JsonValueKind.Array)
                list = albums;
            else if (data.TryGetProperty("songs", out var songs) && songs.ValueKind == JsonValueKind.Array)
                list = songs;

            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                return null;

            var targetAlbum = Normalize(track.Album);
            var targetArtist = Normalize(track.Artist);
            JsonElement? best = null;
            var bestScore = int.MinValue;
            foreach (var item in list.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? ""
                    : item.TryGetProperty("album", out var al) ? al.GetString() ?? "" : "";
                var creator = item.TryGetProperty("creator", out var cr) ? cr.GetString() ?? "" : "";
                var score = 0;
                var key = Normalize(name);
                if (!string.IsNullOrEmpty(targetAlbum))
                {
                    if (key == targetAlbum)
                        score += 80;
                    else if (key.Contains(targetAlbum, StringComparison.Ordinal)
                             || targetAlbum.Contains(key, StringComparison.Ordinal))
                        score += 40;
                }
                else
                    score += 10;

                if (!string.IsNullOrEmpty(targetArtist))
                {
                    var creatorKey = Normalize(creator);
                    if (creatorKey == targetArtist)
                        score += 60;
                    else if (creatorKey.Contains(targetArtist, StringComparison.Ordinal)
                             || targetArtist.Contains(creatorKey, StringComparison.Ordinal))
                        score += 30;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }

            var pick = best ?? list[0];
            if (track.Year is not > 0)
            {
                var year = TryReadYear(pick);
                if (year is not > 0 && pick.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
                    year = TryReadYear(extra);
                if (year is > 0)
                    track.Year = year;
            }

            if (string.IsNullOrWhiteSpace(track.CoverUrl))
            {
                if (pick.TryGetProperty("cover", out var coverEl))
                    track.CoverUrl = coverEl.GetString();
                else if (pick.TryGetProperty("picUrl", out var picEl))
                    track.CoverUrl = picEl.GetString();
            }

            if (pick.TryGetProperty("id", out var idEl))
            {
                return idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt64().ToString()
                    : idEl.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static int? TryReadYear(JsonElement el)
    {
        foreach (var name in new[] { "publishTime", "pubTime", "time_public", "publish_time", "year", "releaseYear" })
        {
            if (!el.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number)
            {
                var v = prop.GetInt64();
                if (v > 1_000_000_000_000)
                    return DateTimeOffset.FromUnixTimeMilliseconds(v).Year;
                if (v > 9999)
                    return (int)(v / 10_000);
                if (v is > 1900 and < 2100)
                    return (int)v;
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                if (long.TryParse(s, out var numeric))
                {
                    if (numeric > 1_000_000_000_000)
                        return DateTimeOffset.FromUnixTimeMilliseconds(numeric).Year;
                    if (numeric > 9999)
                        return (int)(numeric / 10_000);
                    if (numeric is > 1900 and < 2100)
                        return (int)numeric;
                }

                if (s.Length >= 4 && int.TryParse(s[..4], out var y) && y > 1900)
                    return y;
            }
        }

        return null;
    }

    private static string ReadSongId(JsonElement song)
    {
        if (song.TryGetProperty("id", out var id))
            return id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString() : id.GetString() ?? "";
        if (song.TryGetProperty("songmid", out var mid))
            return mid.GetString() ?? "";
        if (song.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            if (extra.TryGetProperty("song_id", out var sid))
                return sid.ValueKind == JsonValueKind.Number ? sid.GetInt64().ToString() : sid.GetString() ?? "";
            if (extra.TryGetProperty("songmid", out var smid))
                return smid.GetString() ?? "";
        }

        return string.Empty;
    }

    private static bool TrackMatchesSongId(OnlineTrack track, JsonElement song, string songId)
    {
        if (string.IsNullOrEmpty(track.Id))
            return false;

        if (!string.IsNullOrEmpty(songId)
            && string.Equals(songId, track.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(ReadSongId(song), track.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        if (song.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "song_id", "songmid", "song_mid" })
            {
                if (!extra.TryGetProperty(key, out var el))
                    continue;
                var value = el.ValueKind == JsonValueKind.Number
                    ? el.GetInt64().ToString()
                    : el.GetString() ?? "";
                if (!string.IsNullOrEmpty(value)
                    && string.Equals(value, track.Id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static string? MapPlatformToSource(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "netease",
        MusicPlatform.QQMusic => "qq",
        MusicPlatform.Kugou => "kugou",
        MusicPlatform.Kuwo => "kuwo",
        MusicPlatform.Migu => "migu",
        _ => null
    };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string ReadComposerFromSong(JsonElement song)
    {
        if (song.TryGetProperty("composer", out var c) && !string.IsNullOrWhiteSpace(c.GetString()))
            return c.GetString()!.Trim();

        if (song.TryGetProperty("composers", out var composers) && composers.ValueKind == JsonValueKind.Array)
        {
            var names = composers.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String
                    ? e.GetString()
                    : e.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (names.Count > 0)
                return string.Join(" / ", names!);
        }

        return string.Empty;
    }
}
