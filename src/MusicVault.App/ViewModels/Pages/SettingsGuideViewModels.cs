using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Pages;

public sealed partial class ReleaseNoteDayViewModel : ObservableObject
{
    public ReleaseNoteDayViewModel(string date, string version, IReadOnlyList<string> items)
    {
        Date = date;
        Version = version;
        Items = items;
        RefreshLocalizedStrings();
    }

    public string Date { get; }
    public string Version { get; }
    public IReadOnlyList<string> Items { get; }

    [ObservableProperty] private string _header = string.Empty;

    public void RefreshLocalizedStrings()
    {
        Header = string.IsNullOrWhiteSpace(Date)
            ? $"v{Version}"
            : $"{FormatDate(Date)} · v{Version}";
    }

    private static string FormatDate(string isoDate)
    {
        if (!DateTime.TryParse(isoDate, out var dt))
            return isoDate;

        var culture = App.Localization.Culture;
        return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? dt.ToString("yyyy年M月d日")
            : dt.ToString("yyyy-MM-dd");
    }
}

public sealed partial class SoftwareFeatureCardViewModel : ObservableObject
{
    public SoftwareFeatureCardViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }
    public string Description { get; }

    [ObservableProperty] private bool _isSelected;
}
