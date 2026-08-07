using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Controls;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>首次运行向导：曲库、下载目录、内置 API。</summary>
public partial class FirstRunWizardModalViewModel : ViewModelBase
{
    private const int LastStepIndex = 3;

    private readonly AppModalHostViewModel _host;
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly ILocalizationService _l10n;
    private readonly LibrariesPageViewModel? _librariesPage;

    [ObservableProperty] private int _stepIndex;

    [ObservableProperty] private string _libraryPath = string.Empty;
    [ObservableProperty] private bool _skipLibraryStep;
    [ObservableProperty] private string _downloadDirectory = string.Empty;
    [ObservableProperty] private bool _autoStartBundledMusicApi = true;
    [ObservableProperty] private bool _autoStartBundledNetEaseApi = true;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public FirstRunWizardModalViewModel(
        AppModalHostViewModel host,
        AppServices services,
        AppSettingsService settings,
        ILocalizationService l10n,
        LibrariesPageViewModel? librariesPage = null)
    {
        _host = host;
        _services = services;
        _settings = settings;
        _l10n = l10n;
        _librariesPage = librariesPage;

        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();

        DownloadDirectory = ResolveDefaultDownloadDirectory(services, settings);
        LibraryPath = ResolveDefaultLibraryPath();

        _autoStartBundledMusicApi = settings.Current.AutoStartBundledMusicApi;
        _autoStartBundledNetEaseApi = settings.Current.AutoStartBundledNetEaseApi;

        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);

        UpdateWizardSteps();
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(WelcomeTitle));
        OnPropertyChanged(nameof(WelcomeDesc));
        OnPropertyChanged(nameof(WelcomeHint));
        OnPropertyChanged(nameof(LibraryTitle));
        OnPropertyChanged(nameof(LibraryDesc));
        OnPropertyChanged(nameof(LibraryWatermark));
        OnPropertyChanged(nameof(LibraryBrowseText));
        OnPropertyChanged(nameof(DownloadTitle));
        OnPropertyChanged(nameof(DownloadDesc));
        OnPropertyChanged(nameof(DownloadWatermark));
        OnPropertyChanged(nameof(DownloadBrowseText));
        OnPropertyChanged(nameof(ApiTitle));
        OnPropertyChanged(nameof(ApiDesc));
        OnPropertyChanged(nameof(ApiAutoStartGoMusicText));
        OnPropertyChanged(nameof(ApiAutoStartNetEaseText));
        OnPropertyChanged(nameof(ApiNoBundledText));
        OnPropertyChanged(nameof(SkipWizardText));
        OnPropertyChanged(nameof(SkipStepText));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(ApiHealthStatusText));
        UpdateWizardSteps();
    }

    public bool HasBundledGoMusicApi => BundledMusicApiHost.Instance.HasBundledExecutable;
    public bool HasBundledNetEaseApi => BundledNetEaseApiHost.Instance.HasBundledApi;
    public bool HasAnyBundledApi => HasBundledGoMusicApi || HasBundledNetEaseApi;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsWelcomeStep => StepIndex == 0;
    public bool IsLibraryStep => StepIndex == 1;
    public bool IsDownloadStep => StepIndex == 2;
    public bool IsApiStep => StepIndex == 3;

    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex >= LastStepIndex;

    public string WelcomeTitle => _l10n.Get("Wizard.FirstRun.Welcome.Title");
    public string WelcomeDesc => _l10n.Get("Wizard.FirstRun.Welcome.Desc");
    public string WelcomeHint => _l10n.Get("Wizard.FirstRun.Welcome.Hint");
    public string LibraryTitle => _l10n.Get("Wizard.FirstRun.Library.Title");
    public string LibraryDesc => _l10n.Get("Wizard.FirstRun.Library.Desc");
    public string LibraryWatermark => _l10n.Get("Wizard.FirstRun.Library.Watermark");
    public string LibraryBrowseText => _l10n.Get("Wizard.FirstRun.Library.Browse");
    public string DownloadTitle => _l10n.Get("Wizard.FirstRun.Download.Title");
    public string DownloadDesc => _l10n.Get("Wizard.FirstRun.Download.Desc");
    public string DownloadWatermark => _l10n.Get("Wizard.FirstRun.Download.Watermark");
    public string DownloadBrowseText => _l10n.Get("Wizard.FirstRun.Download.Browse");
    public string ApiTitle => _l10n.Get("Wizard.FirstRun.Api.Title");
    public string ApiDesc => _l10n.Get("Wizard.FirstRun.Api.Desc");
    public string ApiAutoStartGoMusicText => _l10n.Get("Wizard.FirstRun.Api.AutoStartGoMusic");
    public string ApiAutoStartNetEaseText => _l10n.Get("Wizard.FirstRun.Api.AutoStartNetEase");
    public string ApiNoBundledText => _l10n.Get("Wizard.FirstRun.Api.NoBundled");
    public string SkipWizardText => _l10n.Get("Wizard.FirstRun.SkipWizard");
    public string SkipStepText => _l10n.Get("Wizard.FirstRun.SkipStep");
    public string BackText => _l10n.Get("Wizard.FirstRun.Back");
    public string NextButtonText => IsLastStep ? _l10n.Get("Wizard.FirstRun.Start") : _l10n.Get("Wizard.FirstRun.Next");

    public string ApiHealthStatusText => OnlineApiHealthService.Instance.State switch
    {
        OnlineApiHealthState.Healthy => _l10n.Get("Nav.Discover.ApiHealth.Healthy"),
        OnlineApiHealthState.Checking => _l10n.Get("Nav.Discover.ApiHealth.Checking"),
        OnlineApiHealthState.NotConfigured => _l10n.Get("Nav.Discover.ApiHealth.NotConfigured"),
        OnlineApiHealthState.Unhealthy => string.Format(
            _l10n.Get("Nav.Discover.ApiHealth.Unhealthy"),
            ServiceMessageLocalizer.ResolveDetailAggregate(_l10n, OnlineApiHealthService.Instance.DetailMessage)),
        _ => string.Empty
    };

    public bool ShowApiHealthStatus =>
        IsApiStep && OnlineApiHealthService.Instance.State != OnlineApiHealthState.Unknown;

    public ObservableCollection<StepItem> WizardSteps { get; } = [];

    partial void OnStepIndexChanged(int value)
    {
        StatusMessage = string.Empty;
        UpdateWizardSteps();
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsLibraryStep));
        OnPropertyChanged(nameof(IsDownloadStep));
        OnPropertyChanged(nameof(IsApiStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(ShowApiHealthStatus));
        OnPropertyChanged(nameof(ApiHealthStatusText));

        if (value == LastStepIndex)
            _ = RefreshApiHealthAsync();
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    [RelayCommand]
    private void Back()
    {
        if (StepIndex > 0)
            StepIndex--;
    }

    [RelayCommand]
    private async Task SkipWizardAsync()
    {
        _settings.Current.FirstRunWizardCompleted = true;
        await _settings.SaveAsync().ConfigureAwait(true);
        _host.Complete(FirstRunWizardChoice.Skipped);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (StepIndex < LastStepIndex)
        {
            StepIndex++;
            return;
        }

        await FinishAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void SkipCurrentStep()
    {
        if (StepIndex == 1)
            SkipLibraryStep = true;
        if (StepIndex < LastStepIndex)
            StepIndex++;
        else
            _ = FinishAsync();
    }

    [RelayCommand]
    private async Task PickLibraryFolderAsync()
    {
        var path = await PickFolderAsync(_l10n.Get("Wizard.FirstRun.PickLibraryTitle")).ConfigureAwait(true);
        if (path != null)
        {
            LibraryPath = path;
            SkipLibraryStep = false;
        }
    }

    [RelayCommand]
    private async Task PickDownloadFolderAsync()
    {
        var path = await PickFolderAsync(_l10n.Get("Wizard.FirstRun.PickDownloadTitle")).ConfigureAwait(true);
        if (path != null)
            DownloadDirectory = path;
    }

    private async Task RefreshApiHealthAsync()
    {
        await OnlineApiHealthService.Instance.RefreshAsync().ConfigureAwait(true);
        OnPropertyChanged(nameof(ApiHealthStatusText));
        OnPropertyChanged(nameof(ShowApiHealthStatus));
    }

    private async Task FinishAsync()
    {
        try
        {
            if (!SkipLibraryStep && !string.IsNullOrWhiteSpace(LibraryPath))
            {
                var path = LibraryRepository.NormalizeRootPath(LibraryPath.Trim());
                if (Directory.Exists(path))
                {
                    var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    if (string.IsNullOrWhiteSpace(name))
                        name = _l10n.Get("Wizard.FirstRun.DefaultLibraryName");
                    await _services.LibraryManager.AddOrGetLibraryAsync(name, path).ConfigureAwait(true);
                    if (_librariesPage != null)
                        await _librariesPage.InitializeAsync().ConfigureAwait(true);
                }
            }

            var downloadDir = string.IsNullOrWhiteSpace(DownloadDirectory)
                ? Path.Combine(_services.DataDirectory, "Downloads")
                : DownloadDirectory.Trim();
            Directory.CreateDirectory(downloadDir);

            _settings.Current.DownloadDirectory = downloadDir;
            _settings.Current.AutoStartBundledMusicApi = AutoStartBundledMusicApi;
            _settings.Current.AutoStartBundledNetEaseApi = AutoStartBundledNetEaseApi;
            _settings.Current.FirstRunWizardCompleted = true;

            MusicApiBootstrap.ApplyConfig(_settings.Current, _services.Gatherers);
            _services.Downloads.SetDownloadRoot(downloadDir);
            await _settings.SaveAsync().ConfigureAwait(true);

            _host.Complete(FirstRunWizardChoice.Completed);
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Wizard.FirstRun.SaveFailed", ex.Message);
        }
    }

    private void UpdateWizardSteps()
    {
        WizardSteps.Clear();
        WizardSteps.Add(new StepItem(_l10n.Get("Wizard.FirstRun.Step.Welcome"), StepIndex > 0 ? StepStatus.Finish : StepStatus.Process, 1));
        WizardSteps.Add(new StepItem(_l10n.Get("Wizard.FirstRun.Step.Library"), StepIndex > 1 ? StepStatus.Finish : StepIndex == 1 ? StepStatus.Process : StepStatus.Wait, 2));
        WizardSteps.Add(new StepItem(_l10n.Get("Wizard.FirstRun.Step.Download"), StepIndex > 2 ? StepStatus.Finish : StepIndex == 2 ? StepStatus.Process : StepStatus.Wait, 3));
        WizardSteps.Add(new StepItem(_l10n.Get("Wizard.FirstRun.Step.Online"), StepIndex == 3 ? StepStatus.Process : StepStatus.Wait, 4, showConnector: false));
    }

    private static string ResolveDefaultDownloadDirectory(AppServices services, AppSettingsService settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Current.DownloadDirectory))
            return settings.Current.DownloadDirectory.Trim();

        var desktopMusic = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "MusicDownload");
        if (Directory.Exists(desktopMusic))
            return desktopMusic;

        return Path.Combine(services.DataDirectory, "Downloads");
    }

    private static string ResolveDefaultLibraryPath()
    {
        var desktopMusic = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "MusicDownload");
        if (Directory.Exists(desktopMusic))
            return desktopMusic;

        return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }

    private static async Task<string?> PickFolderAsync(string title)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow == null)
        {
            return null;
        }

        var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            }).ConfigureAwait(true);

        return folders.FirstOrDefault()?.Path.LocalPath;
    }
}
