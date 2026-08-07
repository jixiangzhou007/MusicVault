namespace MusicVault.Services.Api;

/// <summary>偏好与环境变量中的音乐服务地址（艺术家资料、专辑、搜索共用）。</summary>
public static class MusicServiceEndpoints
{
    public const string DefaultNetEaseApiUrl = "http://localhost:3000";
    public const string DefaultQQMusicApiUrl = "https://api.qq.jsososo.com";

    /// <summary>由 <see cref="Gatherers.MusicGathererRegistry"/> 在启动/保存设置时注入。</summary>
    public static string? NetEaseApiUrl { get; set; }

    /// <summary>由 <see cref="Gatherers.MusicGathererRegistry"/> 在启动/保存设置时注入。</summary>
    public static string? QQMusicApiUrl { get; set; }

    public static string? ResolveMusicApi() => MusicApiConfig.Resolve();

    public static string? ResolveNetEaseApi()
    {
        var fromSettings = NetEaseApiUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(fromSettings))
            return fromSettings;

        var env = Environment.GetEnvironmentVariable("MUSICVAULT_NETEASE_API")?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(env) ? null : env;
    }

    public static string? ResolveQQApi()
    {
        var fromSettings = QQMusicApiUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(fromSettings))
            return fromSettings;

        var env = Environment.GetEnvironmentVariable("MUSICVAULT_QQ_API")?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(env) ? null : env;
    }

    public static void ApplyFromEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("MUSICVAULT_NETEASE_API")?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(env))
            NetEaseApiUrl = env;

        var qq = Environment.GetEnvironmentVariable("MUSICVAULT_QQ_API")?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(qq))
            QQMusicApiUrl = qq;
    }
}
