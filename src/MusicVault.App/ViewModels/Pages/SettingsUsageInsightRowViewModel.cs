namespace MusicVault.App.ViewModels.Pages;

public sealed class SettingsUsageInsightRowViewModel(string label, string value, bool isMonospace = false)
{
    public string Label { get; } = label;

    public string Value { get; } = value;

    public bool IsMonospace { get; } = isMonospace;
}
