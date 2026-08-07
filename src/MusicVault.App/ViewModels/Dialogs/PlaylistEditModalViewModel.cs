using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public sealed record PlaylistEditResult(string Name, string? Description);

public partial class PlaylistEditModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public PlaylistEditModalViewModel(
        AppModalHostViewModel host,
        ILocalizationService l10n,
        string title,
        string name,
        string? description,
        string nameWatermark,
        string descriptionWatermark)
    {
        _host = host;
        _l10n = l10n;
        Title = title;
        Name = name;
        Description = description ?? string.Empty;
        NameWatermark = nameWatermark;
        DescriptionWatermark = descriptionWatermark;
    }

    public string Title { get; }
    public string NameWatermark { get; }
    public string DescriptionWatermark { get; }

    public string NameLabel => _l10n.Get("Playlists.Edit.NameLabel");

    public string DescriptionLabel => _l10n.Get("Playlists.Edit.DescriptionLabel");

    public string CancelButtonText => _l10n.Get("Common.Cancel");

    public string SaveButtonText => _l10n.Get("Common.Save");

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _description;

    [RelayCommand]
    private void Cancel() => _host.Complete(null);

    [RelayCommand]
    private void Save()
    {
        var trimmed = string.IsNullOrWhiteSpace(Name) ? NameWatermark : Name.Trim();
        var desc = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        _host.Complete(new PlaylistEditResult(trimmed, desc));
    }
}
