namespace MusicVault.App.Localization;

using MusicVault.Core.Enums;

/// <summary>传输列表行与清除确认文案。</summary>
public static class TransferRowLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;

    public static string ClearSelectedTitleOne(string displayName) =>
        string.Format(G("Transfers.ClearSelected.TitleOne"), displayName);

    public static string ClearSelectedDescriptionWithFiles =>
        G("Transfers.ClearSelected.DescriptionWithFiles");

    public static string ClearSelectedDescriptionListOnly =>
        G("Transfers.ClearSelected.DescriptionListOnly");

    public static string ClearSelectedConfirmListOnly =>
        G("Transfers.ClearSelected.ConfirmListOnly");

    public static string ClearSelectedConfirmDefault =>
        G("Transfers.ClearSelected.ConfirmDefault");

    public static string ClearSelectedDeleteFiles =>
        G("Transfers.ClearSelected.DeleteFiles");

    public static string CancelDownloadTitle => G("Transfers.CancelDownload.Title");

    public static string CancelDownloadDescription => G("Transfers.CancelDownload.Description");

    public static string CancelDownloadConfirm => G("Transfers.CancelDownload.Confirm");

    public static string RemoveRecordModalTitle => G("Transfers.RemoveRecord.ModalTitle");

    public static string RemoveRecordListAndFile => G("Transfers.RemoveRecord.ListAndFile");

    public static string ModalCancel => G("Common.Cancel");

    public static string StatusCompleted => G("Transfers.Row.Status.Completed");

    public static string StatusFailed => G("Transfers.Row.Status.Failed");

    public static string StatusDownloading => G("Transfers.Row.Status.Downloading");

    public static string StatusPending => G("Transfers.Row.Status.Pending");

    public static string StatusPaused => G("Transfers.Row.Status.Paused");

    public static string StatusCancelled => G("Transfers.Row.Status.Cancelled");

    public static string StatusSuccess => G("Transfers.Row.Status.Success");

    public static string StatusUnknown => G("Transfers.Row.Status.Unknown");

    public static string ProgressProcessing => G("Transfers.Row.Progress.Processing");

    public static string ProgressWaiting => G("Transfers.Row.Progress.Waiting");

    public static string ProgressPaused => G("Transfers.Row.Progress.PausedShort");

    public static string ProgressCancelled => G("Transfers.Row.Progress.CancelledShort");

    public static string ProgressEllipsis => G("Transfers.Row.Progress.Ellipsis");

    public static string ToolTipCompleted => G("Transfers.Row.ToolTip.Completed");

    public static string ToolTipCompletedAt(string finishedAt) =>
        string.Format(G("Transfers.Row.ToolTip.CompletedAt"), finishedAt);

    public static string ToolTipDownloading => G("Transfers.Row.ToolTip.Downloading");

    public static string ToolTipDownloadingPercent(int percent) =>
        string.Format(G("Transfers.Row.ToolTip.DownloadingPercent"), percent);

    public static string ToolTipPending => G("Transfers.Row.ToolTip.Pending");

    public static string ToolTipPaused => G("Transfers.Row.ToolTip.Paused");

    public static string ToolTipCancelled => G("Transfers.Row.ToolTip.Cancelled");

    public static string ToolTipFailed => G("Transfers.Row.ToolTip.Failed");

    public static string ToolTipFailedAt(string finishedAt) =>
        string.Format(G("Transfers.Row.ToolTip.FailedAt"), finishedAt);

    public static string DownloadStatusLabel(DownloadStatus status) => status switch
    {
        DownloadStatus.Completed => StatusCompleted,
        DownloadStatus.Failed => StatusFailed,
        DownloadStatus.Downloading => StatusDownloading,
        DownloadStatus.Pending => StatusPending,
        DownloadStatus.Paused => StatusPaused,
        DownloadStatus.Cancelled => StatusCancelled,
        _ => StatusUnknown
    };
}
