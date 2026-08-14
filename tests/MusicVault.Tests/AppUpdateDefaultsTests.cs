using MusicVault.App.Services;

namespace MusicVault.Tests;

public class AppUpdateDefaultsTests
{
    [Fact]
    public void ResolveFeedUrl_PrefersEnvironmentThenSettingsThenDefault()
    {
        Assert.Equal(
            "https://env.example/feed",
            AppUpdateDefaults.ResolveFeedUrl("https://settings.example", "https://env.example/feed"));
        Assert.Equal(
            "https://settings.example",
            AppUpdateDefaults.ResolveFeedUrl("https://settings.example", null));
        Assert.Equal(
            AppUpdateDefaults.DefaultGithubRepoUrl,
            AppUpdateDefaults.ResolveFeedUrl("  ", null));
    }

    [Theory]
    [InlineData("https://github.com/jixiangzhou007/MusicVault", true)]
    [InlineData("https://github.com/jixiangzhou007/MusicVault/", true)]
    [InlineData("https://www.github.com/a/b", true)]
    [InlineData("https://cdn.example.com/musicvault", false)]
    [InlineData("https://github.com/only-one", false)]
    [InlineData("not-a-url", false)]
    public void IsGithubRepoUrl_DetectsRepoRoots(string url, bool expected) =>
        Assert.Equal(expected, AppUpdateDefaults.IsGithubRepoUrl(url));
}
