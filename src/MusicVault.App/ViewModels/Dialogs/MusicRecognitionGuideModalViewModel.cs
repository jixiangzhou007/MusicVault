using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class MusicRecognitionGuideModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public MusicRecognitionGuideModalViewModel(AppModalHostViewModel host, ILocalizationService l10n, string body)
    {
        _host = host;
        _l10n = l10n;
        Body = body;
    }

    public string Title => _l10n.Get("Settings.Recognition.Guide.Title");

    public string CloseLabel => _l10n.Get("About.Close");

    public string Body { get; }

    [RelayCommand]
    private void Close() => _host.Complete(null);
}
