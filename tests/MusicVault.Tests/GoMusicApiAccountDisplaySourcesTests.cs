using MusicVault.Services.Api;
using Xunit;

namespace MusicVault.Tests;

public sealed class GoMusicApiAccountDisplaySourcesTests
{
    [Fact]
    public void DisplaySourceOrder_includes_discover_capsules_and_qq_wx()
    {
        Assert.Contains("qq", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("qq_wx", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("netease", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("kugou", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("kuwo", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("migu", GoMusicApiAccountClient.DisplaySourceOrder);
        Assert.Contains("soda", GoMusicApiAccountClient.DisplaySourceOrder);
    }

    [Fact]
    public void KnownQrCapable_does_not_claim_kuwo_migu_soda()
    {
        Assert.True(GoMusicApiAccountClient.IsKnownQrCapableSource("netease"));
        Assert.True(GoMusicApiAccountClient.IsKnownQrCapableSource("qq"));
        Assert.True(GoMusicApiAccountClient.IsKnownQrCapableSource("kugou"));
        Assert.False(GoMusicApiAccountClient.IsKnownQrCapableSource("kuwo"));
        Assert.False(GoMusicApiAccountClient.IsKnownQrCapableSource("migu"));
        Assert.False(GoMusicApiAccountClient.IsKnownQrCapableSource("soda"));
    }

    [Fact]
    public void DefaultQrSourceDescriptors_only_known_capable()
    {
        var sources = GoMusicApiAccountClient.DefaultQrSourceDescriptors()
            .Select(d => d.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(GoMusicApiAccountClient.KnownQrCapableSources.Length, sources.Count);
        foreach (var s in GoMusicApiAccountClient.KnownQrCapableSources)
            Assert.Contains(s, sources);
    }

    [Fact]
    public void IsRegisteredLoginSource_covers_display_order_not_bilibili()
    {
        foreach (var s in GoMusicApiAccountClient.DisplaySourceOrder)
            Assert.True(GoMusicApiAccountClient.IsRegisteredLoginSource(s));
        Assert.False(GoMusicApiAccountClient.IsRegisteredLoginSource("bilibili"));
    }
}
