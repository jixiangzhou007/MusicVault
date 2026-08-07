using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Controls;
using MusicVault.App.Localization;
using MusicVault.App.Views.Dialogs;
using MusicVault.Services.Download;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class ConfirmBatchEnqueueModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;

    public ConfirmBatchEnqueueModalViewModel(
        AppModalHostViewModel host,
        BatchEnqueuePreview preview,
        ILocalizationService l10n,
        string? sourceLabel = null)
    {
        _host = host;
        var qualityLabel = BatchEnqueuePreviewLocalizer.QualityLabel(l10n, preview);
        var estimatedSizeLabel = BatchEnqueuePreviewLocalizer.EstimatedSizeLabel(l10n, preview);

        TitleText = string.IsNullOrWhiteSpace(sourceLabel)
            ? l10n.Get("Discover.BatchEnqueue.Modal.Title")
            : string.Format(l10n.Get("Discover.BatchEnqueue.Modal.Title.Source"), sourceLabel.Trim());

        SummaryText = preview.EligibleCount > 0
            ? string.Format(
                l10n.Get("Discover.BatchEnqueue.Modal.Summary"),
                preview.TotalCount,
                preview.EligibleCount,
                qualityLabel)
            : string.Format(
                l10n.Get("Discover.BatchEnqueue.Modal.Summary.NoEligible"),
                preview.TotalCount,
                qualityLabel);

        StatsLine = string.Format(
            l10n.Get("Discover.BatchEnqueue.Modal.StatsLine"),
            estimatedSizeLabel,
            qualityLabel,
            preview.EligibleCount);

        DetailText = preview.SkippedByQuality > 0
            ? string.Format(l10n.Get("Discover.BatchEnqueue.Modal.Detail.Skipped"), preview.SkippedByQuality)
            : l10n.Get("Discover.BatchEnqueue.Modal.Detail");

        CanConfirm = preview.EligibleCount > 0;
        ConfirmButtonText = l10n.Get("Discover.BatchEnqueue.Modal.Confirm");
        CancelButtonText = l10n.Get("Discover.BatchEnqueue.Modal.Cancel");

        WizardSteps =
        [
            new StepItem(l10n.Get("Discover.BatchEnqueue.Modal.Step.Select"), StepStatus.Finish, 1),
            new StepItem(l10n.Get("Discover.BatchEnqueue.Modal.Step.Confirm"), StepStatus.Process, 2, showConnector: false),
        ];
    }

    public IReadOnlyList<StepItem> WizardSteps { get; }

    public string TitleText { get; }

    public string SummaryText { get; }

    public string StatsLine { get; }

    public string DetailText { get; }

    public bool CanConfirm { get; }

    public string ConfirmButtonText { get; }

    public string CancelButtonText { get; }

    [RelayCommand]
    private void Cancel() => _host.Complete(BatchEnqueueDialogChoice.Cancel);

    [RelayCommand]
    private void Confirm() => _host.Complete(BatchEnqueueDialogChoice.Confirm);
}
