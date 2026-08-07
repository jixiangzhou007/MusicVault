using MusicVault.Services.Api;
using Xunit;

namespace MusicVault.Tests.Api;

public sealed class BundledMusicApiPortFallbackLogicTests
{
    [Theory]
    [InlineData("http://localhost:8080", 8080)]
    [InlineData("http://127.0.0.1:18080", 18080)]
    [InlineData("http://localhost:18080/", 18080)]
    public void IsLocalApiUrl_accepts_both_default_and_fallback_ports(string url, int expectedPort)
    {
        Assert.True(BundledMusicApiHost.IsLocalApiUrl(url));
        Assert.True(Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri));
        Assert.Equal(expectedPort, uri.Port);
    }

    [Fact]
    public void Fallback_and_default_urls_are_distinct_local_candidates()
    {
        Assert.NotEqual(
            BundledMusicApiHost.DefaultBaseUrl.TrimEnd('/'),
            BundledMusicApiHost.FallbackBaseUrl.TrimEnd('/'),
            StringComparer.OrdinalIgnoreCase);
        Assert.True(BundledMusicApiHost.IsLocalApiUrl(BundledMusicApiHost.DefaultBaseUrl));
        Assert.True(BundledMusicApiHost.IsLocalApiUrl(BundledMusicApiHost.FallbackBaseUrl));
    }
}
