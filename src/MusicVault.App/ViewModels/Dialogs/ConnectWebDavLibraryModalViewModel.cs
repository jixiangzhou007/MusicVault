using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Library;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class ConnectWebDavLibraryModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _modalHost;
    private readonly WebDavLibraryService _webDavLibraries;
    private readonly ILocalizationService _l10n;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 443;
    [ObservableProperty] private string _subPath = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _useTls = true;
    [ObservableProperty] private bool _rememberCredentials = true;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string Title => _l10n.Get("WebDav.Modal.Title");
    public string Intro => _l10n.Get("WebDav.Modal.Intro");
    public string DisplayNameWatermark => _l10n.Get("Nas.Modal.DisplayName");
    public string HostWatermark => _l10n.Get("WebDav.Modal.Host");
    public string PortWatermark => _l10n.Get("WebDav.Modal.Port");
    public string SubPathWatermark => _l10n.Get("WebDav.Modal.SubPath");
    public string UsernameWatermark => _l10n.Get("Nas.Modal.Username");
    public string PasswordWatermark => _l10n.Get("Nas.Modal.Password");
    public string UseTlsLabel => _l10n.Get("WebDav.Modal.UseTls");
    public string RememberCredentialsLabel => _l10n.Get("Nas.Modal.RememberCredentials");
    public string RememberCredentialsCaption => _l10n.Get("Nas.Modal.RememberCredentials.Caption");
    public string CancelLabel => _l10n.Get("Common.Cancel");
    public string TestLabel => _l10n.Get("Nas.Modal.Test");
    public string ConnectLabel => _l10n.Get("Nas.Modal.Connect");

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    public ConnectWebDavLibraryModalViewModel(
        AppModalHostViewModel host,
        WebDavLibraryService webDavLibraries,
        ILocalizationService l10n)
    {
        _modalHost = host;
        _webDavLibraries = webDavLibraries;
        _l10n = l10n;
    }

    [RelayCommand]
    private void Cancel() => _modalHost.Complete(ConnectNasLibraryDialogChoice.Cancel);

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        StatusMessage = string.Empty;
        var result = await _webDavLibraries.TestConnectionAsync(BuildEndpoint()).ConfigureAwait(true);
        StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy) return;

        var endpoint = BuildEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint.BuildWebDavRootPath()))
        {
            StatusMessage = _l10n.Get("WebDav.Modal.Error.MissingHost");
            return;
        }

        IsBusy = true;
        try
        {
            var test = await _webDavLibraries.TestConnectionAsync(endpoint).ConfigureAwait(true);
            StatusMessage = test.Message;
            if (!test.Success)
                return;

            var name = string.IsNullOrWhiteSpace(DisplayName)
                ? $"{Host.Trim()} · WebDAV"
                : DisplayName.Trim();

            await _webDavLibraries.AddOrGetWebDavLibraryAsync(name, endpoint).ConfigureAwait(true);
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
        Protocol = NetworkLibraryProtocol.WebDav,
        UseTls = UseTls,
        Host = Host.Trim(),
        Port = Port > 0 ? Port : UseTls ? 443 : 80,
        SubPath = SubPath.Trim(),
        Username = Username.Trim(),
        Password = Password,
        RememberCredentials = RememberCredentials
    };
}
