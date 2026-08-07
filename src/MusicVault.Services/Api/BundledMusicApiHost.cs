using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using MusicVault.Services.Http;
using MusicVault.Services.Process;

namespace MusicVault.Services.Api;

/// <summary>
/// 随安装包分发的 go-music-api 子进程：放在 MusicVault.exe 同级的 api/ 目录。
/// 他人未配置远程 API 时，可自动启动本地服务；未打包时仍走酷狗/酷我直连。
/// </summary>
public sealed class BundledMusicApiHost : IDisposable
{
    public const string DefaultBaseUrl = "http://localhost:8080";
    public const string FallbackBaseUrl = "http://localhost:18080";
    public const int DefaultPort = 8080;
    public const int FallbackPort = 18080;

    public static BundledMusicApiHost Instance { get; } = new();

    private System.Diagnostics.Process? _process;
    private bool _startedByApp;

    public string? BundledExecutablePath { get; private set; }

    public bool HasBundledExecutable => !string.IsNullOrEmpty(BundledExecutablePath);

    /// <summary>在应用目录下查找 api/go-music-api.exe（含单文件发布时 ProcessPath 旁路）。</summary>
    public void Discover(string appBaseDirectory)
    {
        BundledExecutablePath = null;
        foreach (var baseDir in EnumerateSearchRoots(appBaseDirectory))
        {
            var candidates = new[]
            {
                Path.Combine(baseDir, "api", "go-music-api.exe"),
                Path.Combine(baseDir, "go-music-api", "go-music-api.exe"),
                Path.Combine(baseDir, "tools", "go-music-api.exe")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;
                BundledExecutablePath = path;
                return;
            }
        }
    }

    internal static IReadOnlyList<string> EnumerateSearchRoots(string? appBaseDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void TryAdd(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return;
            try
            {
                var full = Path.GetFullPath(dir);
                if (seen.Add(full))
                    list.Add(full);
            }
            catch
            {
                /* ignore invalid path */
            }
        }

        TryAdd(appBaseDirectory);
        TryAdd(AppContext.BaseDirectory);
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
            TryAdd(Path.GetDirectoryName(processPath));

        return list;
    }

    public static bool IsLocalApiUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Port != DefaultPort && uri.Port != FallbackPort) return false;
        return IsLoopbackHost(uri);
    }

    private static bool IsLoopbackHost(Uri uri) =>
        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || uri.Host == "[::1]";

    private static int ResolveListenPort(string baseUrl) =>
        Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ? uri.Port : DefaultPort;

    /// <summary>端口已被占用且非健康 go-music-api 时，勿再启动/空等。</summary>
    private static bool IsTcpPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureRunningAsync(string baseUrl, CancellationToken ct = default)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (await IsHealthyAsync(trimmed, quick: true, ct).ConfigureAwait(false))
            return true;

        if (!HasBundledExecutable)
            return false;

        var port = ResolveListenPort(trimmed);

        // 默认端口被其它进程占用（如本机 python）时，健康检查会失败；勿空等 20s，交给端口回退。
        if (IsTcpPortListening(port))
            return false;

            if (_process == null || _process.HasExited)
            {
                try
                {
                    var workDir = Path.GetDirectoryName(BundledExecutablePath!)!;
                    _process = HiddenProcessLauncher.Start(
                        BundledExecutablePath!,
                        workDir,
                        new Dictionary<string, string> { ["PORT"] = port.ToString() });
                    _startedByApp = _process != null;
                }
                catch
                {
                    return false;
                }
            }

            if (_process == null)
                return false;

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(400, ct).ConfigureAwait(false);
                if (await IsHealthyAsync(trimmed, quick: false, ct).ConfigureAwait(false))
                    return true;
                if (_process.HasExited)
                    return false;
            }

            return false;
    }

    /// <summary>
    /// 本地 URL 拉起失败时，在 8080 / 18080 间回退。
    /// 已配置 18080 时也会尝试（旧逻辑只在「当前为 8080」时回退，导致设置已写 18080 后无法恢复）。
    /// 跳过「端口被占用但非健康 API」的候选（例如本机其它进程占 8080）。
    /// </summary>
    public async Task<string?> EnsureRunningWithPortFallbackAsync(string baseUrl, CancellationToken ct = default)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (await EnsureRunningAsync(trimmed, ct).ConfigureAwait(false))
            return trimmed;

        if (!IsLocalApiUrl(trimmed))
            return null;

        foreach (var candidate in new[] { DefaultBaseUrl, FallbackBaseUrl })
        {
            var alt = candidate.TrimEnd('/');
            if (string.Equals(alt, trimmed, StringComparison.OrdinalIgnoreCase))
                continue;

            var port = ResolveListenPort(alt);
            if (IsTcpPortListening(port)
                && !await IsHealthyAsync(alt, quick: true, ct).ConfigureAwait(false))
                continue;

            StopIfStartedByApp();
            if (await EnsureRunningAsync(alt, ct).ConfigureAwait(false))
                return alt;
        }

        return null;
    }

    public static async Task<bool> IsHealthyAsync(
        string baseUrl,
        bool quick = false,
        CancellationToken ct = default)
    {
        var attempts = quick ? 1 : 2;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(400, ct).ConfigureAwait(false);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(quick ? TimeSpan.FromMilliseconds(800) : TimeSpan.FromSeconds(5));
                var url = $"{baseUrl.TrimEnd('/')}/api/v1/music/search?q=ping&type=song&sources=kugou";
                using var doc = await MusicSearchHttp.Client
                    .GetFromJsonAsync<JsonDocument>(url, cts.Token)
                    .ConfigureAwait(false);
                if (doc?.RootElement.TryGetProperty("code", out var code) == true && code.GetInt32() == 200)
                    return true;
            }
            catch
            {
                // retry on next attempt
            }
        }

        return false;
    }

    public void StopIfStartedByApp()
    {
        if (!_startedByApp || _process == null) return;
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch { /* ignore */ }
        finally
        {
            _process.Dispose();
            _process = null;
            _startedByApp = false;
        }
    }

    public void Dispose() => StopIfStartedByApp();
}
