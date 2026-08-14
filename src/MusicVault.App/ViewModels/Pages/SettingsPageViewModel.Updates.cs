using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;
using MusicVault.Services.Audio;
using Velopack;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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

    // --- domain members (moved from main) ---

    public string AdvancedTabOnlineSourcesLabel => _l10n.Get("Settings.Advanced.Tab.OnlineSources");
    public string AdvancedTabUpdateDiagnosticsLabel => _l10n.Get("Settings.Advanced.Tab.UpdateDiagnostics");
    public string UpdateDiagnosticsTabAppUpdateLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.AppUpdate");
    public string UpdateDiagnosticsTabBundledApiLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.BundledApi");
    public string UpdateDiagnosticsTabChangelogLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.Changelog");
    public string UpdateDiagnosticsTabTroubleshootLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.Troubleshoot");

    public bool IsUpdateDiagnosticsAppTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.AppUpdate;

    public bool IsUpdateDiagnosticsBundledApiTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.BundledApi;

    public bool IsUpdateDiagnosticsChangelogTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.Changelog;

    public bool IsUpdateDiagnosticsTroubleshootTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.Troubleshoot;

    public string UpdateFeedAdvancedLabel => _l10n.Get("Settings.Update.FeedAdvanced");

    public string UpdateCurrentVersionLabel => _l10n.Get("Settings.Update.CurrentVersion");
    public string UpdateCheckLabel => _l10n.Get("Settings.Update.Check");
    public string UpdateCheckingAppLabel => _l10n.Get("Settings.Update.CheckingApp");
    public string UpdateFeedUrlLabel => _l10n.Get("Settings.Update.FeedUrl");
    public string UpdateFeedUrlPlaceholder => _l10n.Get("Settings.Update.FeedNotConfigured");
    public string UpdateFrequencyFieldLabel => _l10n.Get("Settings.Update.Frequency");
    public string UpdatePendingInstallLabel => _l10n.Get("Settings.Update.PendingInstall");
    public string UpdateRestartInstallLabel => _l10n.Get("Settings.Update.RestartInstall");
    public string UpdateCheckingBundledApiLabel => _l10n.Get("Settings.Update.CheckingBundledApi");
    public string UpdateDownloadInstallLabel => _l10n.Get("Settings.Update.DownloadInstall");
    public string UpdateNoHistoryLabel => _l10n.Get("Settings.Update.NoHistory");
    public string UpdateFrequencyToolTip => _l10n.Get("Settings.Update.FrequencyToolTip");
    public string DiagnosticsExportPackageLabel => _l10n.Get("Settings.Diagnostics.ExportPackage");
    public string DiagnosticsPlatformLoginLabel => _l10n.Get("Settings.Diagnostics.PlatformLogin");
    public string DiagnosticsPlatformLoginButtonLabel => _l10n.Get("Settings.Diagnostics.PlatformLoginButton");
    public string DiagnosticsPlatformLoginHint => _l10n.Get("Settings.Diagnostics.PlatformLoginHint");
    public string DiagnosticsRecentStatusTitle => _l10n.Get("Settings.Diagnostics.RecentStatus");
    public string DiagnosticsUsageStatsTitle => _l10n.Get("Settings.Diagnostics.UsageStats");
    public string DiagnosticsExportSnapshotLabel => _l10n.Get("Settings.Diagnostics.ExportSnapshot");
    public string DiagnosticsResetStatsLabel => _l10n.Get("Settings.Diagnostics.ResetStats");
    public string ResetUsagePopconfirmTitle => _l10n.Get("Settings.Diagnostics.ResetUsageTitle");
    public string ResetUsagePopconfirmDescription => _l10n.Get("Settings.Diagnostics.ResetUsageDescription");
    public string ResetUsagePopconfirmConfirm => _l10n.Get("Settings.Diagnostics.ResetUsageConfirm");

    public IReadOnlyList<string> UpdateFrequencyOptionLabels =>
    [
        _l10n.Get("Settings.Update.Frequency.Daily"),
        _l10n.Get("Settings.Update.Frequency.Weekly"),
        _l10n.Get("Settings.Update.Frequency.Monthly"),
        _l10n.Get("Settings.Update.Frequency.Off")
    ];

    [ObservableProperty] private int _updateFrequencyIndex = 2;

    [ObservableProperty] private string _updateFeedUrl = string.Empty;

    [ObservableProperty] private string _versionText = "v1.0.0";
    [ObservableProperty] private bool _hasPendingUpdate;
    [ObservableProperty] private string _appUpdateStatusText = string.Empty;
    [ObservableProperty] private bool _showAppUpdateStatus;
    [ObservableProperty] private bool _appUpdateStatusIsNew;
    [ObservableProperty] private bool _appUpdateStatusIsError;
    [ObservableProperty] private bool _appUpdateStatusIsOk;
    [ObservableProperty] private bool _isCheckingAppUpdate;
    [ObservableProperty] private string _musicApiUpdateStatusText = string.Empty;
    [ObservableProperty] private bool _showMusicApiUpdateStatus;
    [ObservableProperty] private bool _musicApiUpdateStatusIsNew;
    [ObservableProperty] private bool _musicApiUpdateStatusIsError;
    [ObservableProperty] private bool _musicApiUpdateStatusIsOk;
    [ObservableProperty] private bool _isCheckingMusicApiUpdate;
}
