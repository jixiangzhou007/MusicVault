using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class PaneSplitWidthStoreTests
{
    [Fact]
    public void ClampToGrid_respects_min_and_max()
    {
        var clamped = PaneSplitWidthStore.ClampToGrid(50, gridWidth: 1000);
        Assert.Equal(PaneSplitWidthStore.MinPaneWidth, clamped);

        var wide = PaneSplitWidthStore.ClampToGrid(900, gridWidth: 1000);
        Assert.True(wide <= (1000 - PaneSplitWidthStore.SplitterHitWidth) * PaneSplitWidthStore.MaxPaneWidthRatio);
        Assert.True(wide >= PaneSplitWidthStore.MinPaneWidth);
    }
}
