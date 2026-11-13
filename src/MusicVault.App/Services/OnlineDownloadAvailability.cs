namespace MusicVault.App.Services;

/// <summary>
/// 在线曲目下载是否开放。不按操作系统一刀切：Mac 配了统一 API 就保留下载。
/// </summary>
public static class OnlineDownloadAvailability
{
    public const string MacOnlinePlayOnlyHintKey = "Download.Disabled.OnlinePlayOnly";
    public const string ConfigureApiHintKey = "Download.Disabled.ConfigureApi";
    public const string TransfersHintKey = "Transfers.Workflow.Hint";
    public const string TransfersOnlinePlayOnlyHintKey = "Transfers.Workflow.Hint.OnlinePlayOnly";
    public const string TransfersConfigureApiHintKey = "Transfers.Workflow.Hint.ConfigureApi";

    public static bool IsAvailable(bool unifiedApiConfigured) => unifiedApiConfigured;

    public static string DisabledHintKey(bool isMacOs) =>
        isMacOs ? MacOnlinePlayOnlyHintKey : ConfigureApiHintKey;

    public static string WorkflowHintKey(bool unifiedApiConfigured, bool isMacOs) =>
        IsAvailable(unifiedApiConfigured)
            ? TransfersHintKey
            : isMacOs ? TransfersOnlinePlayOnlyHintKey : TransfersConfigureApiHintKey;
}
