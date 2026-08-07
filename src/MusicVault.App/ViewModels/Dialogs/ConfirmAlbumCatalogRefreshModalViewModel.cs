using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Views.Dialogs;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class ConfirmAlbumCatalogRefreshModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;

    public ConfirmAlbumCatalogRefreshModalViewModel(
        AppModalHostViewModel host,
        string albumName,
        bool willOverwriteCache,
        ILocalizationService l10n)
    {
        _host = host;
        TitleText = l10n.Get("AlbumCatalog.Refresh.Title");
        SummaryText = string.Format(
            l10n.Get(willOverwriteCache
                ? "AlbumCatalog.Refresh.SummaryOverwrite"
                : "AlbumCatalog.Refresh.SummaryFirst"),
            albumName);
        DetailText = l10n.Get("AlbumCatalog.Refresh.Detail");
        ConfirmButtonText = l10n.Get("AlbumCatalog.Refresh.Confirm");
        CancelButtonText = l10n.Get("Common.Cancel");
    }

    public string TitleText { get; }

    public string SummaryText { get; }

    public string DetailText { get; }

    public string CancelButtonText { get; }

    public bool CanConfirm => true;

    public string ConfirmButtonText { get; }

    [RelayCommand]
    private void Cancel() => _host.Complete(AlbumCatalogRefreshChoice.Cancel);

    [RelayCommand]
    private void Confirm() => _host.Complete(AlbumCatalogRefreshChoice.Confirm);
}
