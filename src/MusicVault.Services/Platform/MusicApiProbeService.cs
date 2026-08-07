using System.Net.Http.Json;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services;

namespace MusicVault.Services.Platform;

public sealed record ApiProbeResult(string Label, string Url, bool Ok, string Detail);

/// <summary>探测统一 API 与扩展网易云 API 的关键接口（设置诊断 / 发布校验）。</summary>
public static class MusicApiProbeService
{
    public static async Task<IReadOnlyList<ApiProbeResult>> ProbeAllAsync(
        string? unifiedBaseUrl,
        string? neteaseBaseUrl,
        bool prepareServices = true,
        CancellationToken ct = default)
    {
        var results = new List<ApiProbeResult>();

        if (prepareServices)
        {
            var unifiedPrep = await PlatformConnectionTester
                .PrepareUnifiedApiAsync(unifiedBaseUrl, ct)
                .ConfigureAwait(false);
            if (!unifiedPrep.Ok)
            {
                results.Add(new ApiProbeResult(
                    "go-music-api · prepare",
                    unifiedBaseUrl ?? "—",
                    false,
                    unifiedPrep.Message));
            }

            var neteasePrep = await PlatformConnectionTester
                .PrepareBundledNetEaseApiAsync(neteaseBaseUrl, ct)
                .ConfigureAwait(false);
            if (!neteasePrep.Ok && BundledNetEaseApiHost.IsLocalApiUrl(neteaseBaseUrl ?? BundledNetEaseApiHost.DefaultBaseUrl))
            {
                results.Add(new ApiProbeResult(
                    "netease-cloud-music-api · prepare",
                    neteaseBaseUrl ?? BundledNetEaseApiHost.DefaultBaseUrl,
                    false,
                    neteasePrep.Message));
            }
        }

        var goBase = await ResolveUnifiedProbeBaseAsync(unifiedBaseUrl, ct).ConfigureAwait(false);
        if (goBase != null)
        {
            results.Add(await ProbeEndpointAsync(
                "go-music-api · health",
                $"{goBase}/api/v1/music/search?q=ping&type=song&sources=kugou",
                ct).ConfigureAwait(false));
            results.Add(await ProbeEndpointAsync(
                "go-music-api · search netease",
                $"{goBase}/api/v1/music/search?q=test&type=song&sources=netease",
                ct).ConfigureAwait(false));
            results.Add(await ProbeEndpointAsync(
                "go-music-api · qr sources",
                $"{goBase}/api/v1/system/qr_login/sources",
                ct,
                doc => doc.RootElement.TryGetProperty("code", out var c) && c.GetInt32() == 200).ConfigureAwait(false));
        }
        else
        {
            results.Add(new ApiProbeResult("go-music-api · health", "—", false, ServiceMessageCodec.Key("Api.Probe.UnifiedNotConfigured")));
        }

        var neBase = ResolveNetEaseProbeBase(neteaseBaseUrl);
        if (neBase != null)
        {
            results.Add(await ProbeEndpointAsync(
                "netease · search",
                $"{neBase}/search?keywords=ping&type=1&limit=1",
                ct).ConfigureAwait(false));
            results.Add(await ProbeEndpointAsync(
                "netease · toplist (charts)",
                $"{neBase}/toplist",
                ct,
                doc => doc.RootElement.TryGetProperty("code", out var c) && c.GetInt32() == 200
                       && doc.RootElement.TryGetProperty("list", out var list)
                       && list.ValueKind == System.Text.Json.JsonValueKind.Array
                       && list.GetArrayLength() > 0).ConfigureAwait(false));
            results.Add(await ProbeEndpointAsync(
                "netease · playlist detail",
                $"{neBase}/playlist/detail?id=3778678",
                ct,
                doc => doc.RootElement.TryGetProperty("code", out var c) && c.GetInt32() == 200).ConfigureAwait(false));
        }
        else
        {
            results.Add(new ApiProbeResult("netease · toplist (charts)", "—", false, ServiceMessageCodec.Key("Api.Probe.NetEaseNotConfigured")));
        }

        return results;
    }

    private static async Task<string?> ResolveUnifiedProbeBaseAsync(string? configured, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(configured)
            ? MusicGathererRegistry.ResolveEffectiveMusicApiUrl(null)
            : configured.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return await PlatformConnectionTester.ResolveHealthyUnifiedBaseAsync(url, ct).ConfigureAwait(false)
               ?? url;
    }

    private static string? ResolveNetEaseProbeBase(string? configured)
    {
        var url = string.IsNullOrWhiteSpace(configured)
            ? MusicServiceEndpoints.ResolveNetEaseApi()
            : configured.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private static async Task<ApiProbeResult> ProbeEndpointAsync(
        string label,
        string url,
        CancellationToken ct,
        Func<System.Text.Json.JsonDocument, bool>? validate = null)
    {
        validate ??= doc => doc.RootElement.TryGetProperty("code", out var c) && c.GetInt32() == 200;

        try
        {
            using var doc = await MusicVault.Services.Http.MusicSearchHttp.Client
                .GetFromJsonAsync<System.Text.Json.JsonDocument>(url, ct)
                .ConfigureAwait(false);
            if (doc == null)
                return new ApiProbeResult(label, url, false, ServiceMessageCodec.Key("Api.Probe.NoResponse"));

            if (!validate(doc))
            {
                var msg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString()
                    : doc.RootElement.TryGetProperty("message", out var m2) ? m2.GetString() : null;
                return new ApiProbeResult(label, url, false, msg ?? ServiceMessageCodec.Key("Api.Probe.BadResponse"));
            }

            var detail = doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == System.Text.Json.JsonValueKind.Number
                ? $"code={code.GetInt32()}"
                : "ok";
            return new ApiProbeResult(label, url, true, detail);
        }
        catch (Exception ex)
        {
            return new ApiProbeResult(label, url, false, ex.Message);
        }
    }
}
