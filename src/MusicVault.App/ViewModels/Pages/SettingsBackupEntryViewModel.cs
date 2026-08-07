using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Pages;

public sealed partial class SettingsBackupEntryViewModel : ObservableObject
{
    private readonly ILocalizationService _l10n;

    public SettingsBackupEntryViewModel(
        string fileName,
        string createdAtText,
        string sizeText,
        string filePath,
        ILocalizationService l10n)
    {
        FileName = fileName;
        CreatedAtText = createdAtText;
        SizeText = sizeText;
        FilePath = filePath;
        _l10n = l10n;
    }

    public string FileName { get; }

    public string CreatedAtText { get; }

    public string SizeText { get; }

    public string MetaText => $"{CreatedAtText} · {SizeText}";

    public string FilePath { get; }

    public bool IsFullBackup =>
        FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public string KindLabel => IsFullBackup
        ? _l10n.Get("Settings.Backup.KindFull")
        : _l10n.Get("Settings.Backup.KindDatabase");

    /// <summary>列表主列：备份文件名。</summary>
    public string DisplayName => FileName;

    public void RefreshLocalizedStrings() => OnPropertyChanged(nameof(KindLabel));
}
