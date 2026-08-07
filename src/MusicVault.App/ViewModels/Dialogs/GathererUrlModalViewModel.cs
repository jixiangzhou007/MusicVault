using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class GathererUrlModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public GathererUrlModalViewModel(
        AppModalHostViewModel host,
        ILocalizationService l10n,
        string title,
        string description,
        string watermark,
        string url)
    {
        _host = host;
        _l10n = l10n;
        Title = title;
        Description = description;
        Watermark = watermark;
        _url = url;
        _l10n.CultureChanged += OnCultureChanged;
    }

    public string Title { get; }

    public string Description { get; }

    public string Watermark { get; }

    public string CancelButtonText => _l10n.Get("Common.Cancel");

    public string SaveButtonText => _l10n.Get("Common.Save");

    [ObservableProperty] private string _url;

    [RelayCommand]
    private void Cancel() => _host.Complete(null);

    [RelayCommand]
    private void Save() => _host.Complete(Url.Trim());

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CancelButtonText));
        OnPropertyChanged(nameof(SaveButtonText));
    }
}
