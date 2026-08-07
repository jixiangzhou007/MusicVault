using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;

namespace MusicVault.App.Views.Dialogs;

public partial class MusicApiUpdateDialog : Window
{
    private BundledMusicApiUpdateCheckResult? _check;
    private Progress<MusicApiDownloadProgress>? _progressReporter;

    public MusicApiUpdateDialog() => InitializeComponent();

    public MusicApiUpdateDialog(BundledMusicApiUpdateCheckResult check) : this()
    {
        _check = check;
        var l10n = App.Localization;
        Title = l10n.Get("Settings.ApiUpdate.Modal.WindowTitle");
        TitleText.Text = l10n.Get("Settings.ApiUpdate.Modal.Title.New");
        SkipButton.Content = l10n.Get("Settings.ApiUpdate.Modal.SkipLater");
        UpdateButton.Content = l10n.Get("Settings.ApiUpdate.Modal.UpdateNow");
        DetailText.Text = string.Format(
            l10n.Get("Settings.ApiUpdate.Modal.Detail"),
            check.LocalVersion,
            check.LatestVersion);

        _progressReporter = new Progress<MusicApiDownloadProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownloadProgress.IsVisible = true;
                StageText.IsVisible = true;
                DownloadProgress.Value = Math.Clamp(p.Percent, 0, 100);
                StageText.Text = ServiceMessageLocalizer.Resolve(App.Localization, p.Stage);
            });
        });

        SkipButton.Click += (_, _) => CloseWith(MusicApiUpdateDialogChoice.SkipThisVersion);
        UpdateButton.Click += OnUpdateClick;
    }

    private async void OnUpdateClick(object? sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        DownloadProgress.IsVisible = true;
        StageText.IsVisible = true;
        DownloadProgress.Value = 0;
        StageText.Text = ServiceMessageLocalizer.Resolve(
            App.Localization,
            ServiceMessageCodec.Key("ApiUpdate.Progress.Preparing"));

        var ok = await RunDownloadAndRestartAsync().ConfigureAwait(true);
        CloseWith(ok ? MusicApiUpdateDialogChoice.Update : MusicApiUpdateDialogChoice.Cancel);
    }

    public async Task<bool> RunDownloadAndRestartAsync(CancellationToken ct = default)
    {
        if (_check == null || _progressReporter == null)
            return false;

        var svc = new BundledMusicApiUpdateService();
        var result = await svc.DownloadAndApplyAsync(_check, _progressReporter, ct).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StageText.Text = ServiceMessageLocalizer.Resolve(App.Localization, result.Message));
        return result.Succeeded;
    }

    private void CloseWith(MusicApiUpdateDialogChoice choice) => Close(choice);
}
