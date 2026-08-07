namespace MusicVault.App.ViewModels.Dialogs;

public sealed record BatchScrapeConfirmField(string Label, bool Enabled)
{
    public string StatusGlyph => Enabled ? "✓" : "—";
}

public sealed class BatchScrapeConfirmContext
{
    public required int SelectedCount { get; init; }
    public required string PlatformLabel { get; init; }
    public required IReadOnlyList<BatchScrapeConfirmField> Fields { get; init; }

    /// <summary>整清单大批量时的额外提示（可选）。</summary>
    public string? LargeBatchHint { get; init; }

    public required string ScopeLabel { get; init; }
}
