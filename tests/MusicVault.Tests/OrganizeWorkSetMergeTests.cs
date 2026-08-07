using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Models;

namespace MusicVault.Tests;

public sealed class OrganizeWorkSetMergeTests
{
    [Fact]
    public void Merge_Skips_Duplicate_Track_Ids()
    {
        var track1 = new Track { Id = 1, Title = "A" };
        var track2 = new Track { Id = 2, Title = "B" };
        var existing = new List<BatchScrapeQueueItemViewModel>
        {
            new() { Track = track1 }
        };
        var incoming = new[]
        {
            new BatchScrapeQueueItemViewModel { Track = track1 },
            new BatchScrapeQueueItemViewModel { Track = track2 }
        };

        var merged = OrganizeWorkSetMerge.Merge(existing, incoming);
        Assert.Equal(2, merged.Count);
        Assert.Equal(1, merged[0].Track.Id);
        Assert.Equal(2, merged[1].Track.Id);
    }
}
