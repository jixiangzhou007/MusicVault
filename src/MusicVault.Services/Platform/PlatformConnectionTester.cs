using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Platform;

public sealed record PlatformTestResult(bool Ok, string Message);

/// <summary>设置页 / 发现页 API 连通性探测。</summary>
public static class PlatformConnectionTester
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    private static readonly string[] NetEaseProbePaths =
    [
        "cloudsearch?keywords=test&type=1&limit=1&offset=0",
        "search?keywords=test&type=1&limit=1&offset=0"
    ];

    public static async Task<PlatformTestResult> TestUnifiedApiAsync(string? baseUrl, CancellationToken ct = default)
    {
        var prepared = await PrepareUnifiedApiAsync(baseUrl, ct).ConfigureAwait(false);
        if (!prepared.Ok)
            return prepared;

        try
        {
            var (_, diag) = await GoMusicApiClient.SearchAsync("test", Core.Enums.MusicPlatform.NetEase, ct)
                .ConfigureAwait(false);
            var hint = diag.Count > 0
                ? diag[0]
                : ServiceMessageCodec.Key("Api.Connection.SearchProbeResponded");
            return new(true, ServiceMessageCodec.Format("Api.Connection.OkWithHint", hint));
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }

    /// <summary>轻量健康探测（启动/状态栏用）：只读 HTTP ping，不拉起子进程、不同步 Cookie。</summary>
    public static async Task<PlatformTestResult> ProbeUnifiedApiHealthAsync(string? baseUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? MusicGathererRegistry.ResolveEffectiveMusicApiUrl(null)
            : baseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
            return new(false, ServiceMessageCodec.Key("Api.Connection.NotConfigured"));

        var resolved = await ResolveHealthyUnifiedBaseAsync(url, ct).ConfigureAwait(false);
        if (resolved != null)
            return new(true, resolved == url
                ? ServiceMessageCodec.Key("Api.Connection.Ok")
                : ServiceMessageCodec.Format("Api.Connection.OkFallbackPort", resolved));

        return new(false, ServiceMessageCodec.Key("Api.HealthCheck.NoResponse"));
    }

    /// <summary>扩展网易云 API 轻量探测（榜单 / 搜索）。</summary>
    public static async Task<PlatformTestResult> ProbeNetEaseApiHealthAsync(string? baseUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? MusicServiceEndpoints.ResolveNetEaseApi()
            : baseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
            return new(false, ServiceMessageCodec.Key("Api.NetEase.NotConfigured"));

        string? lastDetail = null;
        foreach (var path in new[] { "toplist", "search?keywords=ping&type=1&limit=1" })
        {
            try
            {
                using var doc = await Http
                    .GetFromJsonAsync<JsonDocument>($"{url}/{path}", ct)
                    .ConfigureAwait(false);
                if (doc?.RootElement.TryGetProperty("code", out var code) == true && code.GetInt32() == 200)
                    return new(true, url);
                lastDetail = ServiceMessageCodec.Key("Api.NetEase.Non200");
            }
            catch (Exception ex)
            {
                lastDetail = ex.Message;
            }
        }

        return new(false, lastDetail ?? ServiceMessageCodec.Key("Api.NetEase.Unavailable"));
    }

    /// <summary>本地统一 API：探测配置地址，并在 8080/18080 间互认健康实例。</summary>
    public static async Task<string?> ResolveHealthyUnifiedBaseAsync(string baseUrl, CancellationToken ct = default)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        if (await BundledMusicApiHost.IsHealthyAsync(url, quick: false, ct).ConfigureAwait(false))
            return url;

        if (!BundledMusicApiHost.IsLocalApiUrl(url))
            return null;

        foreach (var candidate in new[] { BundledMusicApiHost.DefaultBaseUrl, BundledMusicApiHost.FallbackBaseUrl })
        {
            var alt = candidate.TrimEnd('/');
            if (string.Equals(alt, url, StringComparison.OrdinalIgnoreCase))
                continue;
            if (await BundledMusicApiHost.IsHealthyAsync(alt, quick: false, ct).ConfigureAwait(false))
                return alt;
        }

        return null;
    }

    public static bool TryResolveHealthyUnifiedBase(string baseUrl, out string resolved)
    {
        resolved = baseUrl.Trim().TrimEnd('/');
        var healthy = ResolveHealthyUnifiedBaseAsync(resolved).GetAwaiter().GetResult();
        if (healthy == null)
            return false;

        resolved = healthy;
        return true;
    }

    public static async Task<PlatformTestResult> TestNetEaseApiAsync(string? baseUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? MusicServiceEndpoints.NetEaseApiUrl ?? MusicServiceEndpoints.DefaultNetEaseApiUrl
            : baseUrl.Trim().TrimEnd('/');

        if (IsLikelyLocalNetEaseApi(url))
        {
            var prepared = await PrepareBundledNetEaseApiAsync(url, ct).ConfigureAwait(false);
            if (!prepared.Ok && BundledNetEaseApiHost.Instance.HasBundledApi)
                return prepared;
        }

        string? lastDetail = null;
        foreach (var path in NetEaseProbePaths)
        {
            try
            {
                using var doc = await Http
                    .GetFromJsonAsync<JsonDocument>($"{url}/{path}", ct)
                    .ConfigureAwait(false);
                if (doc == null)
                {
                    lastDetail = ServiceMessageCodec.Key("Api.NetEase.NoResponse");
                    continue;
                }

                if (doc.RootElement.TryGetProperty("code", out var codeEl)
                    && codeEl.ValueKind == JsonValueKind.Number
                    && codeEl.GetInt32() != 200)
                {
                    var apiMsg = doc.RootElement.TryGetProperty("msg", out var msgEl)
                        ? msgEl.GetString()
                        : null;
                    lastDetail = string.IsNullOrWhiteSpace(apiMsg)
                        ? ServiceMessageCodec.Format("Api.NetEase.BadCode", codeEl.GetInt32())
                        : apiMsg;
                    continue;
                }

                if (doc.RootElement.TryGetProperty("result", out var result)
                    && result.TryGetProperty("songs", out var songs)
                    && songs.ValueKind == JsonValueKind.Array)
                    return new(true, ServiceMessageCodec.Format("Api.NetEase.Ok", url));

                lastDetail = ServiceMessageCodec.Key("Api.NetEase.MissingSongs");
            }
            catch (Exception ex)
            {
                lastDetail = ex.Message;
            }
        }

        var hint = IsLikelyLocalNetEaseApi(url)
            ? BundledNetEaseApiHost.Instance.HasBundledApi
                ? ServiceMessageCodec.Key("Api.NetEase.Hint.AutoStart")
                : ServiceMessageCodec.Key("Api.NetEase.Hint.SetupScript")
            : string.Empty;
        return new(false, ServiceMessageCodec.Format(
            "Api.NetEase.UnavailableDetail",
            lastDetail ?? ServiceMessageCodec.Key("Api.NetEase.CannotConnect"),
            hint));
    }

    /// <summary>Prepare bundled netease API (sync config, auto-start when local).</summary>
    public static async Task<PlatformTestResult> PrepareBundledNetEaseApiAsync(string? baseUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? MusicServiceEndpoints.DefaultNetEaseApiUrl
            : baseUrl.Trim().TrimEnd('/');

        if (!BundledNetEaseApiHost.IsLocalApiUrl(url))
            return new(true, url);

        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        if (!BundledNetEaseApiHost.Instance.HasBundledApi)
            return new(false, ServiceMessageCodec.Key("Api.NetEase.BundledNotFound"));

        if (!BundledNetEaseApiHost.Instance.CanAutoStart)
            return new(false, ServiceMessageCodec.Key("Api.NetEase.NodeNotFound"));

        var effective = await BundledNetEaseApiHost.Instance
            .EnsureRunningWithPortFallbackAsync(url, ct)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(effective))
            return new(false, ServiceMessageCodec.Key("Api.NetEase.StartFailed"));

        MusicServiceEndpoints.NetEaseApiUrl = effective;
        Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", effective);
        return new(true, effective);
    }

    /// <summary>根据设置页当前输入准备统一 API（同步配置、必要时拉起内置服务）。</summary>
    public static async Task<PlatformTestResult> PrepareUnifiedApiAsync(string? baseUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? MusicGathererRegistry.ResolveEffectiveMusicApiUrl(null)
            : baseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
            return new(false, ServiceMessageCodec.Key("Api.Unified.UrlNotFilled"));

        if (BundledMusicApiHost.IsLocalApiUrl(url) && BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            try
            {
                var effective = await MusicApiRuntime.EnsureLocalUnifiedReadyAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(effective))
                    return new(false, ServiceMessageCodec.Key("Api.Unified.StartGoMusicFailed"));
                url = effective;
            }
            catch (Exception ex)
            {
                return new(false, UnifiedApiMessageMapper.MapException(ex));
            }
        }
        else if (!await BundledMusicApiHost.IsHealthyAsync(url, quick: false, ct).ConfigureAwait(false))
        {
            return new(false, ServiceMessageCodec.Format("Api.Unified.Unreachable", url));
        }

        MusicApiConfig.BaseUrl = url;
        await MusicApiSessionHelper.SyncStoredCookiesAsync(ct).ConfigureAwait(false);
        return new(true, url);
    }

    private static bool IsLikelyLocalNetEaseApi(string url) =>
        url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || url.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || url.Contains("[::1]", StringComparison.OrdinalIgnoreCase);
}
