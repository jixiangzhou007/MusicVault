using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Http;

namespace MusicVault.App.Services;

/// <summary>
/// 内置 go-music-api 可选更新：不阻塞启动；默认不自动下载，需用户确认或设置里开启每周检查。
/// 更新源可为上游或自有 fork（见 <see cref="ResolveGithubRepo"/>）。
/// </summary>
public sealed class BundledMusicApiUpdateService
{
    public const string DefaultGithubRepo = "guohuiyuan/go-music-api";
    private const string WindowsAssetSuffix = "go-music-api_windows_amd64.zip";
    private const string UpdateRepoEnv = "MUSICVAULT_MUSIC_API_UPDATE_REPO";

    private static readonly HttpClient Http = MusicSearchHttp.Client;

    /// <summary>由启动/保存设置注入的 <c>owner/repo</c>；可空表示用默认上游。</summary>
    public static string? ConfiguredGithubRepo { get; set; }

    public static bool ShouldCheckOnStartup(AppSettings settings)
    {
        if (!settings.CheckBundledMusicApiUpdates) return false;
        if (!BundledMusicApiHost.Instance.HasBundledExecutable) return false;

        if (settings.LastBundledMusicApiCheckAt == null) return true;
        return DateTime.UtcNow - settings.LastBundledMusicApiCheckAt.Value > TimeSpan.FromDays(7);
    }

    /// <summary>
    /// 解析更新仓库：环境变量 → 设置/注入值 → 默认上游。
    /// 接受 <c>owner/repo</c> 或完整 GitHub URL。
    /// </summary>
    public static string ResolveGithubRepo(string? configured = null, string? envValue = null)
    {
        var env = envValue ?? Environment.GetEnvironmentVariable(UpdateRepoEnv);
        var raw = FirstNonEmpty(env, configured, ConfiguredGithubRepo);
        return NormalizeGithubRepo(raw) ?? DefaultGithubRepo;
    }

    public static string? NormalizeGithubRepo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim().TrimEnd('/');
        if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var path = s.Contains("github.com/", StringComparison.OrdinalIgnoreCase)
                ? s[(s.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length)..]
                : s;
            path = path.Split('?', '#')[0].Trim('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                path = path[..^4];
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0]}/{parts[1]}";
            return null;
        }

        if (Regex.IsMatch(s, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            return s;

        return null;
    }

    public async Task<BundledMusicApiUpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var appDir = AppContext.BaseDirectory;
        var local = BundledMusicApiVersion.ReadLocalVersion(appDir) ?? "unknown";
        var repo = ResolveGithubRepo();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{repo}/releases/latest");
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("User-Agent", "MusicVault-Desktop");

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Fail(local, ServiceMessageCodec.Format("ApiUpdate.GitHubHttpFailed", (int)response.StatusCode));
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var latest = doc.RootElement.TryGetProperty("tag_name", out var tag)
                ? tag.GetString() ?? ""
                : "";
            string? zipUrl = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameEl)) continue;
                    var name = nameEl.GetString() ?? "";
                    if (!name.Equals(WindowsAssetSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                    zipUrl = asset.TryGetProperty("browser_download_url", out var urlEl)
                        ? urlEl.GetString()
                        : null;
                    break;
                }
            }

            if (string.IsNullOrEmpty(zipUrl))
                return Fail(local, ServiceMessageCodec.Key("ApiUpdate.NoWindowsPackage"));

            var available = BundledMusicApiVersion.IsNewer(latest, local);
            return new BundledMusicApiUpdateCheckResult
            {
                Succeeded = true,
                LocalVersion = local,
                LatestVersion = latest,
                UpdateAvailable = available,
                WindowsZipUrl = zipUrl,
                GithubRepo = repo,
                Message = available
                    ? ServiceMessageCodec.Format("ApiUpdate.NewVersionFound", latest, local)
                    : ServiceMessageCodec.Format("ApiUpdate.AlreadyLatest", local)
            };
        }
        catch (Exception ex)
        {
            return Fail(local, ServiceMessageCodec.Format("ApiUpdate.Failed", ex.Message));
        }
    }

    public Task<BundledMusicApiUpdateApplyResult> DownloadAndApplyAsync(
        BundledMusicApiUpdateCheckResult check,
        CancellationToken ct = default) =>
        DownloadAndApplyAsync(check, progress: null, ct);

    public async Task<BundledMusicApiUpdateApplyResult> DownloadAndApplyAsync(
        BundledMusicApiUpdateCheckResult check,
        IProgress<MusicApiDownloadProgress>? progress,
        CancellationToken ct = default)
    {
        if (!check.UpdateAvailable || string.IsNullOrEmpty(check.WindowsZipUrl))
            return new BundledMusicApiUpdateApplyResult
            {
                Succeeded = false,
                Message = ServiceMessageCodec.Key("ApiUpdate.NoPendingUpdate")
            };

        var targetExe = BundledMusicApiHost.Instance.BundledExecutablePath;
        if (string.IsNullOrEmpty(targetExe))
            return new BundledMusicApiUpdateApplyResult
            {
                Succeeded = false,
                Message = ServiceMessageCodec.Key("ApiUpdate.ExeNotFound")
            };

        BundledMusicApiHost.Instance.StopIfStartedByApp();

        var tempRoot = Path.Combine(Path.GetTempPath(), "MusicVault", "api-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, WindowsAssetSuffix);

        try
        {
            progress?.Report(new MusicApiDownloadProgress(0, ServiceMessageCodec.Key("ApiUpdate.Progress.Connecting")));
            using (var response = await Http.GetAsync(check.WindowsZipUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;
                await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fs = File.Create(zipPath);
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await network.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    received += read;
                    var pct = total > 0 ? received * 100.0 / total.Value : Math.Min(95, received / 50000.0);
                    progress?.Report(new MusicApiDownloadProgress(pct, ServiceMessageCodec.Key("ApiUpdate.Progress.Downloading")));
                }
            }

            progress?.Report(new MusicApiDownloadProgress(96, ServiceMessageCodec.Key("ApiUpdate.Progress.Extracting")));
            var extractDir = Path.Combine(tempRoot, "extract");
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            var newExe = Directory.EnumerateFiles(extractDir, "go-music-api.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (newExe == null)
                return new BundledMusicApiUpdateApplyResult
                {
                    Succeeded = false,
                    Message = ServiceMessageCodec.Key("ApiUpdate.ZipMissingExe")
                };

            var backup = targetExe + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(targetExe)) File.Move(targetExe, backup);

            try
            {
                File.Copy(newExe, targetExe, overwrite: true);
            }
            catch
            {
                if (File.Exists(backup))
                    File.Move(backup, targetExe, overwrite: true);
                throw;
            }

            if (File.Exists(backup)) File.Delete(backup);

            var version = check.LatestVersion ?? "unknown";
            BundledMusicApiVersion.WriteLocalVersion(AppContext.BaseDirectory, version);

            progress?.Report(new MusicApiDownloadProgress(98, ServiceMessageCodec.Key("ApiUpdate.Progress.Restarting")));
            if (BundledMusicApiHost.IsLocalApiUrl(MusicApiConfig.Resolve()))
                await BundledMusicApiHost.Instance.EnsureRunningAsync(MusicApiConfig.Resolve()!, ct)
                    .ConfigureAwait(false);

            progress?.Report(new MusicApiDownloadProgress(100, ServiceMessageCodec.Key("ApiUpdate.Progress.Complete")));
            return new BundledMusicApiUpdateApplyResult
            {
                Succeeded = true,
                Message = ServiceMessageCodec.Format("ApiUpdate.Applied", version)
            };
        }
        catch (Exception ex)
        {
            return new BundledMusicApiUpdateApplyResult
            {
                Succeeded = false,
                Message = ServiceMessageCodec.Format("ApiUpdate.Failed", ex.Message)
            };
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
        }
    }

    private static BundledMusicApiUpdateCheckResult Fail(string local, string message) => new()
    {
        Succeeded = false,
        LocalVersion = local,
        Message = message,
        GithubRepo = ResolveGithubRepo()
    };

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }
}

public sealed class BundledMusicApiUpdateCheckResult
{
    public bool Succeeded { get; init; }
    public string? LocalVersion { get; init; }
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? WindowsZipUrl { get; init; }
    public string? GithubRepo { get; init; }
    public string Message { get; init; } = "";
}

public sealed class BundledMusicApiUpdateApplyResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";
}
