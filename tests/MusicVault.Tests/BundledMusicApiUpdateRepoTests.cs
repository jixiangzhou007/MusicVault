using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests;

public sealed class BundledMusicApiUpdateRepoTests
{
    [Theory]
    [InlineData(null, null, BundledMusicApiUpdateService.DefaultGithubRepo)]
    [InlineData("", "", BundledMusicApiUpdateService.DefaultGithubRepo)]
    [InlineData("myorg/go-music-api", null, "myorg/go-music-api")]
    [InlineData("https://github.com/myorg/go-music-api", null, "myorg/go-music-api")]
    [InlineData("https://github.com/myorg/go-music-api.git", null, "myorg/go-music-api")]
    [InlineData("myorg/go-music-api", "env/override", "env/override")]
    public void ResolveGithubRepo_prefers_env_then_configured(string? configured, string? env, string expected)
    {
        Assert.Equal(expected, BundledMusicApiUpdateService.ResolveGithubRepo(configured, env));
    }

    [Theory]
    [InlineData("not a repo")]
    [InlineData("onlyone")]
    [InlineData("https://example.com/x/y")]
    public void NormalizeGithubRepo_rejects_invalid(string value)
    {
        Assert.Null(BundledMusicApiUpdateService.NormalizeGithubRepo(value));
    }

}
