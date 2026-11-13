using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class DiscoverPrimaryCtaTests
{
    [Theory]
    [InlineData(true, true, DiscoverPrimaryCtaKind.Resume)]
    [InlineData(false, true, DiscoverPrimaryCtaKind.BrowseFeatured)]
    [InlineData(false, false, DiscoverPrimaryCtaKind.FocusSearch)]
    public void Resolve_follows_priority(
        bool hasRecentPlayback,
        bool browseHubReady,
        DiscoverPrimaryCtaKind expected) =>
        Assert.Equal(expected, DiscoverPrimaryCta.Resolve(hasRecentPlayback, browseHubReady));
}
