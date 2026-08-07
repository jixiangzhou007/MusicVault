using System.Net.Http.Json;

using System.Text.Json;

using System.Text.RegularExpressions;

using MusicVault.Core.Enums;

using MusicVault.Core.Models;

using MusicVault.Services.Api;
using MusicVault.Services;

namespace MusicVault.Services.Providers;



/// <summary>

/// QQ音乐提供者。需配置 QQ 音乐 API 服务（如 jsososo/QQMusicApi）。

/// </summary>

public sealed class QQMusicProvider : BaseApiMusicProvider

{

    public override MusicPlatform Platform => MusicPlatform.QQMusic;



    protected override string ApiBase =>

        MusicServiceEndpoints.ResolveQQApi() ?? MusicServiceEndpoints.DefaultQQMusicApiUrl;



    public override bool TryParseLink(string url, out string resourceType, out string resourceId)

    {

        resourceType = string.Empty;

        resourceId = string.Empty;

        if (!url.Contains("y.qq.com", StringComparison.OrdinalIgnoreCase)) return false;



        var song = Regex.Match(url, @"songmid=([\w-]+)|songid=(\d+)");

        if (song.Success)

        {

            resourceType = "song";

            resourceId = song.Groups[1].Success ? song.Groups[1].Value : song.Groups[2].Value;

            return true;

        }

        var playlist = Regex.Match(url, @"id=(\d+).*playlist|playlist.*id=(\d+)");

        if (playlist.Success)

        {

            resourceType = "playlist";

            resourceId = playlist.Groups[1].Success ? playlist.Groups[1].Value : playlist.Groups[2].Value;

            return true;

        }

        return false;

    }



    public override async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)

    {

        var paths = new[]

        {

            $"search?key={Uri.EscapeDataString(keyword)}&pageSize=30&pageNum={page}",

            $"search?keyword={Uri.EscapeDataString(keyword)}&pageSize=30&pageNum={page}",

            $"search/song?key={Uri.EscapeDataString(keyword)}&pageSize=30&pageNum={page}"

        };



        foreach (var path in paths)

        {

            try

            {

                var doc = await Http.GetFromJsonAsync<JsonDocument>($"{ApiBase.TrimEnd('/')}/{path}", ct)

                    .ConfigureAwait(false);

                if (doc == null)

                    continue;



                var list = ExtractSongList(doc.RootElement);

                if (list == null)

                    continue;



                var tracks = list.Value.EnumerateArray()

                    .Select(ParseSearchItem)

                    .Where(t => !string.IsNullOrWhiteSpace(t.Title))

                    .ToList();

                if (tracks.Count > 0)

                    return tracks;

            }

            catch

            {

                // try next endpoint shape

            }

        }



        return [];

    }



    public override async Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(trackId))

            return null;



        var id = trackId.Trim();

        var urls = LooksLikeSongMid(id)

            ? new[]

            {

                $"song?songmid={Uri.EscapeDataString(id)}",

                $"song?id={Uri.EscapeDataString(id)}",

                $"song?songid={Uri.EscapeDataString(id)}"

            }

            : new[]

            {

                $"song?songid={Uri.EscapeDataString(id)}",

                $"song?id={Uri.EscapeDataString(id)}",

                $"song?songmid={Uri.EscapeDataString(id)}"

            };



        foreach (var path in urls)

        {

            try

            {

                var doc = await Http.GetFromJsonAsync<JsonDocument>($"{ApiBase.TrimEnd('/')}/{path}", ct)

                    .ConfigureAwait(false);

                if (doc == null)

                    continue;



                if (TryParseTrackDetail(doc.RootElement, out var track))

                    return track;

            }

            catch

            {

                // try next

            }

        }



        return null;

    }



    public override async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)

    {

        var url = $"{ApiBase.TrimEnd('/')}/song/url?id={trackId}&type=128";

        if (quality == AudioQuality.High320) url = $"{ApiBase.TrimEnd('/')}/song/url?id={trackId}&type=320";

        if (quality == AudioQuality.Flac) url = $"{ApiBase.TrimEnd('/')}/song/url?id={trackId}&type=flac";



        var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);

        var songUrl = doc?.RootElement.GetProperty("data").GetString();

        if (string.IsNullOrEmpty(songUrl))

            throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.QQ.NoDownloadLink"));

        return songUrl;

    }



    /// <summary>搜索摘要缺年份/封面时，用专辑详情补全。</summary>

    public async Task<OnlineTrack> TryEnrichFromAlbumAsync(OnlineTrack track, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(track.AlbumId))

            return track;



        var albumId = track.AlbumId.Trim();

        var paths = new[]

        {

            $"album?id={Uri.EscapeDataString(albumId)}",

            $"album?albumid={Uri.EscapeDataString(albumId)}",

            $"album?albummid={Uri.EscapeDataString(albumId)}"

        };



        foreach (var path in paths)

        {

            try

            {

                var doc = await Http.GetFromJsonAsync<JsonDocument>($"{ApiBase.TrimEnd('/')}/{path}", ct)

                    .ConfigureAwait(false);

                if (doc == null)

                    continue;



                if (!TryGetDataObject(doc.RootElement, out var data))

                    continue;



                if (track.Year is not > 0)

                {

                    var year = TryReadYear(data);

                    if (year is > 0)

                        track.Year = year;

                }



                if (string.IsNullOrWhiteSpace(track.CoverUrl))

                    track.CoverUrl = ReadPicUrl(data);



                if (string.IsNullOrWhiteSpace(track.Genre)

                    && data.TryGetProperty("genre", out var genreEl))

                {

                    var g = genreEl.GetString();

                    if (!string.IsNullOrWhiteSpace(g))

                        track.Genre = g.Trim();

                }



                if (track.TrackNumber is not > 0 && TryGetSongsArray(data, out var songs))

                {

                    var index = 1;

                    foreach (var song in songs.EnumerateArray())

                    {

                        var songId = ReadSongId(song);

                        var songMid = ReadString(song, "songmid", "mid");

                        var idMatch = !string.IsNullOrEmpty(track.Id)

                            && (string.Equals(songId, track.Id, StringComparison.OrdinalIgnoreCase)

                                || string.Equals(songMid, track.Id, StringComparison.OrdinalIgnoreCase));

                        var title = ReadString(song, "name", "songname", "title");

                        var titleMatch = !string.IsNullOrEmpty(title)

                            && !string.IsNullOrEmpty(track.Title)

                            && string.Equals(Normalize(title), Normalize(track.Title), StringComparison.Ordinal);



                        if (idMatch || titleMatch)

                        {

                            if (song.TryGetProperty("index_album", out var idx) && idx.TryGetInt32(out var tn) && tn > 0)

                                track.TrackNumber = tn;

                            else if (song.TryGetProperty("index", out var idx2) && idx2.TryGetInt32(out var tn2) && tn2 > 0)

                                track.TrackNumber = tn2;

                            else

                                track.TrackNumber = index;

                            break;

                        }



                        index++;

                    }

                }



                if (track.Year is > 0 || !string.IsNullOrWhiteSpace(track.CoverUrl))

                    return track;

            }

            catch

            {

                // try next path

            }

        }



        return track;

    }



    private static bool LooksLikeSongMid(string id) =>

        id.Any(char.IsLetter) && !id.All(char.IsDigit);



    private static JsonElement? ExtractSongList(JsonElement root)

    {

        if (root.TryGetProperty("data", out var data))

        {

            if (data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)

                return list;

            if (data.TryGetProperty("song", out var song) && song.ValueKind == JsonValueKind.Object

                && song.TryGetProperty("list", out var songList) && songList.ValueKind == JsonValueKind.Array)

                return songList;

            if (data.TryGetProperty("songlist", out var songlist) && songlist.ValueKind == JsonValueKind.Array)

                return songlist;

        }



        if (root.TryGetProperty("list", out var rootList) && rootList.ValueKind == JsonValueKind.Array)

            return rootList;



        return null;

    }



    private static bool TryParseTrackDetail(JsonElement root, out OnlineTrack track)

    {

        track = new OnlineTrack();

        if (TryGetDataObject(root, out var data))

        {

            if (data.TryGetProperty("track_info", out var info) && info.ValueKind == JsonValueKind.Object)

            {

                track = ParseSearchItem(info);

                return !string.IsNullOrWhiteSpace(track.Title);

            }



            if (data.TryGetProperty("info", out var info2) && info2.ValueKind == JsonValueKind.Object)

            {

                track = ParseSearchItem(info2);

                return !string.IsNullOrWhiteSpace(track.Title);

            }



            track = ParseSearchItem(data);

            return !string.IsNullOrWhiteSpace(track.Title);

        }



        track = ParseSearchItem(root);

        return !string.IsNullOrWhiteSpace(track.Title);

    }



    private static bool TryGetDataObject(JsonElement root, out JsonElement data)

    {

        if (root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object)

            return true;

        data = root;

        return data.ValueKind == JsonValueKind.Object;

    }



    private static bool TryGetSongsArray(JsonElement data, out JsonElement songs)

    {

        if (data.TryGetProperty("list", out songs) && songs.ValueKind == JsonValueKind.Array)

            return true;

        if (data.TryGetProperty("songs", out songs) && songs.ValueKind == JsonValueKind.Array)

            return true;

        if (data.TryGetProperty("songlist", out songs) && songs.ValueKind == JsonValueKind.Array)

            return true;

        songs = default;

        return false;

    }



    private static int ParseDuration(string? interval)

    {

        if (string.IsNullOrEmpty(interval)) return 0;

        var parts = interval.Split(':');

        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s))

            return (m * 60 + s) * 1000;

        return 0;

    }



    private static OnlineTrack ParseSearchItem(JsonElement s)

    {

        var artist = string.Empty;

        var albumArtist = string.Empty;

        if (s.TryGetProperty("singer", out var singers) && singers.ValueKind == JsonValueKind.Array

            && singers.GetArrayLength() > 0)

        {

            var names = singers.EnumerateArray()

                .Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null)

                .Where(x => !string.IsNullOrWhiteSpace(x))

                .ToList();

            if (names.Count > 0)

            {

                artist = names[0]!;

                albumArtist = string.Join(" / ", names);

            }

        }



        int? year = null;

        string? cover = null;

        var albumId = string.Empty;

        var album = string.Empty;

        if (s.TryGetProperty("album", out var al))

        {

            if (al.ValueKind == JsonValueKind.Object)

            {

                album = ReadString(al, "name", "albumname", "title");

                if (al.TryGetProperty("time_public", out var tp) && tp.GetString() is { Length: >= 4 } yp

                    && int.TryParse(yp[..4], out var y) && y > 0)

                    year = y;

                else

                    year = TryReadYear(al);



                cover = ReadPicUrl(al);

                if (al.TryGetProperty("id", out var alId))

                    albumId = alId.ValueKind == JsonValueKind.Number ? alId.GetInt32().ToString() : alId.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(albumId) && al.TryGetProperty("mid", out var alMid))

                    albumId = alMid.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(albumId) && al.TryGetProperty("albumMID", out var alMid2))

                    albumId = alMid2.GetString() ?? "";

            }

            else if (al.ValueKind == JsonValueKind.String)

            {

                album = al.GetString() ?? "";

            }

        }



        if (string.IsNullOrWhiteSpace(album))

            album = ReadString(s, "albumname", "album_name");



        int? trackNo = null;

        if (s.TryGetProperty("index_album", out var idx) && idx.TryGetInt32(out var tn) && tn > 0)

            trackNo = tn;

        if (trackNo is null or <= 0 && s.TryGetProperty("index", out var idx2) && idx2.TryGetInt32(out var tn2) && tn2 > 0)

            trackNo = tn2;

        if (trackNo is null or <= 0 && s.TryGetProperty("track", out var tr) && tr.TryGetInt32(out var tn3) && tn3 > 0)

            trackNo = tn3;



        var songMid = ReadString(s, "songmid", "mid");

        var songId = ReadString(s, "id", "songid", "song_id");

        var id = !string.IsNullOrWhiteSpace(songMid) ? songMid : songId;



        if (string.IsNullOrWhiteSpace(cover))

            cover = ReadPicUrl(s);



        return new OnlineTrack

        {

            Id = id,

            Platform = MusicPlatform.QQMusic,

            Title = ReadString(s, "name", "songname", "title", "song_name"),

            Artist = artist,

            AlbumArtist = albumArtist,

            Album = album,

            AlbumId = albumId,

            TrackNumber = trackNo,

            Year = year,

            DurationMs = s.TryGetProperty("interval", out var iv) ? ParseDuration(iv.GetString()) : 0,

            CoverUrl = cover,

            HasFlac = QqHasLossless(s)
        };
    }

    private static bool QqHasLossless(JsonElement s)
    {
        foreach (var key in new[] { "sizeflac", "size_flac", "filesize_flac" })
        {
            if (s.TryGetProperty(key, out var el) && TryReadPositiveSize(el))
                return true;
        }

        if (s.TryGetProperty("file", out var file) && file.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "size_flac", "sizeflac" })
            {
                if (file.TryGetProperty(key, out var el) && TryReadPositiveSize(el))
                    return true;
            }
        }

        return false;
    }

    private static bool TryReadPositiveSize(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out var n) && n > 0,
            JsonValueKind.String => long.TryParse(el.GetString(), out var parsed) && parsed > 0,
            _ => false
        };



    private static int? TryReadYear(JsonElement el)

    {

        if (el.TryGetProperty("time_public", out var tp) && tp.GetString() is { Length: >= 4 } yp

            && int.TryParse(yp[..4], out var y) && y > 0)

            return y;

        if (el.TryGetProperty("pubTime", out var pt) && pt.GetString() is { Length: >= 4 } yp2

            && int.TryParse(yp2[..4], out var y2) && y2 > 0)

            return y2;

        if (el.TryGetProperty("aDate", out var ad) && ad.GetString() is { Length: >= 4 } yp3

            && int.TryParse(yp3[..4], out var y3) && y3 > 0)

            return y3;

        return null;

    }



    private static string? ReadPicUrl(JsonElement el)

    {

        foreach (var key in new[] { "pic", "picUrl", "albumPic", "img", "cover", "album_pic" })

        {

            if (!el.TryGetProperty(key, out var pic))

                continue;

            var url = pic.GetString();

            if (!string.IsNullOrWhiteSpace(url))

                return url;

        }



        return null;

    }



    private static string ReadSongId(JsonElement song)

    {

        if (song.TryGetProperty("id", out var id))

            return id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString() : id.GetString() ?? "";

        if (song.TryGetProperty("songid", out var sid))

            return sid.ValueKind == JsonValueKind.Number ? sid.GetInt64().ToString() : sid.GetString() ?? "";

        return string.Empty;

    }



    private static string ReadString(JsonElement el, params string[] names)

    {

        foreach (var name in names)

        {

            if (!el.TryGetProperty(name, out var prop))

                continue;

            if (prop.ValueKind == JsonValueKind.String)

            {

                var s = prop.GetString();

                if (!string.IsNullOrWhiteSpace(s))

                    return s.Trim();

            }

            else if (prop.ValueKind == JsonValueKind.Number)

            {

                return prop.GetRawText();

            }

        }



        return string.Empty;

    }



    private static string Normalize(string? value) =>

        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

}


