using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Core.Models;
using MusicVault.Services.Library;

namespace MusicVault.App.ViewModels.Dialogs;

public enum ConnectNasLibraryDialogChoice
{
    Cancel,
    Connected
}

public partial class ConnectNasLibraryModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _modalHost;
    private readonly NasLibraryService _nasLibraries;
    private readonly ILocalizationService _l10n;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _nasHost = string.Empty;
    [ObservableProperty] private string _shareName = string.Empty;
    [ObservableProperty] private string _subPath = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _rememberCredentials = true;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string Title => _l10n.Get("Nas.Modal.Title");
    public string Intro => _l10n.Get("Nas.Modal.Intro");
    public string DisplayNameWatermark => _l10n.Get("Nas.Modal.DisplayName");
    public string HostWatermark => _l10n.Get("Nas.Modal.Host");
    public string PortWatermark => _l10n.Get("Nas.Modal.Port");
    public string PortToolTip => _l10n.Get("Nas.Modal.PortToolTip");
    public string ShareWatermark => _l10n.Get("Nas.Modal.Share");
    public string SubPathWatermark => _l10n.Get("Nas.Modal.SubPath");
    public string UsernameWatermark => _l10n.Get("Nas.Modal.Username");
    public string PasswordWatermark => _l10n.Get("Nas.Modal.Password");
    public string RememberCredentialsLabel => _l10n.Get("Nas.Modal.RememberCredentials");
    public string RememberCredentialsCaption => _l10n.Get("Nas.Modal.RememberCredentials.Caption");
    public string CancelLabel => _l10n.Get("Common.Cancel");
    public string TestLabel => _l10n.Get("Nas.Modal.Test");
    public string ConnectLabel => _l10n.Get("Nas.Modal.Connect");

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    public ConnectNasLibraryModalViewModel(
        AppModalHostViewModel host,
        NasLibraryService nasLibraries,
        ILocalizationService l10n)
    {
        _modalHost = host;
        _nasLibraries = nasLibraries;
        _l10n = l10n;
    }

    [RelayCommand]
    private void Cancel() => _modalHost.Complete(ConnectNasLibraryDialogChoice.Cancel);

    [RelayCommand]
    private void TestConnection()
    {
        StatusMessage = string.Empty;
        var result = _nasLibraries.TestConnection(BuildEndpoint());
        StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy) return;

        var endpoint = BuildEndpoint();
        var unc = endpoint.BuildUncRootPath();
        if (string.IsNullOrWhiteSpace(unc))
        {
            StatusMessage = _l10n.Get("Nas.Modal.Error.MissingHostShare");
            return;
        }

        IsBusy = true;
        try
        {
            var test = _nasLibraries.TestConnection(endpoint);
            StatusMessage = test.Message;
            if (!test.Success)
                return;

            var name = string.IsNullOrWhiteSpace(DisplayName)
                ? $"{NasHost.Trim()} · {ShareName.Trim()}"
                : DisplayName.Trim();

            await _nasLibraries.AddOrGetNasLibraryAsync(name, endpoint).ConfigureAwait(true);
            _modalHost.Complete(ConnectNasLibraryDialogChoice.Connected);
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Nas.Modal.Error.AddFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private NetworkLibraryEndpoint BuildEndpoint() => new()
    {
        Host = NasHost.Trim(),
        ShareName = ShareName.Trim(),
        SubPath = SubPath.Trim(),
        Username = Username.Trim(),
        Password = Password,
        RememberCredentials = RememberCredentials
    };
}
