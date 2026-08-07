using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using MusicVault.Services.Http;
using MusicVault.Services.Process;

namespace MusicVault.Services.Api;

/// <summary>
/// Bundled NeteaseCloudMusicApi (Node): api/netease-cloud-music-api + optional api/node/node.exe.
/// Windows 上默认绑定 0.0.0.0:3000 可能 EACCES；启动时固定 HOST=127.0.0.1，并在 3000 失败时回退 13000。
/// </summary>
public sealed class BundledNetEaseApiHost : IDisposable
{
    public const string DefaultBaseUrl = "http://localhost:3000";
    public const string FallbackBaseUrl = "http://localhost:13000";
    public const int DefaultPort = 3000;
    public const int FallbackPort = 13000;
    public const string PackageFolderName = "netease-cloud-music-api";
    public const string VersionFileName = "netease-api-version.txt";

    public static BundledNetEaseApiHost Instance { get; } = new();

    private System.Diagnostics.Process? _process;
    private bool _startedByApp;
    private int _startedPort;

    public string? BundledApiDirectory { get; private set; }
    public string? EntryScriptPath { get; private set; }
    public string? NodeExecutablePath { get; private set; }

    public bool HasBundledApi => !string.IsNullOrEmpty(EntryScriptPath) && File.Exists(EntryScriptPath);
    public bool HasBundledNode => !string.IsNullOrEmpty(NodeExecutablePath) && File.Exists(NodeExecutablePath);
    public bool CanAutoStart => HasBundledApi && (HasBundledNode || ResolveNodeOnPath() != null);

    public void Discover(string appBaseDirectory)
    {
        BundledApiDirectory = null;
        EntryScriptPath = null;
        NodeExecutablePath = null;

        foreach (var baseDir in BundledMusicApiHost.EnumerateSearchRoots(appBaseDirectory))
        {
            var apiRoots = new[]
            {
                Path.Combine(baseDir, "api", PackageFolderName),
                Path.Combine(baseDir, PackageFolderName),
                Path.Combine(baseDir, "tools", PackageFolderName)
            };

            foreach (var root in apiRoots)
            {
                var entry = ResolveEntryScript(root);
                if (entry == null) continue;
                BundledApiDirectory = root;
                EntryScriptPath = entry;
                break;
            }

            if (EntryScriptPath != null)
            {
                var bundledNode = Path.Combine(baseDir, "api", "node", "node.exe");
                if (File.Exists(bundledNode))
                    NodeExecutablePath = bundledNode;
                break;
            }
        }
    }

    public static bool IsLocalApiUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Port != DefaultPort && uri.Port != FallbackPort) return false;
        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || uri.Host == "[::1]";
    }

    private static int ResolveListenPort(string baseUrl) =>
        Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ? uri.Port : DefaultPort;

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

        if (!CanAutoStart || EntryScriptPath == null || BundledApiDirectory == null)
            return false;

        var node = NodeExecutablePath ?? ResolveNodeOnPath();
        if (node == null)
            return false;

        var port = ResolveListenPort(trimmed);

        // 端口被占用但非健康网易云 API 时，勿空等；交给端口回退。
        if (IsTcpPortListening(port)
            && !await IsHealthyAsync(trimmed, quick: true, ct).ConfigureAwait(false))
            return false;

        if (_process == null || _process.HasExited || _startedPort != port)
        {
            if (_process is { HasExited: false } && _startedPort != port)
                StopIfStartedByApp();

            try
            {
                var relScript = Path.GetRelativePath(BundledApiDirectory, EntryScriptPath).Replace('\\', '/');
                var args = relScript.Contains(' ') ? $"\"{relScript}\"" : relScript;
                // HOST=127.0.0.1：避免 Windows 上 listen 0.0.0.0:3000 出现 EACCES
                _process = HiddenProcessLauncher.StartWithArguments(
                    node,
                    args,
                    BundledApiDirectory,
                    new Dictionary<string, string>
                    {
                        ["PORT"] = port.ToString(),
                        ["HOST"] = "127.0.0.1"
                    });
                _startedByApp = _process != null;
                _startedPort = port;
            }
            catch
            {
                return false;
            }
        }

        if (_process == null)
            return false;

        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct).ConfigureAwait(false);
            if (await IsHealthyAsync(trimmed, quick: false, ct).ConfigureAwait(false))
                return true;
            if (_process is { HasExited: true })
                return false;
        }

        return false;
    }

    /// <summary>3000 失败时回退 13000（或反之）；跳过「占用但非健康」的端口。</summary>
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
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(quick ? TimeSpan.FromMilliseconds(900) : TimeSpan.FromSeconds(4));
            var url = $"{baseUrl.TrimEnd('/')}/search?keywords=ping&type=1&limit=1";
            using var doc = await MusicSearchHttp.Client
                .GetFromJsonAsync<JsonDocument>(url, cts.Token)
                .ConfigureAwait(false);
            return doc?.RootElement.TryGetProperty("code", out var code) == true && code.GetInt32() == 200;
        }
        catch
        {
            return false;
        }
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
            _startedPort = 0;
        }
    }

    public void Dispose() => StopIfStartedByApp();

    private static string? ResolveEntryScript(string apiRoot)
    {
        if (!Directory.Exists(apiRoot)) return null;

        var marker = Path.Combine(apiRoot, "entry-point.txt");
        if (File.Exists(marker))
        {
            var rel = File.ReadAllText(marker).Trim();
            if (!string.IsNullOrEmpty(rel))
            {
                var fromMarker = Path.GetFullPath(Path.Combine(apiRoot, rel));
                if (File.Exists(fromMarker)) return fromMarker;
            }
        }

        var candidates = new[]
        {
            Path.Combine(apiRoot, "app.js"),
            Path.Combine(apiRoot, "main.js"),
            Path.Combine(apiRoot, "node_modules", "netease-cloud-music-api-alger", "app.js"),
            Path.Combine(apiRoot, "node_modules", "NeteaseCloudMusicApi", "app.js")
        };

        return candidates.FirstOrDefault(File.Exists);
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
