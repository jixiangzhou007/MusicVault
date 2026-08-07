using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class ShellNavigationLocalizationTests
{
    [Theory]
    [InlineData("zh-CN", "Nav.Library", "曲库")]
    [InlineData("zh-CN", "Nav.Discover", "发现")]
    [InlineData("en-US", "Nav.Library", "Library")]
    [InlineData("en-US", "Nav.Discover", "Discover")]
    [InlineData("en-US", "Settings.Section.Advanced", "Online Sources")]
    public void LocalizationService_resolves_shell_navigation_keys(string culture, string key, string expected)
    {
        var settings = new AppSettingsService(Path.Combine(Path.GetTempPath(), "MusicVault.Tests", Guid.NewGuid().ToString("N")));
        settings.Current.Culture = culture;
        var l10n = new LocalizationService(settings);

        Assert.Equal(expected, l10n.Get(key));
    }
}
