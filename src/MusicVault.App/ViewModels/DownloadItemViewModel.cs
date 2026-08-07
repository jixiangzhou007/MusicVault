using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Platforms;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.App.ViewModels;

public partial class DownloadItemViewModel : ViewModelBase
{
    private readonly IPlatformService _platform;

    public DownloadItem Item { get; private set; }

    [ObservableProperty] private int _rowIndex;

    [ObservableProperty] private bool _isSelected;

    public string RowIndexText => RowIndex > 0 ? RowIndex.ToString() : string.Empty;

    /// <summary>由 <see cref="DownloadsPageViewModel"/> 注入，供 Popconfirm 确认后清除记录。</summary>
    public IAsyncRelayCommand<DownloadItemViewModel?>? RemoveListOnlyCommand { get; }

    public IAsyncRelayCommand<DownloadItemViewModel?>? RemoveWithFileCommand { get; }

    public bool CanDeleteLocalFile => DownloadOutcomeHelper.HasValidAudioFile(Item);

    public string RemovePopconfirmTitle => TransferRowLabels.ClearSelectedTitleOne(DisplayName);

    public string RemovePopconfirmDescription => CanDeleteLocalFile
        ? TransferRowLabels.ClearSelectedDescriptionWithFiles
        : TransferRowLabels.ClearSelectedDescriptionListOnly;

    public string RemoveConfirmText => CanDeleteLocalFile
        ? TransferRowLabels.ClearSelectedConfirmListOnly
        : TransferRowLabels.ClearSelectedConfirmDefault;

    public string RemoveAlternateConfirmText => TransferRowLabels.ClearSelectedDeleteFiles;

    public string CancelDownloadPopconfirmTitle => TransferRowLabels.CancelDownloadTitle;

    public string CancelDownloadPopconfirmDescription => TransferRowLabels.CancelDownloadDescription;

    public string CancelDownloadConfirmText => TransferRowLabels.CancelDownloadConfirm;

    public IRelayCommand<DownloadItemViewModel?>? RetryDownloadCommand { get; }

    public IRelayCommand<DownloadItemViewModel?>? PauseDownloadCommand { get; }

    public IRelayCommand<DownloadItemViewModel?>? ResumeDownloadCommand { get; }

    public IRelayCommand<DownloadItemViewModel?>? CancelDownloadCommand { get; }

    public IRelayCommand<DownloadItemViewModel?>? CopyFailedErrorCommand { get; }

    public IRelayCommand<DownloadItemViewModel?>? SearchFailedInDiscoverCommand { get; }

    public string CopyFailedErrorToolTip { get; }

    public string SearchFailedInDiscoverToolTip { get; }

    public string PauseDownloadToolTip => TrackRowActionLabels.PauseDownload;
    public string ResumeDownloadToolTip => TrackRowActionLabels.ResumeDownload;
    public string CancelDownloadToolTip => TrackRowActionLabels.CancelDownload;
    public string RetryDownloadToolTip => TrackRowActionLabels.RetryDownload;
    public string DeleteRecordToolTip => TrackRowActionLabels.DeleteRecord;
    public string RevealInFolderToolTip =>
        string.Format(TrackRowActionLabels.OpenFolderFormat, DisplayName);

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.RowCheckboxName(Title);

    public string RowCheckboxAccessibilityHelp => TrackRowActionLabels.RowCheckboxHelp(Title, Artist);

    public bool ShowFailedRecoveryActions => NeedsRecovery;

    public bool CanRetry => NeedsRecovery;

    public bool IsPaused => Item.Status == DownloadStatus.Paused;

    public bool CanPause => DownloadControlRules.CanPause(Item.Status);

    public bool CanResume => DownloadControlRules.CanResume(Item.Status);

    public bool CanCancel => DownloadControlRules.CanCancel(Item.Status);

    public string TargetPathDisplay => string.IsNullOrWhiteSpace(Item.TargetPath)
        ? string.Empty
        : Item.TargetPath;

    public bool HasTargetPath => !string.IsNullOrWhiteSpace(Item.TargetPath);

    public string Title => string.IsNullOrWhiteSpace(Item.DisplayTitle) ? Item.Track.Title : Item.DisplayTitle;
    public string Artist => string.IsNullOrWhiteSpace(Item.DisplayArtist) ? Item.Track.Artist : Item.DisplayArtist;

    public string DisplayName => string.IsNullOrEmpty(Item.TargetPath)
        ? Title
        : Path.GetFileName(Item.TargetPath);

    public string FormatLabel => DownloadQualityLabelLocalizer.Describe(
        App.Localization,
        Item.Quality,
        Item.TargetPath,
        Item.Track.Platform);

    public string FormatDisplay => DownloadQualityLabelLocalizer.Chip(
        App.Localization,
        Item.Quality,
        Item.Track.Platform);

    public double Progress => Item.Progress;

    public bool IsDownloading => Item.Status == DownloadStatus.Downloading;
    public bool IsPending => Item.Status == DownloadStatus.Pending;
    public bool IsActive => IsDownloading || IsPending;
    public bool IsCompleted => DownloadOutcomeHelper.ShouldTreatAsCompleted(Item);

    public bool IsFailed =>
        Item.Status == DownloadStatus.Failed && !DownloadOutcomeHelper.HasValidAudioFile(Item);

    public bool IsCancelled => Item.Status == DownloadStatus.Cancelled;

    /// <summary>失败或已取消：可在失败视图中重试 / 复制原因。</summary>
    public bool NeedsRecovery => IsFailed || IsCancelled;

    /// <summary>下载中/等待时在行底显示进度条。</summary>
    public bool ShowProgressBar => IsActive;

    public bool ShowStatusSuccess => IsCompleted && !IsFailed;

    public bool ShowStatusFailed => NeedsRecovery;

    public bool ShowStatusActive => IsActive && !IsCompleted && !IsFailed;

    /// <summary>仅在尚未收到任何字节且无法估算总量时使用不确定进度。</summary>
    public bool IsProgressIndeterminate =>
        IsDownloading && !Item.BytesDownloadComplete && Item.DownloadedBytes <= 0 && Item.EffectiveTotalBytes <= 0;

    public string ProgressStatusText => Item.Status switch
    {
        DownloadStatus.Downloading when Item.BytesDownloadComplete => TransferRowLabels.ProgressProcessing,
        DownloadStatus.Downloading when Item.EffectiveTotalBytes > 0 => $"{(int)Progress}%",
        DownloadStatus.Downloading => TransferRowLabels.ProgressEllipsis,
        DownloadStatus.Pending => TransferRowLabels.ProgressWaiting,
        DownloadStatus.Paused => TransferRowLabels.ProgressPaused,
        DownloadStatus.Failed => string.Empty,
        DownloadStatus.Cancelled => TransferRowLabels.ProgressCancelled,
        _ => string.Empty
    };

    /// <summary>速览行尾：进行中显示进度，其余显示音质格式。</summary>
    public string TrailingProgressText => ProgressStatusText;

    public bool ShowTrailingFormat => !IsActive;

    public bool ShowTrailingProgress => IsActive;

    public string ProgressPercentText => $"{(int)Progress}%";

    public bool ShowProgressPercent =>
        IsDownloading && !IsProgressIndeterminate && Item.EffectiveTotalBytes > 0;

    /// <summary>仅进行中/失败/取消显示状态点；已完成不再显示勾号。</summary>
    public bool ShowStatusDot => IsActive || NeedsRecovery;

    public string StatusDotToolTip => StatusBadgeToolTip;

    public string FinishedAtText
    {
        get
        {
            if (!Item.FinishedAtUtc.HasValue)
                return string.Empty;

            var local = Item.FinishedAtUtc.Value.ToLocalTime();
            var today = DateTime.Today;
            if (local.Date == today)
                return local.ToString("HH:mm");
            if (local.Year == today.Year)
                return local.ToString("M-d HH:mm");
            return local.ToString("yyyy-M-d");
        }
    }

    public string StatusBadgeToolTip => Item.Status switch
    {
        DownloadStatus.Completed => string.IsNullOrEmpty(FinishedAtText)
            ? TransferRowLabels.ToolTipCompleted
            : TransferRowLabels.ToolTipCompletedAt(FinishedAtText),
        DownloadStatus.Failed => BuildFailedStatusToolTip(),
        DownloadStatus.Downloading => Item.TotalBytes > 0
            ? TransferRowLabels.ToolTipDownloadingPercent((int)Progress)
            : TransferRowLabels.ToolTipDownloading,
        DownloadStatus.Pending => TransferRowLabels.ToolTipPending,
        DownloadStatus.Paused => TransferRowLabels.ToolTipPaused,
        DownloadStatus.Cancelled => BuildCancelledStatusToolTip(),
        _ => StatusLabel
    };
    /// <summary>失败且无有效音频文件时不显示「打开文件夹」；进行中/成功等保留。</summary>
    public bool CanRevealInFolder => HasTargetPath && !IsFailed;

    public string StatusLabel => TransferRowLabels.DownloadStatusLabel(Item.Status);

    public string QualityChip => FormatDisplay;

    public string StatusChipText
    {
        get
        {
            if (NeedsRecovery)
            {
                var shortReason = FailureReasonShort;
                return string.IsNullOrWhiteSpace(shortReason)
                    ? (IsCancelled ? TransferRowLabels.ProgressCancelled : TransferRowLabels.StatusFailed)
                    : shortReason;
            }

            if (IsCompleted)
                return TransferRowLabels.StatusSuccess;
            return ProgressStatusText;
        }
    }

    public bool UseSuccessStatusChip => ShowStatusSuccess;

    public bool UseFailedStatusChip => NeedsRecovery;

    /// <summary>本地化后的失败/取消原因（完整）。</summary>
    public string FailureReasonText
    {
        get
        {
            if (IsCancelled && string.IsNullOrWhiteSpace(Item.ErrorMessage))
                return ServiceMessageLocalizer.Resolve(App.Localization, ServiceMessageCodec.Key("Download.CancelledRetry"));
            var resolved = LocalizedErrorMessage;
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
            return IsFailed
                ? ServiceMessageLocalizer.Resolve(App.Localization, ServiceMessageCodec.Key("Download.FailedRetry"))
                : string.Empty;
        }
    }

    public bool HasFailureReason => !string.IsNullOrWhiteSpace(FailureReasonText);

    /// <summary>行内 chip 用短原因。</summary>
    public string FailureReasonShort
    {
        get
        {
            var text = FailureReasonText.Trim();
            if (text.Length == 0)
                return string.Empty;
            return text.Length <= 14 ? text : text[..13] + "…";
        }
    }

    public string SizeText
    {
        get
        {
            if (IsActive && Item.EffectiveTotalBytes > 0)
                return $"{FormatBytes(Item.DownloadedBytes)} / {FormatBytes(Item.EffectiveTotalBytes)}";

            var bytes = ResolveFileBytes();
            return bytes > 0 ? FormatBytes(bytes) : "—";
        }
    }

    public bool CanPlay => IsCompleted
        ? !string.IsNullOrWhiteSpace(Item.TargetPath) && File.Exists(Item.TargetPath)
        : Item.Track != null;

    public DownloadItemViewModel(
        DownloadItem item,
        IPlatformService platform,
        Action onChanged,
        IAsyncRelayCommand<DownloadItemViewModel?>? removeListOnlyCommand = null,
        IAsyncRelayCommand<DownloadItemViewModel?>? removeWithFileCommand = null,
        IRelayCommand<DownloadItemViewModel?>? retryDownloadCommand = null,
        IRelayCommand<DownloadItemViewModel?>? pauseDownloadCommand = null,
        IRelayCommand<DownloadItemViewModel?>? resumeDownloadCommand = null,
        IRelayCommand<DownloadItemViewModel?>? cancelDownloadCommand = null,
        IRelayCommand<DownloadItemViewModel?>? copyFailedErrorCommand = null,
        IRelayCommand<DownloadItemViewModel?>? searchFailedInDiscoverCommand = null,
        string copyFailedErrorToolTip = "",
        string searchFailedInDiscoverToolTip = "")
    {
        Item = item;
        _platform = platform;
        RemoveListOnlyCommand = removeListOnlyCommand;
        RemoveWithFileCommand = removeWithFileCommand;
        RetryDownloadCommand = retryDownloadCommand;
        PauseDownloadCommand = pauseDownloadCommand;
        ResumeDownloadCommand = resumeDownloadCommand;
        CancelDownloadCommand = cancelDownloadCommand;
        CopyFailedErrorCommand = copyFailedErrorCommand;
        SearchFailedInDiscoverCommand = searchFailedInDiscoverCommand;
        CopyFailedErrorToolTip = copyFailedErrorToolTip;
        SearchFailedInDiscoverToolTip = searchFailedInDiscoverToolTip;
        TrackRowActionLabels.LabelsChanged += OnActionLabelsChanged;
        TransferRowLabels.LabelsChanged += OnTransferLabelsChanged;
        App.Localization.CultureChanged += OnLocalizationCultureChanged;
        _ = onChanged;
    }

    private void OnLocalizationCultureChanged(object? sender, EventArgs e) => NotifyLocalizedProperties();

    private void OnTransferLabelsChanged(object? sender, EventArgs e) => NotifyLocalizedProperties();

    public void RefreshLocalizedDisplay() => NotifyLocalizedProperties();

    private void NotifyLocalizedProperties()
    {
        OnPropertyChanged(nameof(RemovePopconfirmTitle));
        OnPropertyChanged(nameof(RemovePopconfirmDescription));
        OnPropertyChanged(nameof(RemoveConfirmText));
        OnPropertyChanged(nameof(RemoveAlternateConfirmText));
        OnPropertyChanged(nameof(CancelDownloadPopconfirmTitle));
        OnPropertyChanged(nameof(CancelDownloadPopconfirmDescription));
        OnPropertyChanged(nameof(CancelDownloadConfirmText));
        OnPropertyChanged(nameof(ProgressStatusText));
        OnPropertyChanged(nameof(TrailingProgressText));
        OnPropertyChanged(nameof(StatusBadgeToolTip));
        OnPropertyChanged(nameof(StatusDotToolTip));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusChipText));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(FormatDisplay));
        OnPropertyChanged(nameof(QualityChip));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    private void OnActionLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PauseDownloadToolTip));
        OnPropertyChanged(nameof(ResumeDownloadToolTip));
        OnPropertyChanged(nameof(CancelDownloadToolTip));
        OnPropertyChanged(nameof(RetryDownloadToolTip));
        OnPropertyChanged(nameof(DeleteRecordToolTip));
        OnPropertyChanged(nameof(RevealInFolderToolTip));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    public void ReplaceItem(DownloadItem item)
    {
        if (item.Id != Item.Id)
            return;

        Item = item;
        SyncFromItem();
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(CanDeleteLocalFile));
        OnPropertyChanged(nameof(RemovePopconfirmTitle));
        OnPropertyChanged(nameof(RemovePopconfirmDescription));
        OnPropertyChanged(nameof(RemoveConfirmText));
    }

    public void SetRowIndex(int rowIndex)
    {
        if (RowIndex == rowIndex)
            return;

        RowIndex = rowIndex;
        OnPropertyChanged(nameof(RowIndexText));
    }

    public void SyncFromItem()
    {
        DownloadOutcomeHelper.ReconcileStatus(Item);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RevealInFolderToolTip));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
        OnPropertyChanged(nameof(CanDeleteLocalFile));
        OnPropertyChanged(nameof(RemovePopconfirmTitle));
        OnPropertyChanged(nameof(RemovePopconfirmDescription));
        OnPropertyChanged(nameof(RemoveConfirmText));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(FormatDisplay));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(NeedsRecovery));
        OnPropertyChanged(nameof(ShowProgressBar));
        OnPropertyChanged(nameof(ShowStatusSuccess));
        OnPropertyChanged(nameof(ShowStatusFailed));
        OnPropertyChanged(nameof(ShowStatusActive));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        OnPropertyChanged(nameof(ProgressStatusText));
        OnPropertyChanged(nameof(TrailingProgressText));
        OnPropertyChanged(nameof(ShowTrailingFormat));
        OnPropertyChanged(nameof(ShowTrailingProgress));
        OnPropertyChanged(nameof(ProgressPercentText));
        OnPropertyChanged(nameof(ShowProgressPercent));
        OnPropertyChanged(nameof(ShowStatusDot));
        OnPropertyChanged(nameof(StatusBadgeToolTip));
        OnPropertyChanged(nameof(StatusDotToolTip));
        OnPropertyChanged(nameof(CanRevealInFolder));
        RevealInFolderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(QualityChip));
        OnPropertyChanged(nameof(StatusChipText));
        OnPropertyChanged(nameof(UseSuccessStatusChip));
        OnPropertyChanged(nameof(UseFailedStatusChip));
        OnPropertyChanged(nameof(FailureReasonText));
        OnPropertyChanged(nameof(FailureReasonShort));
        OnPropertyChanged(nameof(HasFailureReason));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(ShowFailedRecoveryActions));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(TargetPathDisplay));
        OnPropertyChanged(nameof(HasTargetPath));
    }

    [RelayCommand(CanExecute = nameof(CanRevealInFolder))]
    private void RevealInFolder()
    {
        if (!CanRevealInFolder)
            return;
        _platform.RevealInFileManager(Item.TargetPath);
    }

    private string LocalizedErrorMessage =>
        ServiceMessageLocalizer.Resolve(
            App.Localization,
            string.IsNullOrWhiteSpace(Item.ErrorMessage)
                ? null
                : DownloadFailureMessageMapper.MapStored(Item.ErrorMessage));

    private string BuildFailedStatusToolTip()
    {
        var error = FailureReasonText;
        var hasError = !string.IsNullOrWhiteSpace(error);
        var hasTime = !string.IsNullOrEmpty(FinishedAtText);

        if (hasError && hasTime)
            return $"{error} · {FinishedAtText}";
        if (hasError)
            return error;
        if (hasTime)
            return TransferRowLabels.ToolTipFailedAt(FinishedAtText);
        return TransferRowLabels.ToolTipFailed;
    }

    private string BuildCancelledStatusToolTip()
    {
        var error = FailureReasonText;
        return string.IsNullOrWhiteSpace(error)
            ? TransferRowLabels.ToolTipCancelled
            : error;
    }

    private long ResolveFileBytes()
    {
        if (!string.IsNullOrWhiteSpace(Item.TargetPath) && File.Exists(Item.TargetPath))
            return new FileInfo(Item.TargetPath).Length;

        if (Item.TotalBytes > 0)
            return Item.TotalBytes;

        return Item.DownloadedBytes;
    }

    private string FormatBytes(long bytes) => FileSizeFormatter.Format(bytes, App.Localization);
}
