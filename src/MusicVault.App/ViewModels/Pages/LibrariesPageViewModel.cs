using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Library;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>多音乐库管理页</summary>
public partial class LibrariesPageViewModel : PageViewModelBase
{
    private readonly AppLibraryFacade _library;
    private readonly AppModalHostViewModel _appModal;
    private readonly ILocalizationService _l10n;

    public override string PageTitle => string.Empty;
    public override string PageSubtitle => string.Empty;
    public override bool ShowStandardPageHeader => false;

    public ObservableCollection<LibraryListItemViewModel> Libraries { get; } = [];

    public bool HasSelectedRemoteLibrary =>
        SelectedLibrary?.SourceType is LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav;

    public bool CanReconnectSelectedNas =>
        SelectedLibrary is { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav }
        && NasLibraryUiHelper.GetNetworkLibraryStatus(_library, SelectedLibrary)?.Reachability
            != NasReachability.Online;

    public string PaneTitle => _l10n.Get("Nav.Libraries");
    public string AddSourceToolTip => _l10n.Get("Library.AddSource");
    public string AddLocalFolderLabel => _l10n.Get("Library.AddLocalFolder");
    public string AddRemoteNasLabel => _l10n.Get("Library.AddRemoteNas");
    public string AddRemoteWebDavLabel => _l10n.Get("Library.AddRemoteWebDav");
    public string RefreshListToolTip => _l10n.Get("Library.RefreshList");
    public string MoreActionsToolTip => _l10n.Get("Library.MoreActions");
    public string ScanSelectedLabel => _l10n.Get("Library.ScanSelected");
    public string ScanAllLabel => _l10n.Get("Library.ScanAll");
    public string DeleteLibraryMenuLabel => _l10n.Get("Library.DeleteLibraryMenu");
    public string SettingsTitle => _l10n.Get("Libraries.Settings.Title");
    public string SettingsIntro => _l10n.Get("Libraries.Settings.Intro");
    public string FieldNameLabel => _l10n.Get("Libraries.Field.Name");
    public string FieldRootPathLabel => _l10n.Get("Libraries.Field.RootPath");
    public string FieldFileNameLabel => _l10n.Get("Libraries.Field.FileName");
    public string FieldFolderLabel => _l10n.Get("Libraries.Field.Folder");
    public string NameWatermark => _l10n.Get("Libraries.Field.NameWatermark");
    public string ParticipateInScanLabel => _l10n.Get("Libraries.ParticipateInScan");
    public string SaveLabel => _l10n.Get("Libraries.Save");
    public string ScanThisLibraryLabel => _l10n.Get("Libraries.ScanThisLibrary");
    public string RemoveLibraryLabel => _l10n.Get("Libraries.RemoveLibrary");
    public string DeleteConfirmText => _l10n.Get("Common.Delete");
    public string EmptyNoSourcesTitle => _l10n.Get("Libraries.Empty.NoSources");
    public string EmptyNoSourcesDescription => _l10n.Get("Libraries.Empty.NoSourcesDesc");
    public string EmptySelectSourceTitle => _l10n.Get("Libraries.Empty.SelectSource");
    public string EmptySelectSourceDescription => _l10n.Get("Libraries.Empty.SelectSourceDesc");

    public string ReconnectNasLabel => _l10n.Get("Nas.Reconnect");
    public string ReconnectNasToolTip => _l10n.Get("Nas.Reconnect.ToolTip");
    public string DownloadNasLibraryLabel => _l10n.Get("Nas.DownloadLibraryToLocal");
    public string DownloadNasLibraryToolTip => _l10n.Get("Nas.DownloadLibraryToLocal.ToolTip");

    public bool CanDownloadNasLibraryToLocal =>
        SelectedLibrary is { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav }
        && _hasLocalDownloadTarget;

    private bool _hasLocalDownloadTarget;
    public string? SelectedNasStatusText
    {
        get
        {
            if (SelectedLibrary is not { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav })
                return null;
            var status = NasLibraryUiHelper.GetNetworkLibraryStatus(_library, SelectedLibrary);
            if (status == null)
                return null;
            var segment = NasLibraryUiHelper.GetStatusSegment(status.Reachability, _l10n);
            return string.IsNullOrWhiteSpace(status.Message) ? segment : $"{segment} · {status.Message}";
        }
    }

    public bool ShowSelectedNasStatus => !string.IsNullOrWhiteSpace(SelectedNasStatusText);

    public bool HasSelectedLibrary => SelectedLibrary != null;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool ShowLibrariesEmpty => Libraries.Count == 0;

    [ObservableProperty] private LibraryListItemViewModel? _selectedLibraryItem;
    [ObservableProperty] private MusicLibrary? _selectedLibrary;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private int _scanAcoustIdAttempts;
    private int _scanAcoustIdMatches;

    public string ScanStatusText => IsScanning
        ? FormatScanProgressText(ScanProgress, _scanAcoustIdMatches, _scanAcoustIdAttempts)
        : string.Empty;

    public LibrariesPageViewModel(AppLibraryFacade library, AppModalHostViewModel appModal, ILocalizationService l10n)
    {
        _library = library;
        _appModal = appModal;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedUi();
        _library.NasLibraries.StatusChanged += OnNasLibraryStatusChanged;
        _library.WebDavLibraries.StatusChanged += OnNasLibraryStatusChanged;
        RefreshLocalizedUi();
    }

    public event EventHandler? OpenTransfersPageRequested;

    private void OnNasLibraryStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            NasLibraryUiHelper.ApplyNetworkLibraryStatuses(Libraries, _library, _l10n);
            RefreshLocalizedNasUi();
        });
    }

    private void RefreshLocalizedUi()
    {
        RefreshLocalizedNasUi();
        OnPropertyChanged(nameof(PaneTitle));
        OnPropertyChanged(nameof(AddSourceToolTip));
        OnPropertyChanged(nameof(AddLocalFolderLabel));
        OnPropertyChanged(nameof(AddRemoteNasLabel));
        OnPropertyChanged(nameof(AddRemoteWebDavLabel));
        OnPropertyChanged(nameof(RefreshListToolTip));
        OnPropertyChanged(nameof(MoreActionsToolTip));
        OnPropertyChanged(nameof(ScanSelectedLabel));
        OnPropertyChanged(nameof(ScanAllLabel));
        OnPropertyChanged(nameof(DeleteLibraryMenuLabel));
        OnPropertyChanged(nameof(SettingsTitle));
        OnPropertyChanged(nameof(SettingsIntro));
        OnPropertyChanged(nameof(FieldNameLabel));
        OnPropertyChanged(nameof(FieldRootPathLabel));
        OnPropertyChanged(nameof(FieldFileNameLabel));
        OnPropertyChanged(nameof(FieldFolderLabel));
        OnPropertyChanged(nameof(NameWatermark));
        OnPropertyChanged(nameof(ParticipateInScanLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(ScanThisLibraryLabel));
        OnPropertyChanged(nameof(RemoveLibraryLabel));
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(EmptyNoSourcesTitle));
        OnPropertyChanged(nameof(EmptyNoSourcesDescription));
        OnPropertyChanged(nameof(EmptySelectSourceTitle));
        OnPropertyChanged(nameof(EmptySelectSourceDescription));
        OnPropertyChanged(nameof(ScanStatusText));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmTitle));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmDescription));
        ApplyLibraryDisabledLabels();
        RefreshLibraryListItems();
    }

    private void RefreshLibraryListItems()
    {
        foreach (var lib in Libraries)
            lib.RefreshLocalizedStrings(_l10n);
    }

    private void RefreshLocalizedNasUi()
    {
        OnPropertyChanged(nameof(ReconnectNasLabel));
        OnPropertyChanged(nameof(ReconnectNasToolTip));
        OnPropertyChanged(nameof(DownloadNasLibraryLabel));
        OnPropertyChanged(nameof(DownloadNasLibraryToolTip));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        OnPropertyChanged(nameof(CanDownloadNasLibraryToLocal));
        OnPropertyChanged(nameof(SelectedNasStatusText));
        OnPropertyChanged(nameof(ShowSelectedNasStatus));
    }

    private void ApplyLibraryDisabledLabels()
    {
        var label = _l10n.Get("Library.Disabled");
        foreach (var lib in Libraries)
            lib.SetDisabledLabel(label);
    }

    public string DeleteLibraryPopconfirmTitle =>
        SelectedLibrary == null
            ? _l10n.Get("Library.DeleteLibrary.Title")
            : string.Format(_l10n.Get("Library.DeleteLibrary.Confirm"), SelectedLibrary.Name);

    public string DeleteLibraryPopconfirmDescription =>
        SelectedLibrary == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(SelectedLibrary.RootPath)
                ? _l10n.Get("Library.DeleteLibrary.Desc")
                : string.Format(_l10n.Get("Library.DeleteLibrary.DescWithPath"), SelectedLibrary.RootPath);

    partial void OnSelectedLibraryItemChanged(LibraryListItemViewModel? value)
    {
        SelectedLibrary = value?.Library;
        OnPropertyChanged(nameof(HasSelectedLibrary));
        OnPropertyChanged(nameof(HasSelectedRemoteLibrary));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        OnPropertyChanged(nameof(CanDownloadNasLibraryToLocal));
        OnPropertyChanged(nameof(SelectedNasStatusText));
        OnPropertyChanged(nameof(ShowSelectedNasStatus));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmTitle));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmDescription));
    }

    partial void OnSelectedLibraryChanged(MusicLibrary? value)
    {
        OnPropertyChanged(nameof(HasSelectedLibrary));
        OnPropertyChanged(nameof(HasSelectedRemoteLibrary));
        OnPropertyChanged(nameof(CanReconnectSelectedNas));
        OnPropertyChanged(nameof(CanDownloadNasLibraryToLocal));
        OnPropertyChanged(nameof(SelectedNasStatusText));
        OnPropertyChanged(nameof(ShowSelectedNasStatus));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmTitle));
        OnPropertyChanged(nameof(DeleteLibraryPopconfirmDescription));
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(ScanStatusText));

    partial void OnScanProgressChanged(double value) => OnPropertyChanged(nameof(ScanStatusText));

    public async Task InitializeAsync() => await ReloadLibrariesAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await ReloadLibrariesAsync();

    private async Task ReloadLibrariesAsync()
    {
        var selectedId = SelectedLibraryItem?.LibraryId;
        var allLibs = await _library.LibraryManager.GetAllAsync();

        foreach (var stale in Libraries.Where(l => allLibs.All(x => x.Id != l.LibraryId)).ToList())
            Libraries.Remove(stale);

        foreach (var lib in allLibs)
        {
            var count = await _library.Tracks.CountByLibraryScopeAsync(lib.Id, lib.RootPath);
            var size = await NasLibraryUiHelper.ResolveLibrarySizeBytesAsync(_library, lib);
            var item = Libraries.FirstOrDefault(l => l.LibraryId == lib.Id);
            if (item != null)
                item.UpdateStats(count, size);
            else
                Libraries.Add(new LibraryListItemViewModel(lib, lib.Name, lib.RootPath, count, size));
        }

        NasLibraryUiHelper.ApplyNetworkLibraryStatuses(Libraries, _library, _l10n);
        _hasLocalDownloadTarget = (await _library.NasDownloads.GetLocalTargetLibrariesAsync()).Count > 0;
        RefreshLocalizedNasUi();
        ApplyLibraryDisabledLabels();
        RefreshLibraryListItems();

        SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == selectedId)
                              ?? Libraries.FirstOrDefault();
        OnPropertyChanged(nameof(ShowLibrariesEmpty));
    }

    [RelayCommand]
    private async Task PickFolderAndAddAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        try
        {
            var folders = await desktop.MainWindow!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = _l10n.Get("Library.Tracks.PickFolderTitle"),
                AllowMultiple = false
            });
            var folder = folders.FirstOrDefault();
            if (folder == null) return;

            var path = LibraryRepository.NormalizeRootPath(folder.Path.LocalPath);
            var (lib, created) = await _library.LibraryManager.AddOrGetLibraryAsync(Path.GetFileName(path), path);
            await ReloadLibrariesAsync();
            SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == lib.Id)
                                  ?? Libraries.FirstOrDefault();
            StatusMessage = string.Empty;
            if (created)
                MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Library.Tracks.LibraryAdded"), path));
            else
                MessageService.Instance.ShowInfo(_l10n.Get("Library.Tracks.LibraryAlreadyExists"));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Library.Tracks.LibraryAddFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ConnectNasLibraryAsync()
    {
        var choice = await _appModal.ShowConnectNasLibraryAsync(_library.NasLibraries, _l10n).ConfigureAwait(true);
        if (choice != ConnectNasLibraryDialogChoice.Connected)
            return;

        await ReloadLibrariesAsync();
        SelectedLibraryItem = Libraries.LastOrDefault(l => l.IsNetworkSource)
                              ?? Libraries.LastOrDefault();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.NasAdded"));
    }

    [RelayCommand]
    private async Task ConnectWebDavLibraryAsync()
    {
        var choice = await _appModal.ShowConnectWebDavLibraryAsync(_library.WebDavLibraries, _l10n)
            .ConfigureAwait(true);
        if (choice != ConnectNasLibraryDialogChoice.Connected)
            return;

        await ReloadLibrariesAsync();
        SelectedLibraryItem = Libraries.LastOrDefault(l => l.IsNetworkSource)
                              ?? Libraries.LastOrDefault();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.WebDavAdded"));
    }

    [RelayCommand]
    private async Task ReconnectNasLibraryAsync()
    {
        if (SelectedLibrary is not { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav })
            return;

        var libName = SelectedLibrary.Name;
        NasConnectionTestResult result = SelectedLibrary.SourceType switch
        {
            LibrarySourceType.NetworkWebDav =>
                await _library.WebDavLibraries.ReconnectLibraryAsync(SelectedLibrary.Id).ConfigureAwait(true),
            _ => await _library.NasLibraries.ReconnectLibraryAsync(SelectedLibrary.Id).ConfigureAwait(true)
        };
        await ReloadLibrariesAsync();

        if (result.Success)
        {
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Nas.Reconnect.Success"), libName));
        }
        else
        {
            var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
            StatusMessage = message;
            MessageService.Instance.ShowError(string.Format(_l10n.Get("Nas.Reconnect.Failed"), message));
        }
    }

    [RelayCommand]
    private async Task DownloadNasLibraryToLocalAsync()
    {
        if (SelectedLibrary is not { SourceType: LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav })
            return;

        try
        {
            var targetId = App.Settings.Current.NasDownloadTargetLibraryId;
            var result = await _library.NasDownloads
                .EnqueueLibraryDownloadAsync(SelectedLibrary, targetId)
                .ConfigureAwait(true);

            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(NasLibraryUiHelper.FormatBatchDownloadMessage(result, _l10n));
            if (result.Enqueued > 0)
                OpenTransfersPageRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var message = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            StatusMessage = message;
            MessageService.Instance.ShowError(string.Format(_l10n.Get("Nas.Download.Failed"), message));
        }
    }

    [RelayCommand]
    private async Task SaveSelectedAsync()
    {
        if (SelectedLibrary == null) return;
        await _library.LibraryManager.UpdateLibraryAsync(SelectedLibrary);
        await ReloadLibrariesAsync();
        SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == SelectedLibrary.Id)
                              ?? SelectedLibraryItem;
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Libraries.Saved"));
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedLibrary == null) return;
        var id = SelectedLibrary.Id;
        await _library.LibraryManager.DeleteLibraryAsync(id);
        await ReloadLibrariesAsync();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(_l10n.Get("Libraries.Removed"));
    }

    [RelayCommand]
    private async Task ScanSelectedAsync()
    {
        if (SelectedLibrary == null) return;
        var libName = SelectedLibrary.Name;
        IsScanning = true;
        ResetScanProgressCounters();
        _library.Scanner.ProgressChanged += OnScanProgress;
        try
        {
            var result = await _library.LibraryManager.ScanLibraryAsync(SelectedLibrary.Id, incremental: false);
            await ReloadLibrariesAsync();
            StatusMessage = string.Empty;
            if (result.RootMissing)
                MessageService.Instance.ShowError(NasLibraryUiHelper.FormatScanSummary(result, _l10n));
            else
                MessageService.Instance.ShowSuccess($"{NasLibraryUiHelper.FormatScanSummary(result, _l10n)}（{libName}）");
        }
        finally
        {
            _library.Scanner.ProgressChanged -= OnScanProgress;
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        IsScanning = true;
        ResetScanProgressCounters();
        _library.Scanner.ProgressChanged += OnScanProgress;
        try
        {
            await _library.Scanner.ScanAllEnabledAsync(incremental: false);
            await ReloadLibrariesAsync();
            StatusMessage = string.Empty;
            MessageService.Instance.ShowSuccess(_l10n.Get("Library.Tracks.ScanAllDone"));
        }
        finally
        {
            _library.Scanner.ProgressChanged -= OnScanProgress;
            IsScanning = false;
        }
    }

    private void OnScanProgress(object? sender, ScanProgressEventArgs e)
    {
        ScanProgress = e.Percent;
        _scanAcoustIdAttempts = e.AcoustIdLookupAttempts;
        _scanAcoustIdMatches = e.AcoustIdLookupMatches;
        OnPropertyChanged(nameof(ScanStatusText));
    }

    private void ResetScanProgressCounters()
    {
        ScanProgress = 0;
        _scanAcoustIdAttempts = 0;
        _scanAcoustIdMatches = 0;
        OnPropertyChanged(nameof(ScanStatusText));
    }

    private string FormatScanProgressText(double percent, int matches, int attempts) =>
        attempts > 0
            ? string.Format(_l10n.Get("Library.ScanProgressWithAcoustId"), percent, matches, attempts)
            : string.Format(_l10n.Get("Library.ScanProgress"), percent);
}
