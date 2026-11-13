namespace MusicVault.App.Services;

public readonly record struct DiscoverSourceFailureBannerModel(
    bool Show,
    string Message,
    bool ShowRetry,
    bool ShowOpenSettings);

/// <summary>
/// 发现页源失败横幅：有具体搜索部分失败文案时优先展示，否则回落到 API 健康摘要。
/// </summary>
public static class DiscoverSourceFailureBanner
{
    public static DiscoverSourceFailureBannerModel Resolve(
        string? partialSearchFailureText,
        bool apiHealthBad,
        string? apiHealthText)
    {
        if (!string.IsNullOrWhiteSpace(partialSearchFailureText))
            return new(true, partialSearchFailureText.Trim(), ShowRetry: true, ShowOpenSettings: true);

        if (apiHealthBad && !string.IsNullOrWhiteSpace(apiHealthText))
            return new(true, apiHealthText.Trim(), ShowRetry: true, ShowOpenSettings: true);

        return new(false, string.Empty, false, false);
    }
}
