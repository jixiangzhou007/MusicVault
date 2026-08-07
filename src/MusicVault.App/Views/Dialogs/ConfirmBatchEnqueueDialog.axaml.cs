using Avalonia.Controls;
using MusicVault.App.Localization;
using MusicVault.Services.Download;

namespace MusicVault.App.Views.Dialogs;

public enum BatchEnqueueDialogChoice
{
    Cancel,
    Confirm
}

public partial class ConfirmBatchEnqueueDialog : Window
{
    public ConfirmBatchEnqueueDialog() => InitializeComponent();

    public ConfirmBatchEnqueueDialog(BatchEnqueuePreview preview, string? sourceLabel = null) : this()
    {
        var l10n = App.Localization;
        var qualityLabel = BatchEnqueuePreviewLocalizer.QualityLabel(l10n, preview);
        Title = l10n.Get("Discover.BatchEnqueue.Modal.WindowTitle");
        CancelButton.Content = l10n.Get("Discover.BatchEnqueue.Modal.Cancel");
        ConfirmButton.Content = l10n.Get("Discover.BatchEnqueue.Modal.Confirm");

        TitleText.Text = string.IsNullOrWhiteSpace(sourceLabel)
            ? l10n.Get("Discover.BatchEnqueue.Modal.Title")
            : string.Format(l10n.Get("Discover.BatchEnqueue.Modal.Title.Source"), sourceLabel.Trim());

        SummaryText.Text = preview.EligibleCount > 0
            ? string.Format(
                l10n.Get("Discover.BatchEnqueue.Modal.Summary"),
                preview.TotalCount,
                preview.EligibleCount,
                qualityLabel)
            : string.Format(
                l10n.Get("Discover.BatchEnqueue.Modal.Summary.NoEligible"),
                preview.TotalCount,
                qualityLabel);

        DetailText.Text = preview.SkippedByQuality > 0
            ? string.Format(l10n.Get("Discover.BatchEnqueue.Modal.Detail.Skipped"), preview.SkippedByQuality)
            : l10n.Get("Discover.BatchEnqueue.Modal.Detail");

        ConfirmButton.IsEnabled = preview.EligibleCount > 0;
        CancelButton.Click += (_, _) => Close(BatchEnqueueDialogChoice.Cancel);
        ConfirmButton.Click += (_, _) => Close(BatchEnqueueDialogChoice.Confirm);
    }
}
