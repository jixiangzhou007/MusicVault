using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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

    // --- domain members (moved from main) ---

    public string BackupSectionTitle => _l10n.Get("Settings.Backup.SectionTitle");
    public string BackupOperationsTitle => _l10n.Get("Settings.Backup.Operations");
    public string BackupDescription => _l10n.Get("Settings.Backup.Description");
    public string BackupNowLabel => _l10n.Get("Settings.Backup.Now");
    public string BackupRefreshListLabel => _l10n.Get("Settings.Backup.RefreshList");
    public string BackupOpenFolderLabel => _l10n.Get("Settings.Backup.OpenFolder");
    public string BackupExportDiagnosticsLabel => _l10n.Get("Settings.Backup.ExportDiagnostics");
    public string BackupHistoryTitle => _l10n.Get("Settings.Backup.History");
    public string BackupEmptyLabel => _l10n.Get("Settings.Backup.Empty");
    public string BackupEmptyHint => _l10n.Get("Settings.Backup.EmptyHint");

    public string BackupColumnNameLabel => _l10n.Get("Settings.Backup.Column.Name");
    public string BackupColumnDateLabel => _l10n.Get("Settings.Backup.Column.Date");
    public string BackupColumnSizeLabel => _l10n.Get("Settings.Backup.Column.Size");
    public string BackupColumnActionsLabel => _l10n.Get("Settings.Backup.Column.Actions");

    public string BackupColumnMetaLabel => _l10n.Get("Settings.Backup.Column.Meta");


    public string BackupRestoreLabel => _l10n.Get("Settings.Backup.Restore");
    public string BackupRestoreConfirmTitle => _l10n.Get("Settings.Backup.RestoreConfirmTitle");
    public string BackupRestoreConfirmDescription => _l10n.Get("Settings.Backup.RestoreConfirmDescription");
    public string BackupPreviousPageLabel => _l10n.Get("Settings.Backup.PreviousPage");
    public string BackupNextPageLabel => _l10n.Get("Settings.Backup.NextPage");

    public const int BackupPageSize = 5;

    private readonly List<SettingsBackupEntryViewModel> _backupEntriesAll = [];

    public ObservableCollection<SettingsBackupEntryViewModel> BackupEntriesPage { get; } = [];

    public bool HasBackupEntries => _backupEntriesAll.Count > 0;

    public string BackupListSummary => _backupEntriesAll.Count switch
    {
        0 => _l10n.Get("Settings.Backup.SummaryNone"),
        1 => _l10n.Get("Settings.Backup.SummaryOne"),
        _ => string.Format(_l10n.Get("Settings.Backup.SummaryMany"), _backupEntriesAll.Count)
    };

    public int BackupPageCount =>
        _backupEntriesAll.Count == 0
            ? 0
            : (int)Math.Ceiling(_backupEntriesAll.Count / (double)BackupPageSize);

    public bool ShowBackupPagination => BackupPageCount > 1;

    public bool CanBackupPreviousPage => BackupPageIndex > 0;

    public bool CanBackupNextPage => BackupPageIndex < BackupPageCount - 1;

    public string BackupPageInfoText =>
        BackupPageCount <= 1
            ? string.Empty
            : string.Format(_l10n.Get("Settings.Backup.PageInfo"), BackupPageIndex + 1, BackupPageCount);

    [ObservableProperty] private int _backupPageIndex;

    public bool CanOpenBackupFolder =>
        _backupEntriesAll.Count > 0
        && Directory.Exists(Path.GetDirectoryName(_backupEntriesAll[0].FilePath) ?? string.Empty);
}
