using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class PreviewLoadingModalViewModel : ViewModelBase, IOnlinePreviewProgressReporter
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;
    private readonly Action _requestCancel;
    private bool _completed;

    public PreviewLoadingModalViewModel(
        AppModalHostViewModel host,
        OnlineTrack track,
        Action requestCancel,
        ILocalizationService l10n)
    {
        _host = host;
        _l10n = l10n;
        _requestCancel = requestCancel;
        Title = track.Title;
        Artist = track.Artist;
        StatusText = _l10n.Get("Preview.Loading.Preparing");
    }

    public string ModalTitle => _l10n.Get("Preview.Loading.ModalTitle");

    public string CancelLabel => _l10n.Get("Common.Cancel");

    public string Title { get; }

    public string Artist { get; }

    [ObservableProperty] private string _statusText;

    [ObservableProperty] private double _progressValue;

    [ObservableProperty] private bool _isProgressIndeterminate = true;

    public string ProgressPercentText => $"{(int)Math.Round(ProgressValue)}%";

    public bool ShowProgressPercent => !IsProgressIndeterminate;

    partial void OnProgressValueChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercentText));
        OnPropertyChanged(nameof(ShowProgressPercent));
    }

    partial void OnIsProgressIndeterminateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowProgressPercent));
        OnPropertyChanged(nameof(ProgressPercentText));
    }

    public void ReportStage(string statusText)
    {
        if (_completed || string.IsNullOrWhiteSpace(statusText))
            return;

        StatusText = statusText;
        if (!IsProgressIndeterminate && ProgressValue <= 0)
            IsProgressIndeterminate = true;
    }

    public void ReportDownloadProgress(double? fraction)
    {
        if (_completed)
            return;

        if (fraction is null or < 0)
        {
            IsProgressIndeterminate = true;
            return;
        }

        IsProgressIndeterminate = false;
        ProgressValue = Math.Clamp(fraction.Value, 0, 1) * 100;
        if (fraction.Value >= 1)
            StatusText = _l10n.Get("Player.Online.Stage.StartingPlayer");
    }

    public void Complete()
    {
        if (_completed)
            return;

        _completed = true;
        _host.Complete(null);
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_completed)
            return;

        StatusText = _l10n.Get("Preview.Loading.Cancelling");
        _requestCancel();
    }
}
