using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>网易云音乐（需配置可用的 NeteaseCloudMusicApi 地址）</summary>
public sealed class NetEaseMusicProvider : IMusicSourceProvider
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public MusicPlatform Platform => MusicPlatform.NetEase;

    public async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
    {
        var apiBase = ResolveApiBase();
        if (apiBase == null)
            return [];

        var offset = (page - 1) * 30;
        var paths = new[]
        {
            $"cloudsearch?keywords={Uri.EscapeDataString(keyword)}&type=1&limit=30&offset={offset}",
            $"search?keywords={Uri.EscapeDataString(keyword)}&type=1&limit=30&offset={offset}"
        };

        Exception? last = null;
        foreach (var path in paths)
        {
            try
            {
                var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/{path}", ct).ConfigureAwait(false);
                if (doc == null) continue;

                if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                    continue;

                if (!doc.RootElement.TryGetProperty("result", out var result)) continue;
                if (!result.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
                    continue;

                var list = songs.EnumerateArray().Select(ParseSong).Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { last = ex; }
        }

        throw last ?? new InvalidOperationException(ServiceMessageCodec.Key("Provider.NetEase.NoSongs"));
    }

    public async Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default)
    {
        var apiBase = ResolveApiBase();
        if (apiBase == null) return null;
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/song/detail?ids={trackId}", ct).ConfigureAwait(false);
            if (doc?.RootElement.TryGetProperty("songs", out var songs) == true
                && songs.ValueKind == JsonValueKind.Array
                && songs.GetArrayLength() > 0)
                return ParseSong(songs[0]);
        }
        catch { /* ignore */ }
        return null;
    }

    public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct) =>
        FetchListAsync($"album?id={albumId}", "songs", ct);

    public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct) =>
        FetchListAsync($"artist/top/song?id={artistId}", "songs", ct);

    public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct) =>
        FetchPlaylistTracksAsync(playlistId, ct);

    private async Task<IReadOnlyList<OnlineTrack>> FetchPlaylistTracksAsync(string playlistId, CancellationToken ct)
    {
        var songs = await FetchListAsync($"playlist/detail?id={playlistId}", "songs", ct).ConfigureAwait(false);
        if (songs.Count > 0) return songs;

        songs = await FetchPlaylistTracksFromDetailAsync(playlistId, ct).ConfigureAwait(false);
        if (songs.Count > 0) return songs;

        return await FetchListAsync($"playlist/track/all?id={playlistId}&limit=1000", "songs", ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<OnlineTrack>> FetchPlaylistTracksFromDetailAsync(string playlistId, CancellationToken ct)
    {
        var apiBase = ResolveApiBase();
        if (apiBase == null) return [];

        try
        {
            var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/playlist/detail?id={playlistId}", ct)
                .ConfigureAwait(false);
            if (doc?.RootElement.TryGetProperty("playlist", out var playlist) != true)
                return [];

            if (playlist.TryGetProperty("tracks", out var tracks) && tracks.ValueKind == JsonValueKind.Array)
            {
                var list = tracks.EnumerateArray().Select(ParseSong)
                    .Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
                if (list.Count > 0) return list;
            }

            if (playlist.TryGetProperty("trackIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                var idList = ids.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64().ToString() : e.GetString())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Take(1000)
                    .ToList();
                if (idList.Count > 0)
                    return await FetchSongsByIdsAsync(apiBase, idList, ct).ConfigureAwait(false);
            }
        }
        catch { /* fallback paths above */ }

        return [];
    }

    private async Task<IReadOnlyList<OnlineTrack>> FetchSongsByIdsAsync(
        string apiBase, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var joined = string.Join(",", ids);
        var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/song/detail?ids={joined}", ct)
            .ConfigureAwait(false);
        if (doc?.RootElement.TryGetProperty("songs", out var songs) != true
            || songs.ValueKind != JsonValueKind.Array)
            return [];

        return songs.EnumerateArray().Select(ParseSong).Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
    }

    public async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
    {
        var apiBase = ResolveApiBase()
            ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.NetEase.NotConfigured"));

        var url = $"{apiBase}/song/url/v1?id={trackId}&level={MapLevel(quality)}";
        var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
        var songUrl = doc?.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
        if (string.IsNullOrEmpty(songUrl))
            throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.NetEase.NoDownloadLink"));
        return songUrl;
    }

    public async Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default)
    {
        try { return await ResolveDownloadUrlAsync(trackId, AudioQuality.High320, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    public bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = string.Empty;
        resourceId = string.Empty;
        if (!url.Contains("music.163.com", StringComparison.OrdinalIgnoreCase)) return false;

        var idMatch = System.Text.RegularExpressions.Regex.Match(url, @"id=(\d+)");
        if (!idMatch.Success) return false;

        resourceId = idMatch.Groups[1].Value;
        resourceType = url.Contains("playlist", StringComparison.OrdinalIgnoreCase) ? "playlist"
            : url.Contains("album", StringComparison.OrdinalIgnoreCase) ? "album"
            : url.Contains("artist", StringComparison.OrdinalIgnoreCase) ? "artist" : "song";
        return true;
    }

    private static string? ResolveApiBase() => MusicVault.Services.Api.MusicServiceEndpoints.ResolveNetEaseApi();

    private static string MapLevel(AudioQuality q) => q switch
    {
        AudioQuality.Flac => "lossless",
        AudioQuality.High320 => "exhigh",
        _ => "standard"
    };

    private async Task<IReadOnlyList<OnlineTrack>> FetchListAsync(string path, string prop, CancellationToken ct)
    {
        var apiBase = ResolveApiBase();
        if (apiBase == null) return [];

        var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/{path}", ct).ConfigureAwait(false);
        if (doc == null) return [];
        if (!doc.RootElement.TryGetProperty(prop, out var element) || element.ValueKind != JsonValueKind.Array)
            return [];
        return element.EnumerateArray().Select(ParseSong).ToList();
    }

    internal static OnlineTrack ParseSongElement(JsonElement s) => ParseSong(s);

    private static OnlineTrack ParseSong(JsonElement s)
    {
        var title = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var (artist, albumArtist) = ParseArtists(s);

        JsonElement? albumEl = null;
        if (s.TryGetProperty("al", out var al) && al.ValueKind == JsonValueKind.Object)
            albumEl = al;
        else if (s.TryGetProperty("album", out var albumNode) && albumNode.ValueKind == JsonValueKind.Object)
            albumEl = albumNode;

        var album = albumEl.HasValue && albumEl.Value.TryGetProperty("name", out var aln)
            ? aln.GetString() ?? ""
            : "";

        int? year = null;
        if (albumEl.HasValue && albumEl.Value.TryGetProperty("publishTime", out var pt))
            year = ParsePublishYear(pt);

        int? trackNo = null;
        if (s.TryGetProperty("no", out var noEl) && noEl.TryGetInt32(out var no) && no > 0)
            trackNo = no;

        int? discNo = null;
        if (s.TryGetProperty("cd", out var cdEl) && cdEl.ValueKind == JsonValueKind.Object
            && cdEl.TryGetProperty("no", out var cdNoEl)
            && cdNoEl.TryGetInt32(out var disc) && disc > 0)
            discNo = disc;

        if (year is null or <= 0 && s.TryGetProperty("publishTime", out var songPt))
            year = ParsePublishYear(songPt);

        var albumId = string.Empty;
        if (albumEl.HasValue && albumEl.Value.TryGetProperty("id", out var albumIdEl))
            albumId = albumIdEl.ValueKind == JsonValueKind.Number
                ? albumIdEl.GetInt64().ToString()
                : albumIdEl.GetString() ?? string.Empty;

        var genre = s.TryGetProperty("genre", out var genreEl) ? genreEl.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(genre) && s.TryGetProperty("alia", out var alia)
            && alia.ValueKind == JsonValueKind.Array)
        {
            genre = string.Join(" / ",
                alia.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        var composer = ReadComposer(s);

        return new OnlineTrack
        {
            Id = s.TryGetProperty("id", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt64().ToString()
                    : idEl.GetString() ?? ""
                : "",
            Platform = MusicPlatform.NetEase,
            Title = title,
            Artist = artist,
            AlbumArtist = albumArtist,
            Album = album,
            AlbumId = albumId,
            Composer = composer,
            Year = year,
            Genre = genre.Trim(),
            TrackNumber = trackNo,
            DiscNumber = discNo,
            DurationMs = s.TryGetProperty("dt", out var dt) ? dt.GetInt32() : 0,
            CoverUrl = albumEl.HasValue && albumEl.Value.TryGetProperty("picUrl", out var pic)
                ? pic.GetString()
                : null,
            HasFlac = NetEaseHasLossless(s)
        };
    }

    /// <summary>网易云 maxbr≥999000 表示有无损（999000=FLAC，1999000=Hi-Res）。</summary>
    private static bool NetEaseHasLossless(JsonElement s)
    {
        foreach (var key in new[] { "maxbr", "maxBr" })
        {
            if (s.TryGetProperty(key, out var el) && el.TryGetInt32(out var br) && br >= 999_000)
                return true;
        }

        if (s.TryGetProperty("privilege", out var priv) && priv.ValueKind == JsonValueKind.Object
            && priv.TryGetProperty("maxbr", out var maxbr)
            && maxbr.TryGetInt32(out var pbr) && pbr >= 999_000)
            return true;

        return false;
    }

    private static (string Artist, string AlbumArtist) ParseArtists(JsonElement s)
    {
        if (s.TryGetProperty("ar", out var ar) && ar.ValueKind == JsonValueKind.Array && ar.GetArrayLength() > 0)
        {
            var names = ar.EnumerateArray()
                .Select(a => a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (names.Count == 0)
                return (string.Empty, string.Empty);
            return (names[0], string.Join(" / ", names));
        }

        if (s.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array
            && artists.GetArrayLength() > 0)
        {
            var names = artists.EnumerateArray()
                .Select(a => a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (names.Count == 0)
                return (string.Empty, string.Empty);
            return (names[0], string.Join(" / ", names));
        }

        return (string.Empty, string.Empty);
    }

    private static string ReadComposer(JsonElement s)
    {
        if (s.TryGetProperty("composer", out var c) && !string.IsNullOrWhiteSpace(c.GetString()))
            return c.GetString()!.Trim();

        if (s.TryGetProperty("composers", out var composers) && composers.ValueKind == JsonValueKind.Array)
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

    private static int? ParsePublishYear(JsonElement pt)
    {
        try
        {
            if (pt.ValueKind == JsonValueKind.Number)
            {
                var v = pt.GetInt64();
                if (v > 1_000_000_000_000)
                    return DateTimeOffset.FromUnixTimeMilliseconds(v).Year;
                if (v > 9999)
                    return (int)(v / 10_000);
                if (v is > 1900 and < 2100)
                    return (int)v;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>搜索摘要缺年份/封面时，用专辑详情补全。</summary>
    public async Task<OnlineTrack> TryEnrichFromAlbumAsync(OnlineTrack track, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(track.AlbumId))
            return track;

        var apiBase = ResolveApiBase();
        if (apiBase == null)
            return track;

        try
        {
            var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/album?id={track.AlbumId}", ct)
                .ConfigureAwait(false);
            if (doc?.RootElement.TryGetProperty("album", out var album) != true)
                return track;

            if (track.Year is not > 0 && album.TryGetProperty("publishTime", out var pt))
            {
                var y = ParsePublishYear(pt);
                if (y is > 0)
                    track.Year = y;
            }

            if (string.IsNullOrWhiteSpace(track.CoverUrl) && album.TryGetProperty("picUrl", out var pic))
                track.CoverUrl = pic.GetString();

            if (string.IsNullOrWhiteSpace(track.Genre) && album.TryGetProperty("tags", out var tags)
                && tags.ValueKind == JsonValueKind.Array)
            {
                var tagText = string.Join(" / ",
                    tags.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(tagText))
                    track.Genre = tagText;
            }

            if (album.TryGetProperty("songs", out var songs) && songs.ValueKind == JsonValueKind.Array)
            {
                var index = 1;
                foreach (var song in songs.EnumerateArray())
                {
                    var songId = song.TryGetProperty("id", out var sid)
                        ? sid.GetInt64().ToString()
                        : string.Empty;
                    if (!string.IsNullOrEmpty(track.Id) && string.Equals(songId, track.Id, StringComparison.Ordinal))
                    {
                        if (track.TrackNumber is not > 0)
                        {
                            if (song.TryGetProperty("no", out var noEl) && noEl.TryGetInt32(out var no) && no > 0)
                                track.TrackNumber = no;
                            else
                                track.TrackNumber = index;
                        }

                        if (track.DiscNumber is not > 0 && song.TryGetProperty("cd", out var cd)
                            && cd.TryGetProperty("no", out var cdNo) && cdNo.TryGetInt32(out var disc) && disc > 0)
                            track.DiscNumber = disc;
                        break;
                    }

                    index++;
                }
            }
        }
        catch
        {
            // ignore
        }

        return track;
    }
}
