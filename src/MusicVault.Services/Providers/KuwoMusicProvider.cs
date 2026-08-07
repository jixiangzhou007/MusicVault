using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>酷我音乐（搜索对齐 guohuiyuan/music-lib）</summary>
public sealed class KuwoMusicProvider : BaseApiMusicProvider
{
    public override MusicPlatform Platform => MusicPlatform.Kuwo;

    protected override string ApiBase =>
        Environment.GetEnvironmentVariable("MUSICVAULT_KUWO_API") ?? "";

    public override bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = "song";
        resourceId = string.Empty;
        var m = Regex.Match(url, @"play_detail/(\d+)|id=(\d+)");
        if (m.Success)
        {
            resourceId = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            return url.Contains("kuwo", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public override async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(ApiBase))
        {
            var proxyUrl = $"{ApiBase.TrimEnd('/')}/search?keyword={Uri.EscapeDataString(keyword)}&page={page}";
            var proxyJson = await Http.GetStringAsync(proxyUrl, ct).ConfigureAwait(false);
            using var proxyDoc = JsonDocument.Parse(proxyJson);
            if (proxyDoc.RootElement.TryGetProperty("data", out var pdata)
                && pdata.TryGetProperty("list", out var plist))
            {
                return plist.EnumerateArray().Select(ParseLegacyApiItem).ToList();
            }
        }

        var pn = Math.Max(0, page - 1);
        var url =
            "http://www.kuwo.cn/search/searchMusicBykeyWord?" +
            "vipver=1&client=kt&ft=music&cluster=0&strategy=2012&encoding=utf8&rformat=json" +
            "&mobi=1&issubtitle=1&show_copyright_off=1" +
            $"&pn={pn}&rn=30&all={Uri.EscapeDataString(keyword)}";

        var json = await MusicSearchHttp.GetStringWithUserAgentAsync(url, MusicSearchHttp.KuwoUserAgent, ct)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("abslist", out var list) || list.ValueKind != JsonValueKind.Array)
            return [];

        return list.EnumerateArray()
            .Where(s => s.TryGetProperty("bitSwitch", out var bs) && bs.GetInt32() != 0)
            .Select(ParseAbsListItem)
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .ToList();
    }

    public override async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
    {
        var stub = new OnlineTrack
        {
            Id = trackId,
            Platform = MusicPlatform.Kuwo,
            Title = "track",
            Artist = "artist"
        };
        var viaUnified = await GoMusicApiClient.TryResolveDownloadUrlAsync(stub, quality, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(viaUnified)) return viaUnified;

        if (!string.IsNullOrEmpty(ApiBase))
        {
            var json = await Http.GetStringAsync($"{ApiBase.TrimEnd('/')}/song/url?id={trackId}", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("url").GetString()
                ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Kuwo.LinkFailed"));
        }

        var br = quality == AudioQuality.Flac ? "flac" : quality == AudioQuality.High320 ? "320kmp3" : "128kmp3";
        var qualities = quality == AudioQuality.Flac
            ? new[] { "flac", "2000kflac", "320kmp3", "128kmp3" }
            : quality == AudioQuality.High320
                ? new[] { "320kmp3", "128kmp3" }
                : new[] { br, "320kmp3", "128kmp3" };

        var randomId = $"C_APK_guanwang_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100000, 999999)}";
        foreach (var q in qualities.Distinct())
        {
            var apiUrl =
                "https://mobi.kuwo.cn/mobi.s?" +
                $"f=web&source=kwplayercar_ar_6.0.0.9_B_jiakong_vh.apk&from=PC" +
                $"&type=convert_url_with_sign&br={q}&rid={Uri.EscapeDataString(trackId)}&user={Uri.EscapeDataString(randomId)}";

            try
            {
                var body = await MusicSearchHttp.GetStringWithUserAgentAsync(apiUrl, MusicSearchHttp.KuwoUserAgent, ct)
                    .ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("url", out var urlEl))
                {
                    var songUrl = urlEl.GetString();
                    if (!string.IsNullOrWhiteSpace(songUrl)) return songUrl;
                }
            }
            catch { /* try next quality */ }
        }

        throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Kuwo.NoDownloadLink"));
    }

    private static OnlineTrack ParseAbsListItem(JsonElement item)
    {
        var rid = item.TryGetProperty("MUSICRID", out var mr) ? mr.GetString() ?? "" : "";
        rid = rid.Trim();
        if (rid.StartsWith("MUSIC_", StringComparison.OrdinalIgnoreCase))
            rid = rid[6..];

        var duration = 0;
        if (item.TryGetProperty("DURATION", out var dur))
        {
            _ = int.TryParse(dur.GetString(), out duration);
            if (duration == 0 && dur.ValueKind == JsonValueKind.Number)
                duration = dur.GetInt32();
        }

        var minfo = item.TryGetProperty("MINFO", out var mi) ? mi.GetString() : null;
        return new OnlineTrack
        {
            Id = rid,
            Platform = MusicPlatform.Kuwo,
            Title = item.TryGetProperty("SONGNAME", out var n) ? n.GetString() ?? "" : "",
            Artist = item.TryGetProperty("ARTIST", out var ar) ? ar.GetString() ?? "" : "",
            Album = item.TryGetProperty("ALBUM", out var al) ? al.GetString() ?? "" : "",
            DurationMs = duration * 1000,
            CoverUrl = item.TryGetProperty("hts_MVPIC", out var pic) ? pic.GetString() : null,
            HasFlac = minfo != null && minfo.Contains("flac", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static OnlineTrack ParseLegacyApiItem(JsonElement s) => new()
    {
        Id = s.GetProperty("rid").GetRawText().Trim('"'),
        Platform = MusicPlatform.Kuwo,
        Title = s.GetProperty("name").GetString() ?? "",
        Artist = s.GetProperty("artist").GetString() ?? "",
        Album = s.TryGetProperty("album", out var al) ? al.GetString() ?? "" : "",
        DurationMs = s.TryGetProperty("songTimeMinutes", out var t) ? (int)(t.GetDouble() * 60000) : 0
    };
}
