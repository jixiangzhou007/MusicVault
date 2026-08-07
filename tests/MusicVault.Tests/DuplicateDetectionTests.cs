using MusicVault.Core.Models;
using MusicVault.Services.Deduplication;

namespace MusicVault.Tests;

public class DuplicateDetectionTests
{
    [Fact]
    public void IsLikelyDuplicate_MatchesTitleArtistAndSize()
    {
        var db = Path.Combine(Path.GetTempPath(), $"mv-test-{Guid.NewGuid():N}.db");
        var svc = new DuplicateDetectionService(db);
        var a = new Track { Title = "Hello", Artist = "World", FileSizeBytes = 1000 };
        var b = new Track { Title = "hello", Artist = "world", FileSizeBytes = 1000 };
        Assert.True(svc.IsLikelyDuplicate(a, b));
    }

    [Fact]
    public void IsLikelyDuplicate_RejectsDifferentSize()
    {
        var db = Path.Combine(Path.GetTempPath(), $"mv-test-{Guid.NewGuid():N}.db");
        var svc = new DuplicateDetectionService(db);
        var a = new Track { Title = "A", Artist = "B", FileSizeBytes = 100 };
        var b = new Track { Title = "A", Artist = "B", FileSizeBytes = 200 };
        Assert.False(svc.IsLikelyDuplicate(a, b));
    }
}
