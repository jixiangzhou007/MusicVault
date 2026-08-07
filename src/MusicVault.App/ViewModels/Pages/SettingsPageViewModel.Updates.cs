using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;
using MusicVault.Services.Audio;
using Velopack;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 — 应用更新、诊断导出、更新历史与启动时检查。</summary>
public partial class SettingsPageViewModel
{

    private void LoadUpdateHistory()
    {
        UpdateHistoryEntries.Clear();
        foreach (var entry in ReleaseNotesReader.Load())
            UpdateHistoryEntries.Add(new ReleaseNoteDayViewModel(entry.Date, entry.Version, entry.Items));
        NotifyUpdateHistoryPresentationChanged();
    }

    private void NotifyUpdateHistoryPresentationChanged()
    {
        OnPropertyChanged(nameof(HasUpdateHistoryEntries));
    }


    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _l10n.Get("Settings.Dialog.ExportDiagnostics"),
            SuggestedFileName = $"MusicVault-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            FileTypeChoices =
            [
                new FilePickerFileType(_l10n.Get("Settings.Dialog.TextFileFilter")) { Patterns = ["*.txt"] }
            ]
        });
        if (file == null) return;

        try
        {
            await PlaybackDiagnostics.WriteExportAsync(file.Path.LocalPath).ConfigureAwait(true);
            SetLocalizedStatus("Settings.Status.DiagnosticsExported", file.Path.LocalPath);
            ShellStatusService.Instance.Info(StatusMessage, ShellCategoryDiagnostics);
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("Settings.Status.ExportFailed", ex.Message);
            ShellStatusService.Instance.Error(StatusMessage, ShellCategoryDiagnostics, ex);
        }
    }


    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingAppUpdate = true;
        ShowAppUpdateStatus = false;
        SetLocalizedStatus("Settings.Status.CheckingAppUpdate");
        _settings.Current.UpdateFeedUrl = UpdateFeedUrl;
        try
        {
            var result = await _updates.CheckAndDownloadAsync();
            StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
            HasPendingUpdate = result.HasUpdate;
            _pendingUpdate = result.Info;
            _settings.Current.LastUpdateCheckAt = DateTime.UtcNow;
            await _settings.SaveAsync();
            ApplyAppUpdateStatus(result);
        }
        finally
        {
            IsCheckingAppUpdate = false;
        }
    }

    private void ApplyAppUpdateStatus(UpdateCheckResult result)
    {
        ShowAppUpdateStatus = true;
        AppUpdateStatusIsNew = result.HasUpdate;
        AppUpdateStatusIsError = !result.HasUpdate && IsUpdateStatusError(result.Message);
        AppUpdateStatusIsOk = !AppUpdateStatusIsNew && !AppUpdateStatusIsError;
        AppUpdateStatusText = result.HasUpdate
            ? string.Format(_l10n.Get("Settings.Update.NewVersionApp"), result.Version?.TrimStart('v', 'V'))
            : AppUpdateStatusIsError
                ? ShortenUpdateMessage(ServiceMessageLocalizer.Resolve(_l10n, result.Message))
                : _l10n.Get("Settings.Update.UpToDate");
    }


    private bool IsUpdateStatusError(string message)
    {
        var resolved = ServiceMessageLocalizer.Resolve(_l10n, message);
        return resolved.Contains("失败", StringComparison.Ordinal)
        || resolved.Contains("未配置", StringComparison.Ordinal)
        || resolved.Contains("未包含", StringComparison.Ordinal)
        || resolved.Contains("fail", StringComparison.OrdinalIgnoreCase)
        || resolved.Contains("not configured", StringComparison.OrdinalIgnoreCase)
        || resolved.Contains("missing", StringComparison.OrdinalIgnoreCase)
        || (ServiceMessageCodec.TryGetKey(message) is { } key
            && (key.Contains("Failed", StringComparison.Ordinal)
                || key.Contains("NotConfigured", StringComparison.Ordinal)
                || key.Contains("Missing", StringComparison.Ordinal)));
    }

    private static string ShortenUpdateMessage(string message)
    {
        if (message.Length <= 28) return message;
        return message[..25] + "…";
    }

    [RelayCommand]
    private void ApplyUpdateAndRestart()
    {
        if (_pendingUpdate != null)
            _updates.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    /// <summary>根据设置判断启动时是否应检查更新。</summary>
    public static bool ShouldCheckOnStartup(AppSettingsService settings)
    {
        var feed = AppUpdateDefaults.ResolveFeedUrl(
            settings.Current.UpdateFeedUrl,
            Environment.GetEnvironmentVariable("MUSICVAULT_UPDATE_URL"));
        if (string.IsNullOrWhiteSpace(feed))
            return false;

        if (settings.Current.UpdateCheckFrequency == "off") return false;
        if (settings.Current.LastUpdateCheckAt == null) return true;

        var elapsed = DateTime.UtcNow - settings.Current.LastUpdateCheckAt.Value;
        return settings.Current.UpdateCheckFrequency switch
        {
            "daily" => elapsed.TotalDays >= 1,
            "weekly" => elapsed.TotalDays >= 7,
            "monthly" => elapsed.TotalDays >= 30,
            _ => false
        };
    }

}
