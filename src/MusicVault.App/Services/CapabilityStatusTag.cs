namespace MusicVault.App.Services;

/// <summary>平台能力状态键 → ant-tag 胶囊样式。</summary>
public static class CapabilityStatusTag
{
    private static readonly HashSet<string> SuccessKeys = new(StringComparer.Ordinal)
    {
        "Settings.Capability.Full",
        "Settings.Capability.FullLibVlc"
    };

    private static readonly HashSet<string> WarningKeys = new(StringComparer.Ordinal)
    {
        "Settings.Capability.PartialMacPlayback",
        "Settings.Capability.PartialNetwork",
        "Settings.Capability.PartialLibVlc"
    };

    private static readonly HashSet<string> ErrorKeys = new(StringComparer.Ordinal)
    {
        "Settings.Capability.Unavailable"
    };

    public static CapabilityStatusPresentation Present(string statusKeyOrLegacyText)
    {
        if (SuccessKeys.Contains(statusKeyOrLegacyText))
            return new(true, false, false);

        if (WarningKeys.Contains(statusKeyOrLegacyText))
            return new(false, true, false);

        if (ErrorKeys.Contains(statusKeyOrLegacyText))
            return new(false, false, true);

        return PresentLegacy(statusKeyOrLegacyText);
    }

    private static CapabilityStatusPresentation PresentLegacy(string status) =>
        new(
            status.Contains("完整", StringComparison.Ordinal)
            || status.Contains("Full", StringComparison.OrdinalIgnoreCase),
            status.Contains("部分", StringComparison.Ordinal)
            || status.Contains("Partial", StringComparison.OrdinalIgnoreCase),
            status.Contains("不可用", StringComparison.Ordinal)
            || status.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
}

public sealed record CapabilityStatusPresentation(bool IsSuccess, bool IsWarning, bool IsError);
