using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class OnlineTrackDownloadPickerModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;
    private readonly Action<OnlineTrackFormatViewModel, string?> _enqueue;
    private readonly Action? _openTransfers;

    public OnlineTrackDownloadPickerModalViewModel(
        AppModalHostViewModel host,
        string title,
        string artist,
        IEnumerable<OnlineTrackFormatViewModel> options,
        Action<OnlineTrackFormatViewModel, string?> enqueue,
        IEnumerable<OnlineTrackDownloadHistoryEntry> recentDownloads,
        ILocalizationService l10n,
        Action? openTransfers = null,
        string? defaultDirectory = null)
    {
        _host = host;
        _l10n = l10n;
        _enqueue = enqueue;
        _openTransfers = openTransfers;
        Title = title;
        Artist = artist;
        DefaultDirectory = defaultDirectory ?? string.Empty;
        foreach (var option in options.Where(x => x.IsAvailable))
            Options.Add(option);
        foreach (var row in recentDownloads)
            RecentDownloads.Add(row);
    }

    public string ModalTitle => _l10n.Get("Discover.DownloadPicker.Title");

    public string FolderLabel => _l10n.Get("Discover.DownloadPicker.FolderLabel");

    public string PickFolderLabel => _l10n.Get("Discover.DownloadPicker.PickFolder");

    public string UseDefaultFolderLabel => _l10n.Get("Discover.DownloadPicker.UseDefaultFolder");

    public string NoQualityText => _l10n.Get("Discover.DownloadPicker.NoQuality");

    public string RecentTitle => _l10n.Get("Discover.DownloadPicker.RecentTitle");

    public string NoRecentText => _l10n.Get("Discover.DownloadPicker.NoRecent");

    public string OpenTransfersLabel => _l10n.Get("Discover.DownloadPicker.OpenTransfers");

    public string CancelLabel => _l10n.Get("Common.Cancel");

    public string Title { get; }
    public string Artist { get; }
    public string DefaultDirectory { get; }
    public ObservableCollection<OnlineTrackFormatViewModel> Options { get; } = [];
    public ObservableCollection<OnlineTrackDownloadHistoryEntry> RecentDownloads { get; } = [];
    public bool HasOptions => Options.Count > 0;
    public bool HasRecentDownloads => RecentDownloads.Count > 0;
    public bool HasCustomDirectory => !string.IsNullOrWhiteSpace(CustomDirectory);
    public string DownloadDirectoryDisplay =>
        HasCustomDirectory
            ? CustomDirectory
            : string.Format(_l10n.Get("Discover.DownloadPicker.DefaultFolder"), DefaultDirectory);

    [ObservableProperty] private string _customDirectory = string.Empty;

    partial void OnCustomDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasCustomDirectory));
        OnPropertyChanged(nameof(DownloadDirectoryDisplay));
    }

    [RelayCommand]
    private void Cancel() => _host.Complete(null);

    [RelayCommand]
    private void Pick(OnlineTrackFormatViewModel? option)
    {
        if (option == null)
            return;

        _enqueue(option, HasCustomDirectory ? CustomDirectory.Trim() : null);
        _host.Complete(option);
    }

    [RelayCommand]
    private void OpenTransfers()
    {
        _openTransfers?.Invoke();
        _host.Complete(null);
    }

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.StorageProvider == null)
            return;

        var start = HasCustomDirectory ? CustomDirectory.Trim() : DefaultDirectory;
        var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _l10n.Get("Discover.DownloadPicker.PickFolderDialogTitle"),
            AllowMultiple = false,
            SuggestedStartLocation = await TryCreateStartFolderAsync(desktop.MainWindow.StorageProvider, start)
        }).ConfigureAwait(true);
        var folder = folders.FirstOrDefault();
        if (folder == null)
            return;

        CustomDirectory = folder.Path.LocalPath;
    }

    [RelayCommand]
    private void UseDefaultFolder() => CustomDirectory = string.Empty;

    private static async Task<IStorageFolder?> TryCreateStartFolderAsync(
        IStorageProvider provider,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        try
        {
            return await provider.TryGetFolderFromPathAsync(path).ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record OnlineTrackDownloadHistoryEntry(
    string Title,
    string Artist,
    string StatusText,
    string? TargetPath);
