namespace MusicVault.Services.Api;

/// <summary>
/// 发现搜索栏与平台登录抽屉共用的「是否可扫码登录」判定，以及图标彩色/置灰规则。
/// </summary>
public static class PlatformQrLoginSupport
{
    /// <summary>
    /// 是否可走扫码登录。
    /// API 未配置 → false；远端已返回列表 → 以远端为准（QQ 家族 qq/qq_wx 互通）；
    /// 远端为空（探测失败）→ 回退已知默认可扫源，避免整栏误灰。
    /// </summary>
    public static bool IsSupported(
        string? source,
        IReadOnlyCollection<string> remoteQrSources,
        bool apiConfigured)
    {
        if (!apiConfigured || string.IsNullOrWhiteSpace(source))
            return false;

        var key = source.Trim();
        var hasRemote = remoteQrSources.Count > 0;

        if (IsQqFamily(key))
        {
            if (hasRemote)
                return Contains(remoteQrSources, "qq") || Contains(remoteQrSources, "qq_wx");
            return GoMusicApiAccountClient.IsKnownQrCapableSource("qq")
                   || GoMusicApiAccountClient.IsKnownQrCapableSource("qq_wx");
        }

        if (hasRemote)
            return Contains(remoteQrSources, key);

        return GoMusicApiAccountClient.IsKnownQrCapableSource(key);
    }

    /// <summary>彩色图标：已登录，或当前可扫码登录。</summary>
    public static bool ShowColorIcon(bool isLoggedIn, bool supportsQr) =>
        isLoggedIn || supportsQr;

    private static bool IsQqFamily(string source) =>
        string.Equals(source, "qq", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, "qq_wx", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, "qqmusic", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(IReadOnlyCollection<string> sources, string key) =>
        sources.Any(s => string.Equals(s, key, StringComparison.OrdinalIgnoreCase));
}
