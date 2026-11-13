using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverTrackBatchSlicerTests
{
    private static OnlineTrack T(string id) => new()
    {
        Id = id,
        Title = id,
        Artist = "a",
        Platform = MusicPlatform.NetEase
    };

    [Fact]
    public void Slice_empty_pool_returns_empty() =>
        Assert.Empty(DiscoverTrackBatchSlicer.Slice([], take: 10, offset: 0, excludeIds: null));

    [Fact]
    public void Slice_wraps_and_takes_exact_count()
    {
        var pool = Enumerable.Range(0, 15).Select(i => T(i.ToString())).ToList();
        var batch = DiscoverTrackBatchSlicer.Slice(pool, take: 10, offset: 10, excludeIds: null);
        Assert.Equal(10, batch.Count);
        Assert.Equal("10", batch[0].Id);
        Assert.Equal("4", batch[9].Id); // wrap: 10..14,0..4
    }

    [Fact]
    public void Slice_excludes_ids_then_wraps()
    {
        var pool = Enumerable.Range(0, 12).Select(i => T(i.ToString())).ToList();
        var exclude = Enumerable.Range(0, 10).Select(i => i.ToString()).ToArray();
        var batch = DiscoverTrackBatchSlicer.Slice(pool, take: 10, offset: 0, excludeIds: exclude);
        Assert.Equal(10, batch.Count);
        Assert.DoesNotContain(batch, t => t.Id is "0" or "1"); // first excluded batch gone; uses 10,11 then wraps remaining
        Assert.Contains(batch, t => t.Id == "10");
    }
}
