using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class BatchScrapeProgressModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;
    private readonly Action _requestCancel;

    public BatchScrapeProgressModalViewModel(
        AppModalHostViewModel host,
        Action requestCancel,
        ILocalizationService l10n)
    {
        _host = host;
        _l10n = l10n;
        _requestCancel = requestCancel;
        _currentFileText = l10n.Get("Scrape.Progress.Preparing");
    }

    public string Title => _l10n.Get("Scrape.Progress.Title");

    public string CancelLabel => _l10n.Get("Common.Cancel");

    [ObservableProperty] private string _currentFileText;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMaximum = 1;
    [ObservableProperty] private int _successCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private int _ignoredCount;
    [ObservableProperty] private int _totalCount;

    public string StatsText => string.Format(
        _l10n.Get("Scrape.Progress.Stats"),
        SuccessCount,
        FailedCount,
        IgnoredCount,
        TotalCount);

    public string ProgressPercentText
    {
        get
        {
            if (ProgressMaximum <= 0)
                return "0%";
            var pct = (int)Math.Round(ProgressValue / ProgressMaximum * 100);
            return $"{Math.Clamp(pct, 0, 100)}%";
        }
    }

    public void Begin(int total)
    {
        TotalCount = total;
        ProgressMaximum = Math.Max(1, total);
        ProgressValue = 0;
        SuccessCount = 0;
        FailedCount = 0;
        IgnoredCount = 0;
        CurrentFileText = _l10n.Get("Scrape.Progress.Preparing");
        NotifyStatsChanged();
    }

    public void ReportCurrent(string displayName, int processed, int success, int failed, int ignored)
    {
        CurrentFileText = string.IsNullOrWhiteSpace(displayName)
            ? _l10n.Get("Scrape.Progress.Searching")
            : string.Format(_l10n.Get("Scrape.Progress.SearchingTrack"), displayName);
        ProgressValue = processed;
        SuccessCount = success;
        FailedCount = failed;
        IgnoredCount = ignored;
        NotifyStatsChanged();
    }

    public void Finish(BatchScrapeRunResult result) => _host.Complete(result);

    partial void OnSuccessCountChanged(int value) => OnPropertyChanged(nameof(StatsText));
    partial void OnFailedCountChanged(int value) => OnPropertyChanged(nameof(StatsText));
    partial void OnIgnoredCountChanged(int value) => OnPropertyChanged(nameof(StatsText));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(StatsText));

    partial void OnProgressValueChanged(double value) => OnPropertyChanged(nameof(ProgressPercentText));

    partial void OnProgressMaximumChanged(double value) => OnPropertyChanged(nameof(ProgressPercentText));

    private void NotifyStatsChanged() => OnPropertyChanged(nameof(StatsText));

    [RelayCommand]
    private void Cancel() => _requestCancel();
}
