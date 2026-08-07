using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>酷狗音乐（搜索对齐 guohuiyuan/music-lib：songsearch.kugou.com）</summary>
public sealed class KugouMusicProvider : BaseApiMusicProvider
{
    public override MusicPlatform Platform => MusicPlatform.Kugou;

    protected override string ApiBase =>
        Environment.GetEnvironmentVariable("MUSICVAULT_KUGOU_API") ?? "";

    public override bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = "song";
        resourceId = string.Empty;
        var m = Regex.Match(url, @"hash=([a-fA-F0-9]+)", RegexOptions.IgnoreCase);
        if (m.Success) { resourceId = m.Groups[1].Value; return true; }
        return false;
    }

    public override async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(ApiBase))
            return await SearchViaProxyApiAsync(keyword, page, ct).ConfigureAwait(false);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var url =
            "http://songsearch.kugou.com/song_search_v2?" +
            $"keyword={Uri.EscapeDataString(keyword)}" +
            "&platform=WebFilter&format=json" +
            $"&page={page}&pagesize=30" +
            "&userid=-1&clientver=&tag=em&filter=2&iscorrection=1&privilege_filter=0" +
            $"&_={ts}";

        var json = await MusicSearchHttp.GetStringWithUserAgentAsync(url, MusicSearchHttp.MobileUserAgent, ct)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("lists", out var lists)
            || lists.ValueKind != JsonValueKind.Array)
            return [];

        return lists.EnumerateArray().Select(ParseSearchItem).Where(t => !string.IsNullOrWhiteSpace(t.Id)).ToList();
    }

    public override async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
    {
        var stub = new OnlineTrack
        {
            Id = trackId,
            Platform = MusicPlatform.Kugou,
            Title = "track",
            Artist = "artist"
        };
        var viaUnified = await GoMusicApiClient.TryResolveDownloadUrlAsync(stub, quality, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(viaUnified)) return viaUnified;

        if (!string.IsNullOrEmpty(ApiBase))
        {
            var url = $"{ApiBase}/song/url?hash={trackId}";
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("url").GetString()
                ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Kugou.LinkFailed"));
        }

        throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Kugou.UnifiedApiRequired"));
    }

    private async Task<IReadOnlyList<OnlineTrack>> SearchViaProxyApiAsync(string keyword, int page, CancellationToken ct)
    {
        var url = $"{ApiBase.TrimEnd('/')}/search?keyword={Uri.EscapeDataString(keyword)}&page={page}";
        var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("info", out var songs))
            return [];
        return songs.EnumerateArray().Select(ParseLegacyMobileItem).ToList();
    }

    private static OnlineTrack ParseSearchItem(JsonElement s)
    {
        var fileHash = s.TryGetProperty("FileHash", out var fh) ? fh.GetString() : null;
        var sqHash = s.TryGetProperty("SQFileHash", out var sq) ? sq.GetString() : null;
        var hqHash = s.TryGetProperty("HQFileHash", out var hq) ? hq.GetString() : null;
        var id = firstHash(fileHash, sqHash, hqHash,
            s.TryGetProperty("ResFileHash", out var rh) ? rh.GetString() : null);

        return new OnlineTrack
        {
            Id = id,
            Platform = MusicPlatform.Kugou,
            Title = clean(s.TryGetProperty("SongName", out var sn) ? sn.GetString() : null),
            Artist = clean(s.TryGetProperty("SingerName", out var singer) ? singer.GetString() : null),
            Album = clean(s.TryGetProperty("AlbumName", out var al) ? al.GetString() : null),
            DurationMs = s.TryGetProperty("Duration", out var d) ? d.GetInt32() * 1000 : 0,
            CoverUrl = s.TryGetProperty("Image", out var img) ? img.GetString()?.Replace("{size}", "240") : null,
            HasFlac = !string.IsNullOrWhiteSpace(sqHash)
        };
    }

    private static OnlineTrack ParseLegacyMobileItem(JsonElement s)
    {
        var sq = s.TryGetProperty("sqhash", out var sqh) ? sqh.GetString() : null;
        return new OnlineTrack
        {
            Id = s.GetProperty("hash").GetString() ?? "",
            Platform = MusicPlatform.Kugou,
            Title = s.TryGetProperty("songname", out var sn) ? sn.GetString() ?? ""
                : s.TryGetProperty("filename", out var fn) ? fn.GetString()?.Split(" - ").LastOrDefault() ?? "" : "",
            Artist = s.TryGetProperty("singername", out var singer) ? singer.GetString() ?? "" : "",
            Album = s.TryGetProperty("album_name", out var al) ? al.GetString() ?? "" : "",
            DurationMs = s.TryGetProperty("duration", out var d) ? d.GetInt32() * 1000 : 0,
            HasFlac = !string.IsNullOrWhiteSpace(sq)
        };
    }

    private static string firstHash(params string?[] hashes)
    {
        foreach (var h in hashes)
            if (!string.IsNullOrWhiteSpace(h)) return h;
        return "";
    }

    private static string clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = System.Net.WebUtility.HtmlDecode(value);
        return Regex.Replace(value, "<[^>]*>", "").Trim();
    }
}
