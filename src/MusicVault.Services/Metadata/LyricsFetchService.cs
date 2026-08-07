using System.Net.Http;
using System.Text.Json;
using MusicVault.Core.Text;

namespace MusicVault.Services.Metadata;

/// <summary>在线歌词获取与双语合并（LRCLIB）。</summary>
public sealed class LyricsFetchService
{
    private static readonly HttpClient Http = MusicVault.Services.Http.HttpClients.Default;

    public async Task<string?> DownloadAsync(string artist, string title, CancellationToken ct = default)
    {
        var primary = await FetchBestMatchAsync(artist, title, ct);
        if (string.IsNullOrWhiteSpace(primary))
            return null;

        if (!MusicTextScript.PreferChineseLyricsTranslation(artist, title))
            return primary;

        var zh = await FetchChineseMatchAsync(artist, title, ct);
        if (string.IsNullOrWhiteSpace(zh))
            return primary;

        return LrcParser.MergeBilingualSources(primary, zh);
    }

    private static async Task<string?> FetchBestMatchAsync(string artist, string title, CancellationToken ct)
    {
        var url =
            $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";
        try
        {
            var json = await Http.GetStringAsync(url, ct);
            return ExtractLyricsFromJson(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> FetchChineseMatchAsync(string artist, string title, CancellationToken ct)
    {
        var url =
            $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("language", out var langEl))
                    continue;
                var lang = langEl.GetString() ?? string.Empty;
                if (!IsChineseLanguage(lang))
                    continue;

                if (item.TryGetProperty("syncedLyrics", out var synced) && synced.ValueKind == JsonValueKind.String)
                {
                    var s = synced.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }

                if (item.TryGetProperty("plainLyrics", out var plain) && plain.ValueKind == JsonValueKind.String)
                {
                    var p = plain.GetString();
                    if (!string.IsNullOrWhiteSpace(p))
                        return p;
                }
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private static bool IsChineseLanguage(string lang)
    {
        var l = lang.Trim().ToLowerInvariant();
        return l is "zh" or "zho" or "chi" or "cmn" or "zh-hans" or "zh-hant"
               or "chinese" or "mandarin" or "中文" or "汉语";
    }

    private static string? ExtractLyricsFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("syncedLyrics", out var synced) && synced.ValueKind == JsonValueKind.String)
            {
                var s = synced.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }

            if (root.TryGetProperty("plainLyrics", out var plain) && plain.ValueKind == JsonValueKind.String)
            {
                var p = plain.GetString();
                if (!string.IsNullOrWhiteSpace(p))
                    return p;
            }
        }
        catch
        {
            /* 回退旧格式 */
            var syncedIdx = json.IndexOf("\"syncedLyrics\"", StringComparison.Ordinal);
            if (syncedIdx >= 0)
                return UnescapeJsonString(json, syncedIdx + 15);
            var plainIdx = json.IndexOf("\"plainLyrics\"", StringComparison.Ordinal);
            if (plainIdx >= 0)
                return UnescapeJsonString(json, plainIdx + 14);
        }

        return null;
    }

    private static string UnescapeJsonString(string json, int keyOffset)
    {
        var start = json.IndexOf('"', keyOffset) + 1;
        var end = json.IndexOf('"', start);
        if (start <= 0 || end <= start) return string.Empty;
        return json[start..end].Replace("\\n", "\n").Replace("\\r", "");
    }
}
