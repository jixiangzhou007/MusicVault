namespace MusicVault.Services.Api;

/// <summary>听歌识曲提供方标识（设置项 MusicRecognitionApiUrl）。</summary>
public static class MusicRecognitionProviders
{
    /// <summary>使用捆绑的网易云 API（/audio/match + 本地指纹）。</summary>
    public const string NetEaseBundled = "netease://bundled";

    public static bool IsNetEaseProvider(string? apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return true;

        var trimmed = apiUrl.Trim();
        return trimmed.Equals(NetEaseBundled, StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("netease://", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternalHttpProvider(string? apiUrl) =>
        !string.IsNullOrWhiteSpace(apiUrl)
        && Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
