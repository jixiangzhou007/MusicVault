using Avalonia;
using Avalonia.Headless;
using MusicVault.App;

[assembly: AvaloniaTestApplication(typeof(MusicVault.Tests.App.HeadlessTestAppBuilder))]

namespace MusicVault.Tests.App;

public static class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => Program.BuildAvaloniaApp()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
