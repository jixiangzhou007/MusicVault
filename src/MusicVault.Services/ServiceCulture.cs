using System.Text.Json;

namespace MusicVault.Services;

/// <summary>服务层 UI 语言（与 settings.json Culture 同步，供无 ILocalization 的路径使用）。</summary>
public static class ServiceCulture
{
    public const string ZhCn = "zh-CN";
    public const string EnUs = "en-US";

    private static string _current = ZhCn;

    public static string Current => _current;

    public static void SetCurrent(string? culture) =>
        _current = Normalize(culture);

    public static void RefreshFromDataDirectory(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            return;

        var path = Path.Combine(dataDirectory, "settings.json");
        if (!File.Exists(path))
            return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Culture", out var culture)
                && culture.ValueKind == JsonValueKind.String)
                SetCurrent(culture.GetString());
        }
        catch
        {
            // 设置损坏时保留当前/默认语言
        }
    }

    public static string Normalize(string? culture) =>
        !string.IsNullOrWhiteSpace(culture)
        && culture.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? EnUs
            : ZhCn;
}
