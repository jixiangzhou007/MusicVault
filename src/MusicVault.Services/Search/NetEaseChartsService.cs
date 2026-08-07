using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Search;

/// <summary>通过网易云 API 获取官方榜单列表。</summary>
public static class NetEaseChartsService
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;
    private static readonly object CacheLock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private static readonly string[] ToplistPaths = ["toplist", "api/toplist"];

    private static IReadOnlyList<MusicChart>? _cachedCharts;
    private static string? _cachedError;
    private static DateTime _cachedAtUtc;

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedCharts = null;
            _cachedError = null;
            _cachedAtUtc = default;
        }
    }

    public static async Task<(IReadOnlyList<MusicChart> Charts, string? Error)> FetchChartsAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!forceRefresh && TryGetCached(out var cachedCharts, out var cachedError))
            return (cachedCharts, cachedError);

        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        var apiBase = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(apiBase))
        {
            var missing = ((IReadOnlyList<MusicChart>)[], ServiceMessageCodec.Key("Discover.Charts.NotConfigured"));
            SetCache(missing.Item1, missing.Item2);
            return missing;
        }

        apiBase = apiBase.Trim().TrimEnd('/');
        string? lastDetail = null;

        foreach (var path in ToplistPaths)
        {
            try
            {
                var url = $"{apiBase}/{path}";
                using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
                if (doc == null)
                {
                    lastDetail = ServiceMessageCodec.Key("Discover.Charts.NoResponse");
                    continue;
                }

                if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                {
                    lastDetail = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString()
                        : doc.RootElement.TryGetProperty("msg", out var m2) ? m2.GetString() : null;
                    lastDetail ??= ServiceMessageCodec.Key("Discover.ChartsFetchFailed");
                    continue;
                }

                if (!doc.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                {
                    lastDetail = ServiceMessageCodec.Key("Discover.Charts.BadFormat");
                    continue;
                }

                var charts = list.EnumerateArray()
                    .Select(ParseChart)
                    .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                    .ToList();

                var result = charts.Count > 0 ? (charts, null) : ((IReadOnlyList<MusicChart>)[], ServiceMessageCodec.Key("Discover.Charts.NoData"));
                SetCache(result.Item1, result.Item2);
                return result;
            }
            catch (Exception ex)
            {
                lastDetail = ex.Message;
            }
        }

        var failure = ((IReadOnlyList<MusicChart>)[], lastDetail ?? ServiceMessageCodec.Key("Discover.ChartsFetchFailed"));
        SetCache(failure.Item1, failure.Item2);
        return failure;
    }

    private static bool TryGetCached(out IReadOnlyList<MusicChart> charts, out string? error)
    {
        lock (CacheLock)
        {
            if (_cachedCharts == null && _cachedError == null)
            {
                charts = [];
                error = null;
                return false;
            }

            if (DateTime.UtcNow - _cachedAtUtc > CacheTtl)
            {
                charts = [];
                error = null;
                return false;
            }

            charts = _cachedCharts ?? [];
            error = _cachedError;
            return true;
        }
    }

    private static void SetCache(IReadOnlyList<MusicChart> charts, string? error)
    {
        lock (CacheLock)
        {
            _cachedCharts = charts;
            _cachedError = error;
            _cachedAtUtc = DateTime.UtcNow;
        }
    }

    private static MusicChart ParseChart(JsonElement el)
    {
        var id = el.TryGetProperty("id", out var idEl)
            ? idEl.ValueKind == JsonValueKind.String ? idEl.GetString() ?? "" : idEl.GetInt64().ToString()
            : string.Empty;
        var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : string.Empty;
        var freq = el.TryGetProperty("updateFrequency", out var freqEl) ? freqEl.GetString() : null;
        var count = el.TryGetProperty("trackCount", out var countEl) && countEl.TryGetInt32(out var n) ? n : 0;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(freq)) parts.Add(freq);
        if (count > 0) parts.Add(ServiceMessageCodec.Format("Discover.Charts.TrackCountSubtitle", count));
        var subtitle = parts.Count > 0 ? string.Join(" · ", parts) : ServiceMessageCodec.Key("Discover.Charts.NetEaseLabel");
        var description = el.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
        var cover = el.TryGetProperty("coverImgUrl", out var coverEl) ? coverEl.GetString()
            : el.TryGetProperty("coverUrl", out var cover2) ? cover2.GetString() : null;
        return new MusicChart(id, name, subtitle, description, cover, MusicPlatform.NetEase);
    }
}
