using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Views.Dialogs;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class ConfirmRemoveDownloadModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;

    public ConfirmRemoveDownloadModalViewModel(
        AppModalHostViewModel host,
        string displayName,
        bool canDeleteFile)
    {
        _host = host;
        DisplayName = displayName;
        CanDeleteFile = canDeleteFile;
        TransferRowLabels.LabelsChanged += OnLabelsChanged;
    }

    private void OnLabelsChanged(object? sender, EventArgs e) => NotifyLocalizedProperties();

    public string DisplayName { get; }

    public bool CanDeleteFile { get; }

    public bool ShowListAndFileButton => CanDeleteFile;

    public bool ListOnlyIsPrimary => !CanDeleteFile;

    public string ModalTitle => TransferRowLabels.RemoveRecordModalTitle;

    public string CancelButtonText => TransferRowLabels.ModalCancel;

    public string ListAndFileButtonText => TransferRowLabels.RemoveRecordListAndFile;

    public string ListOnlyButtonText => CanDeleteFile
        ? TransferRowLabels.ClearSelectedConfirmListOnly
        : TransferRowLabels.ClearSelectedConfirmDefault;

    public string SummaryText => TransferRowLabels.ClearSelectedTitleOne(DisplayName);

    public string DetailText => CanDeleteFile
        ? TransferRowLabels.ClearSelectedDescriptionWithFiles
        : TransferRowLabels.ClearSelectedDescriptionListOnly;

    private void NotifyLocalizedProperties()
    {
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(CancelButtonText));
        OnPropertyChanged(nameof(ListAndFileButtonText));
        OnPropertyChanged(nameof(ListOnlyButtonText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(DetailText));
    }

    [RelayCommand]
    private void Cancel() => _host.Complete(RemoveDownloadChoice.Cancel);

    [RelayCommand]
    private void ListOnly() => _host.Complete(RemoveDownloadChoice.ListOnly);

    [RelayCommand]
    private void ListAndFile() => _host.Complete(RemoveDownloadChoice.ListAndFile);
}
