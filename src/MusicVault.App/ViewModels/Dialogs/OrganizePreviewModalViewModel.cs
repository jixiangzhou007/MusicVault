using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class OrganizePreviewModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public OrganizePreviewModalViewModel(
        AppModalHostViewModel host,
        IReadOnlyList<OrganizePreviewSection> sections,
        int totalCount,
        string scopeLabel,
        ILocalizationService l10n)
    {
        _host = host;
        _l10n = l10n;
        SummaryText = BuildSummaryText(sections, totalCount, scopeLabel, l10n);
        foreach (var section in sections)
            Sections.Add(section);
    }

    public string ModalTitle => _l10n.Get("Organize.Preview.ModalTitle");

    public string CloseLabel => _l10n.Get("About.Close");

    public string SummaryText { get; }

    public ObservableCollection<OrganizePreviewSection> Sections { get; } = [];

    [RelayCommand]
    private void Close() => _host.Complete(null);

    private static string BuildSummaryText(
        IReadOnlyList<OrganizePreviewSection> sections,
        int totalCount,
        string scopeLabel,
        ILocalizationService l10n)
    {
        if (sections.Count == 0)
            return string.Format(l10n.Get("Organize.Preview.SummaryEmpty"), totalCount, scopeLabel);

        var separator = l10n.Culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "、" : ", ";
        var dimensions = string.Join(separator, sections.Select(s => s.DimensionTitle));
        if (sections.Count == 1)
        {
            return string.Format(
                l10n.Get("Organize.Preview.SummarySingle"),
                totalCount,
                dimensions,
                scopeLabel);
        }

        return string.Format(
            l10n.Get("Organize.Preview.SummaryMultiple"),
            totalCount,
            dimensions,
            scopeLabel);
    }
}
