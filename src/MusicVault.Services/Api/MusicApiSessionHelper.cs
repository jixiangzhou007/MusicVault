using System.Text.Json;
using MusicVault.Services.Gatherers;

namespace MusicVault.Services.Api;

/// <summary>下载/登录前确保 go-music-api 可用，并将本地 Cookie 备份同步到 API。</summary>
public static class MusicApiSessionHelper
{
    private static string? _cookieBackupPath;
    private static DateTime _lastCookieSyncUtc = DateTime.MinValue;
    private static string? _lastCookieFingerprint;
    private static readonly TimeSpan CookieSyncMinInterval = TimeSpan.FromMinutes(5);

    public static void SetCookieBackupDirectory(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            _cookieBackupPath = null;
            return;
        }

        _cookieBackupPath = Path.Combine(dataDirectory, "music-api-cookies.json");
    }

    public static async Task<bool> EnsureReadyAsync(CancellationToken ct = default)
    {
        MusicServiceEndpoints.ApplyFromEnvironment();

        var baseDir = AppContext.BaseDirectory;
        BundledMusicApiHost.Instance.Discover(baseDir);

        var url = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(MusicApiConfig.BaseUrl);
        if (string.IsNullOrWhiteSpace(url) && BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            url = BundledMusicApiHost.DefaultBaseUrl;
            MusicApiConfig.BaseUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", url);
        }
        else if (!string.IsNullOrWhiteSpace(url))
        {
            MusicApiConfig.BaseUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", url);
        }

        if (string.IsNullOrWhiteSpace(url))
            return false;

        url = url.TrimEnd('/');
        if (BundledMusicApiHost.IsLocalApiUrl(url) && BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            if (!await BundledMusicApiHost.Instance.EnsureRunningAsync(url, ct).ConfigureAwait(false))
                return false;
        }
        else if (!await BundledMusicApiHost.IsHealthyAsync(url, quick: false, ct).ConfigureAwait(false))
        {
            return false;
        }

        await SyncStoredCookiesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public static async Task SaveCookiesBackupAsync(
        IReadOnlyDictionary<string, string> cookies,
        CancellationToken ct = default)
    {
        cookies = NormalizePlatformCookies(cookies);
        if (cookies.Count == 0 || string.IsNullOrWhiteSpace(_cookieBackupPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(_cookieBackupPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_cookieBackupPath, json, ct).ConfigureAwait(false);
            InvalidateCookieSyncCache();
            PlatformLoginStateNotifier.NotifyChanged();
        }
        catch
        {
            // 备份失败不阻断登录
        }
    }

    public static void InvalidateCookieSyncCache()
    {
        _lastCookieSyncUtc = DateTime.MinValue;
        _lastCookieFingerprint = null;
    }

    public static async Task SyncStoredCookiesAsync(CancellationToken ct = default, bool qqOnly = false)
    {
        var cookies = NormalizePlatformCookies(await TryLoadCookiesBackupAsync(ct).ConfigureAwait(false));
        if (cookies.Count == 0 || !GoMusicApiAccountClient.IsConfigured)
            return;

        if (qqOnly && !HasQqCookies(cookies))
            return;

        var fingerprint = BuildCookieFingerprint(cookies);
        if (_lastCookieFingerprint == fingerprint
            && DateTime.UtcNow - _lastCookieSyncUtc < CookieSyncMinInterval)
            return;

        await GoMusicApiAccountClient.PostCookiesAsync(cookies, ct).ConfigureAwait(false);
        _lastCookieSyncUtc = DateTime.UtcNow;
        _lastCookieFingerprint = fingerprint;
    }

    private static bool HasQqCookies(IReadOnlyDictionary<string, string> cookies) =>
        cookies.Keys.Any(k => k.Equals("qq", StringComparison.OrdinalIgnoreCase)
                           || k.Equals("qq_wx", StringComparison.OrdinalIgnoreCase));

    private static string BuildCookieFingerprint(IReadOnlyDictionary<string, string> cookies) =>
        string.Join('\n', cookies.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}={kv.Value}"));

    /// <summary>微信扫码登录写入 qq_wx，搜索/下载使用 qq 源，需互为别名。</summary>
    public static IReadOnlyDictionary<string, string> NormalizePlatformCookies(
        IReadOnlyDictionary<string, string> cookies)
    {
        if (cookies.Count == 0)
            return cookies;

        var map = cookies.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        if (map.TryGetValue("qq_wx", out var wx) && !string.IsNullOrWhiteSpace(wx)
            && (!map.TryGetValue("qq", out var qq) || string.IsNullOrWhiteSpace(qq)))
            map["qq"] = wx;

        if (map.TryGetValue("qq", out var qqCookie) && !string.IsNullOrWhiteSpace(qqCookie)
            && (!map.TryGetValue("qq_wx", out var wxCookie) || string.IsNullOrWhiteSpace(wxCookie)))
            map["qq_wx"] = qqCookie;

        return map;
    }

    private static async Task<IReadOnlyDictionary<string, string>> TryLoadCookiesBackupAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_cookieBackupPath) || !File.Exists(_cookieBackupPath))
            return new Dictionary<string, string>();

        try
        {
            await using var stream = File.OpenRead(_cookieBackupPath);
            var map = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            if (map == null || map.Count == 0)
                return new Dictionary<string, string>();

            return map.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
