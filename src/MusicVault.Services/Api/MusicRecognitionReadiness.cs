using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>听歌识曲（网易云 bundled）环境自检。</summary>
public sealed record MusicRecognitionReadinessResult(
    bool Ok,
    string Message,
    bool NetEaseApiHealthy,
    bool HasNode,
    bool HasMatchScript,
    bool IsWindowsSupported);

public static class MusicRecognitionReadiness
{
    public static async Task<MusicRecognitionReadinessResult> CheckNetEaseBundledAsync(
        string? netEaseApiBaseUrl,
        bool tryStartApi = true,
        CancellationToken ct = default)
    {
        if (!NetEaseAudioRecognitionClient.IsSupported)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.Readiness.WindowsOnly"),
                false,
                false,
                false,
                false);
        }

        var apiUrl = string.IsNullOrWhiteSpace(netEaseApiBaseUrl)
            ? BundledNetEaseApiHost.DefaultBaseUrl
            : netEaseApiBaseUrl.Trim().TrimEnd('/');

        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        var hasApi = BundledNetEaseApiHost.Instance.HasBundledApi;
        var hasNode = BundledNetEaseApiHost.Instance.HasBundledNode || ResolveNodeOnPath() != null;
        var scriptPath = ResolveMatchScriptPath();
        var hasScript = scriptPath != null;

        if (!hasApi)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.Readiness.BundledApiNotFound"),
                false,
                hasNode,
                hasScript,
                true);
        }

        if (!hasNode)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.Readiness.NodeNotFound"),
                false,
                false,
                hasScript,
                true);
        }

        if (!hasScript)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.Readiness.ScriptNotFound"),
                false,
                hasNode,
                false,
                true);
        }

        if (tryStartApi)
        {
            var started = await BundledNetEaseApiHost.Instance
                .EnsureRunningAsync(apiUrl, ct)
                .ConfigureAwait(false);
            if (!started)
            {
                return new MusicRecognitionReadinessResult(
                    false,
                    ServiceMessageCodec.Format("Recognition.Readiness.NetEaseApiNoResponse", apiUrl),
                    false,
                    hasNode,
                    hasScript,
                    true);
            }
        }

        var healthy = await BundledNetEaseApiHost.IsHealthyAsync(apiUrl, quick: false, ct)
            .ConfigureAwait(false);
        if (!healthy)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Format("Recognition.Readiness.NetEaseApiUnavailable", apiUrl),
                false,
                hasNode,
                hasScript,
                true);
        }

        return new MusicRecognitionReadinessResult(
            true,
            ServiceMessageCodec.Format("Recognition.Readiness.Ready", apiUrl),
            true,
            hasNode,
            hasScript,
            true);
    }

    /// <summary>
    /// 外部 HTTP 识曲（如 AudD）：校验 URL / api_token，并做轻量连通探测（不上传音频、不消耗识别配额）。
    /// </summary>
    public static async Task<MusicRecognitionReadinessResult> CheckExternalHttpAsync(
        string? endpointUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl)
            || !Uri.TryCreate(endpointUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.ExternalUrlInvalid"),
                false,
                false,
                false,
                OperatingSystem.IsWindows());
        }

        var hasToken = uri.Query.Contains("api_token=", StringComparison.OrdinalIgnoreCase);
        if (!hasToken)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Key("Recognition.ExternalNeedToken"),
                false,
                false,
                false,
                OperatingSystem.IsWindows());
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            // 探测主机可达性：GET 根路径或原 URL；401/405 也视为可达
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var resp = await MusicVault.Services.Http.HttpClients.Default
                .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            var code = (int)resp.StatusCode;
            // 2xx–4xx 视为可达（含 401/403/405，说明主机在线）
            if (code is >= 200 and < 500)
            {
                return new MusicRecognitionReadinessResult(
                    true,
                    ServiceMessageCodec.Format("Recognition.ExternalReachable", $"{uri.Host}:{code}"),
                    false,
                    false,
                    false,
                    OperatingSystem.IsWindows());
            }

            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Format("Recognition.ExternalUnreachable", $"HTTP {code}"),
                false,
                false,
                false,
                OperatingSystem.IsWindows());
        }
        catch (Exception ex)
        {
            return new MusicRecognitionReadinessResult(
                false,
                ServiceMessageCodec.Format(
                    "Recognition.ExternalUnreachable",
                    ServiceMessageCodec.Resolve(RecognitionMessageMapper.MapException(ex))),
                false,
                false,
                false,
                OperatingSystem.IsWindows());
        }
    }

    private static string? ResolveMatchScriptPath()
    {
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        var apiDir = BundledNetEaseApiHost.Instance.BundledApiDirectory;
        if (apiDir != null)
        {
            var inApi = Path.Combine(apiDir, "scripts", "netease-audio-match.js");
            if (File.Exists(inApi)) return inApi;
        }

        var dev = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "netease-audio-match.js"));
        if (File.Exists(dev)) return dev;

        return null;
    }

    private static string? ResolveNodeOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* ignore */ }
        }

        return null;
    }
}
