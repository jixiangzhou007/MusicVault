using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Controls;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class MusicApiUpdateModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly BundledMusicApiUpdateCheckResult _check;
    private readonly ILocalizationService _l10n;

    public MusicApiUpdateModalViewModel(AppModalHostViewModel host, BundledMusicApiUpdateCheckResult check)
    {
        _host = host;
        _check = check;
        _l10n = App.Localization;
        DetailText = string.Format(
            _l10n.Get("Settings.ApiUpdate.Modal.Detail"),
            check.LocalVersion,
            check.LatestVersion);
    }

    public string DetailText { get; private set; }

    [ObservableProperty] private bool _showProgress;

    [ObservableProperty] private bool _isCompleted;

    [ObservableProperty] private bool _updateSucceeded;

    [ObservableProperty] private double _downloadProgress;

    [ObservableProperty] private string _stageText = string.Empty;

    [ObservableProperty] private bool _canSkip = true;

    [ObservableProperty] private bool _canUpdate = true;

    public bool ShowPromptActions => !ShowProgress && !IsCompleted;

    public bool ShowDismissOnly => IsCompleted;

    public ResultStatus DisplayStatus => IsCompleted
        ? UpdateSucceeded ? ResultStatus.Success : ResultStatus.Error
        : ResultStatus.Info;

    public string TitleText => IsCompleted
        ? UpdateSucceeded
            ? _l10n.Get("Settings.ApiUpdate.Modal.Title.Success")
            : _l10n.Get("Settings.ApiUpdate.Modal.Title.Failed")
        : _l10n.Get("Settings.ApiUpdate.Modal.Title.New");

    public string SkipButtonText => IsCompleted
        ? _l10n.Get("Settings.ApiUpdate.Modal.Close")
        : _l10n.Get("Settings.ApiUpdate.Modal.SkipLater");

    public string UpdateButtonText => IsCompleted
        ? string.Empty
        : _l10n.Get("Settings.ApiUpdate.Modal.UpdateNow");

    [RelayCommand]
    private void Skip()
    {
        if (IsCompleted)
        {
            _host.Complete(UpdateSucceeded
                ? MusicApiUpdateDialogChoice.Update
                : MusicApiUpdateDialogChoice.Cancel);
            return;
        }

        _host.Complete(MusicApiUpdateDialogChoice.SkipThisVersion);
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (IsCompleted)
        {
            _host.Complete(UpdateSucceeded
                ? MusicApiUpdateDialogChoice.Update
                : MusicApiUpdateDialogChoice.Cancel);
            return;
        }

        CanUpdate = false;
        CanSkip = false;
        ShowProgress = true;
        DownloadProgress = 0;
        StageText = ServiceMessageLocalizer.Resolve(_l10n, ServiceMessageCodec.Key("ApiUpdate.Progress.Preparing"));

        var progress = new Progress<MusicApiDownloadProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ShowProgress = true;
                DownloadProgress = Math.Clamp(p.Percent, 0, 100);
                StageText = ServiceMessageLocalizer.Resolve(_l10n, p.Stage);
            });
        });

        var svc = new BundledMusicApiUpdateService();
        var result = await svc.DownloadAndApplyAsync(_check, progress).ConfigureAwait(true);

        ShowProgress = false;
        IsCompleted = true;
        UpdateSucceeded = result.Succeeded;
        DetailText = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        CanSkip = true;
        CanUpdate = false;
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(DisplayStatus));
        OnPropertyChanged(nameof(ShowPromptActions));
        OnPropertyChanged(nameof(ShowDismissOnly));
        OnPropertyChanged(nameof(SkipButtonText));
        OnPropertyChanged(nameof(UpdateButtonText));
    }
}
