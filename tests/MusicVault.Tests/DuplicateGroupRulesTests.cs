using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Models;
using MusicVault.Services.Deduplication;

namespace MusicVault.Tests;

public sealed class DuplicateGroupRulesTests
{
    [Fact]
    public void PickKeeper_Prefers_Larger_FileSize()
    {
        var small = new Track { Id = 1, Title = "A", Artist = "B", FileSizeBytes = 100 };
        var large = new Track { Id = 2, Title = "A", Artist = "B", FileSizeBytes = 9_000_000 };
        var keeper = DuplicateGroupRules.PickKeeper([small, large]);
        Assert.Equal(2, keeper.Id);
    }

    [Fact]
    public void TracksToRemove_Excludes_Keeper_Per_Group()
    {
        var t1 = new Track { Id = 1, Title = "X", Artist = "Y", FileSizeBytes = 1 };
        var t2 = new Track { Id = 2, Title = "X", Artist = "Y", FileSizeBytes = 2 };
        var groups = new List<DuplicateGroup> { new("k", [t1, t2]) };
        var remove = DuplicateGroupRules.TracksToRemove(groups);
        Assert.Single(remove);
        Assert.Equal(1, remove[0].Id);
    }
}
