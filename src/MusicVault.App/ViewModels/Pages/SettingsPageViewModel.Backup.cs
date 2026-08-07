using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 — 数据库备份、恢复与备份列表分页。</summary>
public partial class SettingsPageViewModel
{

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var path = await _services.Backup.CreateFullBackupAsync();
        SetLocalizedStatus("Settings.Status.BackupCreated", Path.GetFileName(path));
        RefreshBackupList();
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(SettingsBackupEntryViewModel? entry)
    {
        if (entry is null) return;
        var path = entry.FilePath;
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            await _services.Backup.RestoreFullBackupAsync(path);
        else
            await _services.Backup.RestoreDatabaseAsync(path);
        SetLocalizedStatus("Settings.Status.RestoreComplete");
    }

    /// <summary>首次进入设置页时再加载备份列表，避免拖慢启动。</summary>
    public void EnsureBackupListLoaded()
    {
        if (_backupEntriesAll.Count > 0)
            return;
        RefreshBackupList();
    }

    [RelayCommand]
    private void RefreshBackupList()
    {
        _backupEntriesAll.Clear();
        foreach (var b in _services.Backup.ListBackups())
        {
            _backupEntriesAll.Add(new SettingsBackupEntryViewModel(
                b.FileName,
                b.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                FormatBackupSize(b.SizeBytes),
                b.FilePath,
                _l10n));
        }

        BackupPageIndex = 0;
        RebuildBackupPage();
    }

    partial void OnBackupPageIndexChanged(int value) => RebuildBackupPage();

    [RelayCommand(CanExecute = nameof(CanBackupPreviousPage))]
    private void BackupPreviousPage()
    {
        if (!CanBackupPreviousPage)
            return;
        BackupPageIndex--;
    }


    [RelayCommand(CanExecute = nameof(CanBackupNextPage))]
    private void BackupNextPage()
    {
        if (!CanBackupNextPage)
            return;
        BackupPageIndex++;
    }

    private void RebuildBackupPage()
    {
        BackupEntriesPage.Clear();
        if (_backupEntriesAll.Count == 0)
        {
            NotifyBackupPaginationChanged();
            return;
        }

        var lastPage = BackupPageCount - 1;
        if (BackupPageIndex > lastPage)
            BackupPageIndex = lastPage;
        if (BackupPageIndex < 0)
            BackupPageIndex = 0;

        var skip = BackupPageIndex * BackupPageSize;
        foreach (var entry in _backupEntriesAll.Skip(skip).Take(BackupPageSize))
            BackupEntriesPage.Add(entry);

        NotifyBackupPaginationChanged();
    }

    private void NotifyBackupPaginationChanged()
    {
        OnPropertyChanged(nameof(HasBackupEntries));
        OnPropertyChanged(nameof(BackupListSummary));
        OnPropertyChanged(nameof(CanOpenBackupFolder));
        OnPropertyChanged(nameof(BackupPageCount));
        OnPropertyChanged(nameof(ShowBackupPagination));
        OnPropertyChanged(nameof(CanBackupPreviousPage));
        OnPropertyChanged(nameof(CanBackupNextPage));
        OnPropertyChanged(nameof(BackupPageInfoText));
        BackupPreviousPageCommand.NotifyCanExecuteChanged();
        BackupNextPageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        var path = _backupEntriesAll.FirstOrDefault()?.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetLocalizedStatus("Settings.Status.NoBackupFiles");
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            SetLocalizedStatus("Settings.Status.BackupDirMissing");
            return;
        }

        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private static string FormatBackupSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:F1} GB";
    }
}
