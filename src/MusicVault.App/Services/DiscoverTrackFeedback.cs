namespace MusicVault.App.Services;

/// <summary>发现/在线曲目行操作反馈（试听、入队、收藏等）。</summary>
public enum DiscoverTrackFeedbackKind
{
    Info,
    Success,
    Warning,
    Error
}

public readonly record struct DiscoverTrackFeedback(
    string Message,
    DiscoverTrackFeedbackKind Kind,
    bool ShowToast = true,
    bool RevealTransfersPane = false);
