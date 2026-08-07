namespace MusicVault.App.ViewModels.Pages;

public sealed class SettingsApiProbeRowViewModel(string label, string url, bool ok, string detail)
{
    public string Label { get; } = label;
    public string Url { get; } = url;
    public bool Ok { get; } = ok;
    public string Detail { get; } = detail;
    public string StatusText => Ok ? Detail : Detail;
}
