using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class OpenSourceDetailModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public OpenSourceDetailModalViewModel(AppModalHostViewModel host, ILocalizationService l10n, OpenSourceProject project)
    {
        _host = host;
        _l10n = l10n;
        Title = project.Name;
        Summary = project.Summary;
        Detail = project.Detail;
    }

    public string Title { get; }

    public string Summary { get; }

    public string Detail { get; }

    public string CloseButtonText => _l10n.Get("About.Close");

    [RelayCommand]
    private void Close() => _host.Complete(null);
}
