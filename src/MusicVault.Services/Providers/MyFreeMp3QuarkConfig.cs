namespace MusicVault.Services.Providers;

/// <summary>
/// MyFreeMP3 夸克通道配置（参考 musicdl quark_parser_config.cookies）。
/// 在「偏好」填写夸克网盘 Cookie 后可搜索/解析 type=kuake 结果。
/// </summary>
public static class MyFreeMp3QuarkConfig
{
    public static string? Cookies { get; private set; }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Cookies);

    public static void Apply(string? cookies) =>
        Cookies = string.IsNullOrWhiteSpace(cookies) ? null : cookies.Trim();
}
