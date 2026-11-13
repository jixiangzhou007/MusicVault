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

/// <summary>首次运行向导：欢迎 → 选路 → 路径详情。</summary>
public partial class FirstRunWizardModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly ILocalizationService _l10n;
    private readonly LibrariesPageViewModel? _librariesPage;

    [ObservableProperty] private int _stepIndex;

    [ObservableProperty] private FirstRunOnboardingPath _selectedPath = FirstRunOnboardingPath.Unset;

    [ObservableProperty] private string _libraryPath = string.Empty;
    [ObservableProperty] private bool _skipLibraryStep;
    [ObservableProperty] private string _downloadDirectory = string.Empty;
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

        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);

        UpdateWizardSteps();
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(WelcomeTitle));
        OnPropertyChanged(nameof(WelcomeDesc));
        OnPropertyChanged(nameof(WelcomeHint));
        OnPropertyChanged(nameof(PathTitle));
        OnPropertyChanged(nameof(PathDesc));
        OnPropertyChanged(nameof(PathLocalTitle));
        OnPropertyChanged(nameof(PathLocalDesc));
        OnPropertyChanged(nameof(PathDiscoverTitle));
        OnPropertyChanged(nameof(PathDiscoverDesc));
        OnPropertyChanged(nameof(LibraryTitle));
        OnPropertyChanged(nameof(LibraryDesc));
        OnPropertyChanged(nameof(LibraryWatermark));
        OnPropertyChanged(nameof(LibraryBrowseText));
        OnPropertyChanged(nameof(DiscoverBundledText));
        OnPropertyChanged(nameof(DiscoverDirectText));
        OnPropertyChanged(nameof(DiscoverMacText));
        OnPropertyChanged(nameof(SkipWizardText));
        OnPropertyChanged(nameof(SkipStepText));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(NextButtonText));
        UpdateWizardSteps();
    }

    public bool HasBundledGoMusicApi => BundledMusicApiHost.Instance.HasBundledExecutable;
    public bool HasBundledNetEaseApi => BundledNetEaseApiHost.Instance.HasBundledApi;
    public bool HasAnyBundledApi => HasBundledGoMusicApi || HasBundledNetEaseApi;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsWelcomeStep => FirstRunWizardFlow.IsWelcome(StepIndex);
    public bool IsPathStep => FirstRunWizardFlow.IsPathStep(StepIndex);
    public bool IsLibraryDetail => FirstRunWizardFlow.IsLibraryDetail(StepIndex, SelectedPath);
    public bool IsDiscoverDetail => FirstRunWizardFlow.IsDiscoverDetail(StepIndex, SelectedPath);
    public DiscoverSetupCopyKind DiscoverCopyKind =>
        FirstRunWizardFlow.ResolveDiscoverCopy(OperatingSystem.IsMacOS(), HasAnyBundledApi);
    public bool ShowMacDiscoverCopy => IsDiscoverDetail && DiscoverCopyKind == DiscoverSetupCopyKind.Mac;
    public bool ShowBundledDiscoverCopy => IsDiscoverDetail && DiscoverCopyKind == DiscoverSetupCopyKind.Bundled;
    public bool ShowDirectDiscoverCopy => IsDiscoverDetail && DiscoverCopyKind == DiscoverSetupCopyKind.Direct;
    public bool IsLocalPathSelected => SelectedPath == FirstRunOnboardingPath.LocalLibrary;
    public bool IsDiscoverPathSelected => SelectedPath == FirstRunOnboardingPath.Discover;
    public bool CanAdvance => FirstRunWizardFlow.CanGoNext(StepIndex, SelectedPath);
    public bool ShowSkipStep => FirstRunWizardFlow.ShowSkipDetail(StepIndex, SelectedPath);
    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex >= FirstRunWizardFlow.LastStepIndex;

    public string WelcomeTitle => _l10n.Get("Wizard.FirstRun.Welcome.Title");
    public string WelcomeDesc => _l10n.Get("Wizard.FirstRun.Welcome.Desc");
    public string WelcomeHint => _l10n.Get("Wizard.FirstRun.Welcome.Hint");
    public string PathTitle => _l10n.Get("Wizard.FirstRun.Path.Title");
    public string PathDesc => _l10n.Get("Wizard.FirstRun.Path.Desc");
    public string PathLocalTitle => _l10n.Get("Wizard.FirstRun.Path.Local.Title");
    public string PathLocalDesc => _l10n.Get("Wizard.FirstRun.Path.Local.Desc");
    public string PathDiscoverTitle => _l10n.Get("Wizard.FirstRun.Path.Discover.Title");
    public string PathDiscoverDesc => _l10n.Get("Wizard.FirstRun.Path.Discover.Desc");
    public string LibraryTitle => _l10n.Get("Wizard.FirstRun.Library.Title");
    public string LibraryDesc => _l10n.Get("Wizard.FirstRun.Library.Desc");
    public string LibraryWatermark => _l10n.Get("Wizard.FirstRun.Library.Watermark");
    public string LibraryBrowseText => _l10n.Get("Wizard.FirstRun.Library.Browse");
    public string DiscoverBundledText => _l10n.Get("Wizard.FirstRun.Discover.Bundled");
    public string DiscoverDirectText => _l10n.Get("Wizard.FirstRun.Discover.Direct");
    public string DiscoverMacText => _l10n.Get("Wizard.FirstRun.Discover.Mac");
    public string SkipWizardText => _l10n.Get("Wizard.FirstRun.SkipWizard");
    public string SkipStepText => _l10n.Get("Wizard.FirstRun.SkipStep");
    public string BackText => _l10n.Get("Wizard.FirstRun.Back");
    public string NextButtonText => IsLastStep ? _l10n.Get("Wizard.FirstRun.Start") : _l10n.Get("Wizard.FirstRun.Next");

    public ObservableCollection<StepItem> WizardSteps { get; } = [];

    partial void OnStepIndexChanged(int value)
    {
        StatusMessage = string.Empty;
        NotifyStepUi();
        UpdateWizardSteps();
    }

    partial void OnSelectedPathChanged(FirstRunOnboardingPath value)
    {
        NotifyStepUi();
        UpdateWizardSteps();
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    private void NotifyStepUi()
    {
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsPathStep));
        OnPropertyChanged(nameof(IsLibraryDetail));
        OnPropertyChanged(nameof(IsDiscoverDetail));
        OnPropertyChanged(nameof(ShowMacDiscoverCopy));
        OnPropertyChanged(nameof(ShowBundledDiscoverCopy));
        OnPropertyChanged(nameof(ShowDirectDiscoverCopy));
        OnPropertyChanged(nameof(IsLocalPathSelected));
        OnPropertyChanged(nameof(IsDiscoverPathSelected));
        OnPropertyChanged(nameof(CanAdvance));
        OnPropertyChanged(nameof(ShowSkipStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonText));
    }

    [RelayCommand]
    private void SelectLocalPath() => SelectedPath = FirstRunOnboardingPath.LocalLibrary;

    [RelayCommand]
    private void SelectDiscoverPath() => SelectedPath = FirstRunOnboardingPath.Discover;

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
        _settings.Current.FirstRunOnboardingPath = FirstRunOnboardingPath.Unset;
        await _settings.SaveAsync().ConfigureAwait(true);
        _host.Complete(FirstRunWizardChoice.Skipped);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (!CanAdvance)
            return;

        if (StepIndex < FirstRunWizardFlow.LastStepIndex)
        {
            StepIndex++;
            return;
        }

        await FinishAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SkipCurrentStepAsync()
    {
        if (!ShowSkipStep)
            return;
        SkipLibraryStep = true;
        await FinishAsync().ConfigureAwait(true);
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

    private async Task FinishAsync()
    {
        try
        {
            if (SelectedPath == FirstRunOnboardingPath.LocalLibrary
                && !SkipLibraryStep
                && !string.IsNullOrWhiteSpace(LibraryPath))
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
            if (SelectedPath == FirstRunOnboardingPath.Discover)
            {
                _settings.Current.AutoStartBundledMusicApi = HasBundledGoMusicApi;
                _settings.Current.AutoStartBundledNetEaseApi = HasBundledNetEaseApi;
            }

            _settings.Current.FirstRunOnboardingPath = SelectedPath == FirstRunOnboardingPath.Unset
                ? FirstRunOnboardingPath.Unset
                : SelectedPath;
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
        WizardSteps.Add(new StepItem(
            _l10n.Get("Wizard.FirstRun.Step.Welcome"),
            StepIndex > 0 ? StepStatus.Finish : StepStatus.Process,
            1));
        WizardSteps.Add(new StepItem(
            _l10n.Get("Wizard.FirstRun.Step.Path"),
            StepIndex > 1 ? StepStatus.Finish : StepIndex == 1 ? StepStatus.Process : StepStatus.Wait,
            2));
        WizardSteps.Add(new StepItem(
            _l10n.Get("Wizard.FirstRun.Step.Setup"),
            StepIndex == 2 ? StepStatus.Process : StepStatus.Wait,
            3,
            showConnector: false));
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
