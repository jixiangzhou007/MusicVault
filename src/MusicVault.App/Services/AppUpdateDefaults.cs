namespace MusicVault.App.Services;

/// <summary>
/// 内置更新源默认值。公开仓库后把 <see cref="DefaultGithubRepoUrl"/> 改成你的仓库地址；
/// 用户仍可在设置中覆盖，或用环境变量 MUSICVAULT_UPDATE_URL。
/// </summary>
public static class AppUpdateDefaults
{
    /// <summary>
    /// Velopack GitHub 更新源（仓库根 URL，不是 releases 子路径）。
    /// 例：https://github.com/guohuiyuan/MusicVault
    /// 留空表示不内置默认源，必须由用户/环境变量配置。
    /// </summary>
    public const string DefaultGithubRepoUrl = "https://github.com/guohuiyuan/MusicVault";

    /// <summary>是否检查预发布版（GitHub prerelease）。</summary>
    public const bool IncludePrerelease = false;

    public static string? ResolveFeedUrl(string? settingsUrl, string? environmentUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(environmentUrl))
            return environmentUrl.Trim();
        if (!string.IsNullOrWhiteSpace(settingsUrl))
            return settingsUrl.Trim();
        if (!string.IsNullOrWhiteSpace(DefaultGithubRepoUrl))
            return DefaultGithubRepoUrl.Trim();
        return null;
    }

    public static bool IsGithubRepoUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
            return false;
        // /owner/repo （忽略多余路径段）
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2;
    }
}
