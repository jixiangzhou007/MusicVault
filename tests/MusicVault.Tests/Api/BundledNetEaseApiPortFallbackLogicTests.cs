using MusicVault.Services.Api;
using Xunit;

namespace MusicVault.Tests.Api;

public sealed class BundledNetEaseApiPortFallbackLogicTests
{
    [Theory]
    [InlineData("http://localhost:3000", 3000)]
    [InlineData("http://127.0.0.1:13000", 13000)]
    [InlineData("http://localhost:13000/", 13000)]
    public void IsLocalApiUrl_accepts_default_and_fallback_ports(string url, int expectedPort)
    {
        Assert.True(BundledNetEaseApiHost.IsLocalApiUrl(url));
        Assert.True(Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri));
        Assert.Equal(expectedPort, uri.Port);
    }

    [Fact]
    public void Fallback_and_default_urls_are_distinct()
    {
        Assert.NotEqual(
            BundledNetEaseApiHost.DefaultBaseUrl.TrimEnd('/'),
            BundledNetEaseApiHost.FallbackBaseUrl.TrimEnd('/'),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3000, BundledNetEaseApiHost.DefaultPort);
        Assert.Equal(13000, BundledNetEaseApiHost.FallbackPort);
    }
}
