using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 下载目录、并发、默认音质与 AcoustId。</summary>
public partial class SettingsPageViewModel
{
    public bool CanOpenDownloadDirectory =>
        !string.IsNullOrWhiteSpace(DownloadDirectory) && Directory.Exists(DownloadDirectory.Trim());

    public string DownloadDirectoryToolTip =>
        string.IsNullOrWhiteSpace(DownloadDirectory) ? string.Empty : DownloadDirectory.Trim();

    private void RefreshDownloadDiskSpaceAlert()
    {
        ShowDownloadDiskSpaceAlert = false;
        DownloadDiskSpaceAlertText = string.Empty;

        var path = DownloadDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
                return;

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.AvailableFreeSpace >= DownloadDiskSpaceWarningBytes)
                return;

            ShowDownloadDiskSpaceAlert = true;
            DownloadDiskSpaceAlertText = string.Format(
                _l10n.Get("Settings.Download.DiskSpaceLow"),
                FormatDiskBytes(drive.AvailableFreeSpace),
                FormatDiskBytes(DownloadDiskSpaceWarningBytes));
        }
        catch
        {
            /* 路径无效或不可访问时不展示磁盘告警 */
        }
    }


    private static string FormatDiskBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
    }


    partial void OnMaxDownloadsChanged(int value)
    {
        OnPropertyChanged(nameof(DownloadConcurrencySummary));
        MarkPendingChanges();
    }


    partial void OnDownloadDirectoryChanged(string value)
    {
        MarkPendingChanges();
        OnPropertyChanged(nameof(CanOpenDownloadDirectory));
        OnPropertyChanged(nameof(DownloadDirectoryToolTip));
        RefreshDownloadDiskSpaceAlert();
    }

    partial void OnDefaultDownloadQualityIndexChanged(int value)
    {
        MarkPendingChanges();
        OnPropertyChanged(nameof(DefaultDownloadQualitySummary));
    }

    partial void OnEnableAcoustIdFingerprintLookupChanged(bool value)
    {
        MarkPendingChanges();
        NotifyAcoustIdPresentationChanged();
    }


    partial void OnAcoustIdApiKeyChanged(string value)
    {
        MarkPendingChanges();
        NotifyAcoustIdPresentationChanged();
    }

    private void NotifyAcoustIdPresentationChanged()
    {
        OnPropertyChanged(nameof(ShowAcoustIdDetails));
        OnPropertyChanged(nameof(ShowAcoustIdSetupWarning));
        OnPropertyChanged(nameof(AcoustIdSetupWarningText));
    }


    [RelayCommand]
    private async Task PickDownloadFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var folders = await desktop.MainWindow!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _l10n.Get("Settings.Dialog.PickDownloadFolder"),
            AllowMultiple = false
        });
        var folder = folders.FirstOrDefault();
        if (folder == null) return;

        DownloadDirectory = folder.Path.LocalPath;
        OnPropertyChanged(nameof(CanOpenDownloadDirectory));
        OnPropertyChanged(nameof(DownloadDirectoryToolTip));
        SetLocalizedStatus("Settings.Status.DownloadDirSelected");
    }


    [RelayCommand]
    private void OpenDownloadDirectory()
    {
        var path = DownloadDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            SetLocalizedStatus("Settings.Status.DownloadDirMissing");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }


    [RelayCommand]
    private async Task CopyDownloadDirectoryAsync()
    {
        var path = DownloadDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetLocalizedStatus("Settings.Status.DownloadDirNotSet");
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var clipboard = desktop.MainWindow?.Clipboard;
        if (clipboard is null)
        {
            SetLocalizedStatus("Settings.Status.ClipboardUnavailable");
            return;
        }

        await clipboard.SetTextAsync(path).ConfigureAwait(true);
        SetLocalizedStatus("Settings.Status.DownloadPathCopied");
    }


    [RelayCommand]
    private async Task TestAcoustIdLookupAsync()
    {
        SyncAcoustIdConfigurationFromUi();
        ShowAcoustIdTestResult = false;

        if (!EnableAcoustIdFingerprintLookup)
        {
            SetAcoustIdTestResult(false, _l10n.Get("Settings.Data.AcoustId.Test.Disabled"));
            return;
        }

        var hasKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MUSICVAULT_ACOUSTID_API_KEY"))
                     || !string.IsNullOrWhiteSpace(AcoustIdApiKey);
        if (!hasKey)
        {
            SetAcoustIdTestResult(false, _l10n.Get("Settings.Data.AcoustId.Test.NoKey"));
            return;
        }

        if (!AcoustIdFingerprintLookup.IsFpcalcAvailable)
        {
            SetAcoustIdTestResult(false, _l10n.Get("Settings.Data.AcoustId.Test.NoFpcalc"));
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _l10n.Get("Settings.Data.AcoustId.TestPickFileTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(_l10n.Get("Settings.Data.AcoustId.TestPickFileType"))
                {
                    Patterns = ["*.mp3", "*.wav", "*.flac", "*.m4a", "*.aac", "*.ogg", "*.wma", "*.ape"]
                }
            ]
        }).ConfigureAwait(true);

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        var path = file.Path.LocalPath;

        try
        {
            var acoustId = await AcoustIdFingerprintLookup.LookupDetailedAsync(path).ConfigureAwait(true);
            if (acoustId.Status == AcoustIdLookupStatus.Success && !string.IsNullOrWhiteSpace(acoustId.AcoustId))
            {
                SetAcoustIdTestResult(true, string.Format(_l10n.Get("Settings.Data.AcoustId.Test.Success"), acoustId.AcoustId));
                return;
            }

            var failKey = acoustId.Status switch
            {
                AcoustIdLookupStatus.FpcalcFailed => "Settings.Data.AcoustId.Test.FpcalcFailed",
                AcoustIdLookupStatus.HttpError => "Settings.Data.AcoustId.Test.HttpError",
                AcoustIdLookupStatus.ApiError => "Settings.Data.AcoustId.Test.ApiError",
                AcoustIdLookupStatus.NoKey => "Settings.Data.AcoustId.Test.NoKey",
                AcoustIdLookupStatus.NoFpcalc => "Settings.Data.AcoustId.Test.NoFpcalc",
                AcoustIdLookupStatus.Disabled => "Settings.Data.AcoustId.Test.Disabled",
                _ => "Settings.Data.AcoustId.Test.NoMatch"
            };
            SetAcoustIdTestResult(false, _l10n.Get(failKey));
        }
        catch (Exception ex)
        {
            SetAcoustIdTestResult(false, ex.Message);
        }
    }


    private void SetAcoustIdTestResult(bool ok, string message)
    {
        var resolved = ServiceMessageLocalizer.Resolve(_l10n, message);
        AcoustIdTestSucceeded = ok;
        AcoustIdTestResultText = resolved;
        ShowAcoustIdTestResult = true;
        StatusMessage = ok
            ? string.Format(_l10n.Get("Settings.Status.AcoustIdTestOk"), resolved)
            : string.Format(_l10n.Get("Settings.Status.AcoustIdTestFailed"), resolved);
        if (ok)
            ShellStatusService.Instance.Info(StatusMessage, ShellCategoryAcoustId);
        else
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryAcoustId);
    }


    private void SyncAcoustIdConfigurationFromUi()
    {
        AcoustIdLookupConfiguration.Apply(EnableAcoustIdFingerprintLookup, AcoustIdApiKey.Trim());
    }


    partial void OnDiscoverAutoOpenBatchEnqueueWizardChanged(bool value) => MarkPendingChanges();

}
