namespace MusicVault.Services.Metadata;

/// <summary>
/// AcoustID 指纹 lookup 运行时配置（由应用设置页同步；环境变量可覆盖 API Key）。
/// </summary>
public static class AcoustIdLookupConfiguration
{
    public static bool Enabled { get; private set; }

    public static string? ApiKey { get; private set; }

    public static void Apply(bool enabled, string? apiKey)
    {
        Enabled = enabled;
        ApiKey = apiKey;
    }

    internal static string? ResolveApiKey()
    {
        var env = Environment.GetEnvironmentVariable("MUSICVAULT_ACOUSTID_API_KEY")?.Trim();
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var key = ApiKey?.Trim();
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
