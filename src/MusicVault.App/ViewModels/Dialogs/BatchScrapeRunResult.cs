namespace MusicVault.App.ViewModels.Dialogs;

public sealed class BatchScrapeRunResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int IgnoredCount { get; init; }
    public int TotalCount { get; init; }
    public bool WasCancelled { get; init; }
    public IReadOnlyList<BatchScrapeFailureEntry> FailureEntries { get; init; } = [];

    public bool HasFailures => FailedCount > 0 || FailureEntries.Count > 0;

    public bool IsFullSuccess => !WasCancelled && FailedCount == 0 && IgnoredCount == 0;

    public bool IsAllFailed => !WasCancelled && SuccessCount == 0 && FailedCount > 0;
}
