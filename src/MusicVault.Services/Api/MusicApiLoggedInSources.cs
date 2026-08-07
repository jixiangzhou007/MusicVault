using MusicVault.Core.Enums;

namespace MusicVault.Services.Api;

/// <summary>检测 go-music-api 已登录平台，供下载/在线播放优先选用。</summary>
public static class MusicApiLoggedInSources
{
    private static readonly string[] SourcePriority =
        ["netease", "qq", "qq_wx", "kuwo", "migu", "kugou", "soda"];

    public static async Task<IReadOnlyList<string>> GetLoggedInSourceKeysAsync(CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured)
            return [];

        await MusicApiSessionHelper.SyncStoredCookiesAsync(ct).ConfigureAwait(false);
        var cookies = MusicApiSessionHelper.NormalizePlatformCookies(
            await GoMusicApiAccountClient.GetCookiesAsync(ct).ConfigureAwait(false));
        if (cookies.Count == 0)
            return [];

        return SourcePriority
            .Where(s => cookies.TryGetValue(s, out var value) && !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    public static string? TryGetSourceKey(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "netease",
        MusicPlatform.QQMusic => "qq",
        MusicPlatform.Kugou => "kugou",
        MusicPlatform.Kuwo => "kuwo",
        MusicPlatform.Migu => "migu",
        MusicPlatform.Soda => "soda",
        _ => null
    };

    public static bool IsPlatformLoggedIn(MusicPlatform platform, IReadOnlyCollection<string> loggedIn)
    {
        var key = TryGetSourceKey(platform);
        return key != null && IsSourceLoggedIn(key, loggedIn);
    }

    public static bool IsSourceLoggedIn(string? source, IReadOnlyCollection<string> loggedIn) =>
        !string.IsNullOrWhiteSpace(source)
        && loggedIn.Any(s => string.Equals(s, source, StringComparison.OrdinalIgnoreCase)
                            || (IsQqFamily(source) && IsQqFamily(s)));

    private static bool IsQqFamily(string source) =>
        string.Equals(source, "qq", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, "qq_wx", StringComparison.OrdinalIgnoreCase);
}
