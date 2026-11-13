using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;
using MusicVault.App.Views.Dialogs;
using MusicVault.Services;
using MusicVault.Services.Library;
using BuildInfo = MusicVault.App.Services.BuildInfo;
using CoreLog = MusicVault.Core.Logging.MusicVaultLog;

namespace MusicVault.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;
    public static AppSettingsService Settings { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static ILocalizationService Localization { get; private set; } = null!;
    internal static MainWindowViewModel? Shell { get; private set; }
#if WINDOWS
    private static WindowsSystemMediaTransportService? SystemMediaTransport;
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ButtonAlignmentFix.Install();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        try
        {
            await StartupAsync();
        }
        catch (Exception ex)
        {
            WriteStartupError(ex);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            return;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartupAsync()
    {
        Services = new AppServices(AppLog.Factory);
        await Services.InitializeCoreAsync();
        CoverArtCache.Initialize(Services.DataDirectory);

        Settings = new AppSettingsService(Services.DataDirectory);
        await Settings.LoadAsync();

        Localization = new LocalizationService(Settings);
        TrackRowActionLabels.Bind(Localization);
        TransferRowLabels.Bind(Localization);
        OnlineTrackRowLabels.Bind(Localization);
        TrackTableHeaderLabels.Bind(Localization);
        MusicPlatformLabels.Bind(Localization);
        CommonDialogLabels.Bind(Localization);
        MusicGenreLabels.Bind(Localization);
        MusicVault.Services.Metadata.AcoustIdLookupConfiguration.Apply(
            Settings.Current.EnableAcoustIdFingerprintLookup,
            Settings.Current.AcoustIdApiKey);

        ApplyDownloadDirectoryFromSettings();

        Theme = new ThemeService(Settings);
        Theme.Apply(Settings.Current.ThemeMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdown;

            var shell = new MainWindowViewModel(Services, Settings, Theme, Localization);
            Shell = shell;

            var mainWindow = new MainWindow
            {
                DataContext = shell,
                Title = BuildInfo.WindowTitle
            };
            shell.AboutRequested += (_, _) => new AboutWindow().Show(mainWindow);
            shell.BatchMetadataRequested += (_, vm) => new BatchMetadataWindow(vm).Show(mainWindow);

            new GlobalHotkeyService().Attach(mainWindow, shell, Settings);

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // 必须先有主窗口，否则版权窗关闭后应用会因「无窗口」自动退出
            if (!Settings.HasAcceptedCopyright)
            {
                var copyright = new CopyrightWindow();
                await copyright.ShowDialog(mainWindow);
                if (!copyright.IsAccepted)
                {
                    desktop.Shutdown();
                    return;
                }
                await Settings.AcceptCopyrightAsync();
            }

            if (!Settings.Current.FirstRunWizardCompleted)
            {
                var libraries = await Services.LibraryManager.GetAllAsync();
                if (libraries.Count > 0)
                {
                    Settings.Current.FirstRunWizardCompleted = true;
                    await Settings.SaveAsync();
                }
                else
                {
                    await shell.AppModal.ShowFirstRunWizardAsync(
                        Services,
                        Settings,
                        Localization,
                        shell.Libraries).ConfigureAwait(true);
                }
            }

            mainWindow.Activate();

#if WINDOWS
            SystemMediaTransport = new WindowsSystemMediaTransportService();
            SystemMediaTransport.Attach(mainWindow, shell.Player);
#endif

            _ = RunBackgroundStartupAsync();

            var log = AppLog.Default;
            CoreLog.AppStarted(log, BuildInfo.WindowTitle,
                OperatingSystem.IsWindows() ? "Windows" :
                OperatingSystem.IsMacOS() ? "macOS" : "Unknown");

            if (SettingsPageViewModel.ShouldCheckOnStartup(Settings))
                _ = CheckUpdatesInBackgroundAsync();
        }
    }

    private static async Task RunBackgroundStartupAsync()
    {
        try
        {
            if (await MusicApiBootstrap.PersistDefaultServiceUrlsIfNeededAsync(
                    Settings.Current, Services.Gatherers).ConfigureAwait(false))
                await Settings.SaveAsync().ConfigureAwait(false);

            if (await MusicApiBootstrap.EnsureBundledApisAsync(Settings.Current).ConfigureAwait(false))
                await Settings.SaveAsync().ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.MainWindow?.DataContext is MainWindowViewModel shell)
                {
                    await shell.RefreshOnlineApiHealthAfterBootstrapAsync().ConfigureAwait(true);
                }
                else
                {
                    await OnlineApiHealthService.Instance.RefreshAsync(force: false).ConfigureAwait(false);
                }
            });
            var legacyHistory = Settings.Current.RecentPlayHistory
                .Select(e => new MusicVault.Data.Repositories.PlayHistoryEntryRecord
                {
                    TrackId = e.TrackId,
                    FilePath = e.FilePath
                })
                .ToList();
            await Services.RunDeferredStartupAsync(legacyHistory).ConfigureAwait(false);
            NotifyNasMountFailuresIfAny();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.MainWindow?.DataContext is MainWindowViewModel shell)
                {
                    await shell.RestorePlaybackAfterDeferredStartupAsync().ConfigureAwait(true);
                }
            });
            if (legacyHistory.Count > 0
                && await Services.PlayHistory.CountAsync().ConfigureAwait(false) > 0)
            {
                Settings.Current.RecentPlayHistory = [];
                await Settings.SaveAsync().ConfigureAwait(false);
            }

            if (BundledMusicApiUpdateService.ShouldCheckOnStartup(Settings.Current))
            {
                var apiUpdater = new BundledMusicApiUpdateService();
                var apiResult = await apiUpdater.CheckForUpdateAsync().ConfigureAwait(false);
                Settings.Current.LastBundledMusicApiCheckAt = DateTime.UtcNow;
                Settings.Current.PendingMusicApiUpdateNotice = apiResult.UpdateAvailable
                    ? apiResult.Message
                    : null;
                await Settings.SaveAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            CoreLog.NonCriticalFailure(AppLog.Default, "后台启动任务", ex.ToString());
        }
    }

    private static void NotifyNasMountFailuresIfAny()
    {
        NotifyRemoteMountFailures(Services.NasLibraries.LastMountSummary);
        NotifyRemoteMountFailures(Services.WebDavLibraries.LastMountSummary);
    }

    private static void NotifyRemoteMountFailures(object? summary)
    {
        switch (summary)
        {
            case NasMountSummary nas when nas.Failed.Count > 0:
                foreach (var failure in nas.Failed)
                {
                    ShellStatusService.Instance.Warn(
                        string.Format(
                            Localization.Get("Nas.Startup.MountFailed"),
                            failure.LibraryName,
                            ServiceMessageLocalizer.Resolve(Localization, failure.Message)),
                        Localization.Get("Nas.Startup.Category"));
                }
                break;
            case WebDavMountSummary webDav when webDav.Failed.Count > 0:
                foreach (var failure in webDav.Failed)
                {
                    ShellStatusService.Instance.Warn(
                        string.Format(
                            Localization.Get("WebDav.Startup.MountFailed"),
                            failure.LibraryName,
                            ServiceMessageLocalizer.Resolve(Localization, failure.Message)),
                        Localization.Get("WebDav.Startup.Category"));
                }
                break;
        }
    }

    private static void ApplyDownloadDirectoryFromSettings()
    {
        var configured = Settings.Current.DownloadDirectory;
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Services.DataDirectory, "Downloads")
            : configured.Trim();
        Services.Downloads.SetDownloadRoot(path);
        var saveSettings = false;
        if (string.IsNullOrWhiteSpace(configured))
        {
            Settings.Current.DownloadDirectory = path;
            saveSettings = true;
        }

        if (MusicApiBootstrap.ApplyConfig(Settings.Current, Services.Gatherers))
            saveSettings = true;

        if (saveSettings)
            _ = Settings.SaveAsync();
    }


    private static void WriteStartupError(Exception ex)
    {
        try
        {
            var log = AppLog.Default;
            CoreLog.UnhandledError(log, ex.ToString());
        }
        catch
        {
            // 日志系统本身不可用时，回退到文件写入
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MusicVault");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "startup-error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}");
            }
            catch { /* 最后的回退：无法写入任何内容 */ }
        }
    }

    private static async Task CheckUpdatesInBackgroundAsync()
    {
        try
        {
            var updater = new UpdateService(Settings);
            var result = await updater.CheckAndDownloadAsync();
            Settings.Current.LastUpdateCheckAt = DateTime.UtcNow;
            await Settings.SaveAsync();

            if (result.HasUpdate)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
                        && d.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        shell.StatusMessage = ServiceMessageLocalizer.Resolve(Localization, result.Message);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            ShellStatusService.Instance.Warn(
                ServiceMessageLocalizer.Format(Localization, "Update.BackgroundCheckFailed", ex.Message),
                Localization.Get("Settings.Update.Check"));
        }
    }

    private static void OnShutdown(object? sender, ShutdownRequestedEventArgs e)
    {
        var log = AppLog.Default;
        CoreLog.AppShuttingDown(log);

        try
        {
            var shell = Shell;
            if (shell != null)
            {
                try { shell.Settings.FlushAutosaveAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) { CoreLog.NonCriticalFailure(log, "设置自动保存冲刷", ex.Message); }
                shell.DesktopLyrics.Dispose();
                shell.Player.StopAndSaveSessionForExit();
            }
            else if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
                     && d.MainWindow?.DataContext is MainWindowViewModel fallbackShell)
            {
                try { fallbackShell.Settings.FlushAutosaveAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) { CoreLog.NonCriticalFailure(log, "设置自动保存冲刷", ex.Message); }
                fallbackShell.DesktopLyrics.Dispose();
                fallbackShell.Player.StopAndSaveSessionForExit();
            }

            Shell = null;

#if WINDOWS
            SystemMediaTransport?.Dispose();
            SystemMediaTransport = null;
#endif

            MusicVault.Services.Api.BundledMusicApiHost.Instance.StopIfStartedByApp();
            MusicVault.Services.Api.BundledNetEaseApiHost.Instance.StopIfStartedByApp();

            if (Services != null)
            {
                try { Services.NasLibraries.DisconnectMountedShares(); }
                catch (Exception ex) { CoreLog.NonCriticalFailure(log, "NAS 断开", ex.Message); }
                try { Services.Player.Stop(); }
                catch (Exception ex) { CoreLog.NonCriticalFailure(log, "播放器停止", ex.Message); }
                Services.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception ex)
        {
            CoreLog.NonCriticalFailure(log, "关闭清理", ex.ToString());
        }
    }
}
