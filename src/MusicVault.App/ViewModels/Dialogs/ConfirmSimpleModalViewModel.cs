using CommunityToolkit.Mvvm.Input;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class ConfirmSimpleModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;

    public ConfirmSimpleModalViewModel(
        AppModalHostViewModel host,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel,
        bool confirmIsDanger = false)
    {
        _host = host;
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;
        ConfirmIsDanger = confirmIsDanger;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }
    public bool ConfirmIsDanger { get; }

    [RelayCommand]
    private void Cancel() => _host.Complete(ConfirmSimpleChoice.Cancel);

    [RelayCommand]
    private void Confirm() => _host.Complete(ConfirmSimpleChoice.Confirm);
}
