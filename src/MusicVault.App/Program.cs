using Avalonia;
using MusicVault.App.Services;
using Velopack;

namespace MusicVault.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                WriteFatalError(ex);
        };

        try
        {
            InitializeVelopack();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteFatalError(ex);
            Environment.Exit(1);
        }
    }

    private static void WriteFatalError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicVault");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "startup-error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}");
        }
        catch { /* ignore */ }
    }

    private static void InitializeVelopack()
    {
        try
        {
            var app = VelopackApp.Build()
                .OnFirstRun(version => Console.WriteLine($"MusicVault 首次安装 v{version}"));

            if (OperatingSystem.IsWindows())
                app = app.OnAfterUpdateFastCallback(version => Console.WriteLine($"MusicVault 已更新至 v{version}"));

            app.Run();
        }
        catch
        {
            // 开发模式或未安装 Velopack 引导程序时忽略
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
