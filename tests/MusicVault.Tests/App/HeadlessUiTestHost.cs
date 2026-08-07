using Avalonia.Threading;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.Views;
using MusicVault.Services;

namespace MusicVault.Tests.App;

/// <summary>Headless UI 冒烟测试宿主：隔离数据目录，跳过版权/向导弹窗。</summary>
public sealed class HeadlessUiTestHost : IAsyncDisposable
{
    public const string TestDataDirEnvVar = "MUSICVAULT_TEST_DATA_DIR";

    public AppServices Services { get; }
    public AppSettingsService Settings { get; }
    public ThemeService Theme { get; }
    public ILocalizationService Localization { get; }
    public MainWindowViewModel Shell { get; }
    public MainWindow Window { get; }

    private readonly string _tempDir;

    private HeadlessUiTestHost(
        string tempDir,
        AppServices services,
        AppSettingsService settings,
        ThemeService theme,
        ILocalizationService localization,
        MainWindowViewModel shell,
        MainWindow window)
    {
        _tempDir = tempDir;
        Services = services;
        Settings = settings;
        Theme = theme;
        Localization = localization;
        Shell = shell;
        Window = window;
    }

    /// <remarks>须在 AvaloniaFact 的 UI 线程上调用，且 await 后勿 ConfigureAwait(false)。</remarks>
    public static Task<HeadlessUiTestHost> CreateAsync() => CreateAsync("zh-CN");

    public static async Task<HeadlessUiTestHost> CreateAsync(string culture)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MusicVault-headless-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Environment.SetEnvironmentVariable(TestSessionProbe.TestDataDirEnvVar, tempDir);

        var services = new AppServices();
        await services.InitializeCoreAsync();
        CoverArtCache.Initialize(services.DataDirectory);

        var settings = new AppSettingsService(services.DataDirectory);
        await settings.LoadAsync();
        settings.Current.CopyrightAccepted = true;
        settings.Current.FirstRunWizardCompleted = true;
        settings.Current.Culture = culture;
        await settings.SaveAsync();

        Dispatcher.UIThread.VerifyAccess();

        var localization = new LocalizationService(settings);
        var theme = new ThemeService(settings);
        theme.Apply(settings.Current.ThemeMode);

        var shell = new MainWindowViewModel(services, settings, theme, localization);
        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1320,
            Height = 860
        };
        window.Show();

        return new HeadlessUiTestHost(tempDir, services, settings, theme, localization, shell, window);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                await DisposeCoreAsync().ConfigureAwait(true);
            else
                await Dispatcher.UIThread.InvokeAsync(DisposeCoreAsync).ConfigureAwait(true);
        }
        catch
        {
            // 冒烟测试以断言为主，清理失败不视为用例失败
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestSessionProbe.TestDataDirEnvVar, null);
            TryDeleteTempDir();
        }
    }

    private async Task DisposeCoreAsync()
    {
        try { Shell.DesktopLyrics.Dispose(); } catch { /* ignore */ }
        try { Shell.Player.StopAndSaveSessionForExit(); } catch { /* ignore */ }

        Services.Scanner.StopBackgroundScan();
        Services.Downloads.CancelAll();

        try { Services.Player.Stop(); } catch { /* ignore */ }

        MusicVault.Services.Api.BundledMusicApiHost.Instance.StopIfStartedByApp();
        MusicVault.Services.Api.BundledNetEaseApiHost.Instance.StopIfStartedByApp();

        try { Window.Close(); } catch { /* ignore */ }

        await Services.DisposeAsync().ConfigureAwait(true);
    }

    private void TryDeleteTempDir()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
