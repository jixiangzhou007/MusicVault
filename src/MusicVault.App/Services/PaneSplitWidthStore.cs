namespace MusicVault.App.Services;

/// <summary>读写各页面分栏宽度（像素）。</summary>
public static class PaneSplitWidthStore
{
    public const double MinPaneWidth = 160;
    public const double SplitterHitWidth = 6;
    public const double MaxPaneWidthRatio = 0.78;

    public static bool TryGet(string key, out double width)
    {
        width = 0;
        var dict = App.Settings?.Current.PaneSplitWidths;
        if (dict is null || !dict.TryGetValue(key, out var stored) || stored < MinPaneWidth)
            return false;

        width = stored;
        return true;
    }

    public static double GetOrDefault(string key, double defaultWidth) =>
        TryGet(key, out var w) ? w : defaultWidth;

    public static double ClampToGrid(double width, double gridWidth)
    {
        if (gridWidth <= SplitterHitWidth + MinPaneWidth * 2)
            return MinPaneWidth;

        var max = Math.Max(MinPaneWidth, (gridWidth - SplitterHitWidth) * MaxPaneWidthRatio);
        return Math.Clamp(width, MinPaneWidth, max);
    }

    public static async Task SaveAsync(string key, double width, CancellationToken ct = default)
    {
        if (App.Settings is null || string.IsNullOrWhiteSpace(key))
            return;

        App.Settings.Current.PaneSplitWidths ??= new Dictionary<string, double>(StringComparer.Ordinal);
        App.Settings.Current.PaneSplitWidths[key] = Math.Max(MinPaneWidth, width);
        await App.Settings.SaveAsync(ct).ConfigureAwait(false);
    }
}
