using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class BatchScrapeResultModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public BatchScrapeResultModalViewModel(
        AppModalHostViewModel host,
        BatchScrapeRunResult result,
        ILocalizationService l10n)
    {
        _host = host;
        _l10n = l10n;
        Result = result;

        TitleText = result.WasCancelled switch
        {
            true => l10n.Get("Scrape.Result.Title.Cancelled"),
            _ when result.IsFullSuccess => l10n.Get("Scrape.Result.Title.FullSuccess"),
            _ when result.IsAllFailed => l10n.Get("Scrape.Result.Title.AllFailed"),
            _ => l10n.Get("Scrape.Result.Title.Partial")
        };

        OutcomeText = result.WasCancelled switch
        {
            true when result.FailedCount > 0 => string.Format(
                l10n.Get("Scrape.Result.Outcome.CancelledWithFailed"),
                result.SuccessCount,
                result.FailedCount,
                result.IgnoredCount),
            true => string.Format(
                l10n.Get("Scrape.Result.Outcome.Cancelled"),
                result.SuccessCount,
                result.IgnoredCount),
            _ when result.IsFullSuccess => string.Format(
                l10n.Get("Scrape.Result.Outcome.FullSuccess"),
                result.TotalCount),
            _ when result.IsAllFailed => string.Format(
                l10n.Get("Scrape.Result.Outcome.AllFailed"),
                result.TotalCount),
            _ => string.Format(
                l10n.Get("Scrape.Result.Outcome.Partial"),
                result.SuccessCount,
                result.FailedCount)
        };

        ActionHint = result.WasCancelled switch
        {
            true when result.FailedCount > 0 || result.IgnoredCount > 0 =>
                l10n.Get("Scrape.Result.Hint.RetrySelected"),
            true => string.Empty,
            _ when result.HasFailures => l10n.Get("Scrape.Result.Hint.RetryOrSource"),
            _ => l10n.Get("Scrape.Result.Hint.Updated")
        };

        FailureSectionTitle = result.FailureEntries.Count switch
        {
            0 => string.Empty,
            1 => l10n.Get("Scrape.Result.FailuresOne"),
            var n => string.Format(l10n.Get("Scrape.Result.FailuresMany"), n)
        };

        ProcessedSummaryText = result.WasCancelled
            ? string.Format(
                l10n.Get("Scrape.Result.ProcessedSummaryCancelled"),
                result.SuccessCount + result.FailedCount,
                result.TotalCount)
            : string.Format(l10n.Get("Scrape.Result.ProcessedSummary"), result.TotalCount);
    }

    public BatchScrapeRunResult Result { get; }

    public string TitleText { get; }

    public string OutcomeText { get; }

    public string ActionHint { get; }

    public string FailureSectionTitle { get; }

    public string ProcessedSummaryText { get; }

    public string SuccessLabel => _l10n.Get("Scrape.Result.Label.Success");

    public string FailedLabel => _l10n.Get("Scrape.Result.Label.Failed");

    public string IgnoredLabel => _l10n.Get("Scrape.Result.Label.Ignored");

    public string DismissLabel => _l10n.Get("Scrape.Result.Dismiss");

    public bool ShowActionHint => !string.IsNullOrWhiteSpace(ActionHint);

    public bool ShowFailureSection => Result.FailureEntries.Count > 0;

    public ResultStatus ResultStatus => Result.WasCancelled switch
    {
        true => ResultStatus.Warning,
        _ when Result.IsFullSuccess => ResultStatus.Success,
        _ when Result.IsAllFailed => ResultStatus.Error,
        _ => ResultStatus.Warning,
    };

    [RelayCommand]
    private void Dismiss() => _host.Complete(BatchScrapeDialogChoice.Dismiss);
}
