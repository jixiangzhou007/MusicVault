namespace MusicVault.Services.Api;

/// <summary>go-music-api / music-lib 统一后端配置（对齐洛雪音源思路）</summary>
public static class MusicApiConfig
{
    /// <summary>由应用在启动/保存设置时注入，例如 http://localhost:8080</summary>
    public static string? BaseUrl { get; set; }

    public static string? Resolve()
    {
        var fromSettings = BaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(fromSettings)) return fromSettings;

        var env = Environment.GetEnvironmentVariable("MUSICVAULT_MUSIC_API")?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(env) ? null : env;
    }
}
