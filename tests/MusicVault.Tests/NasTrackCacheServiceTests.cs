using MusicVault.Core.Models;
using MusicVault.Core.Nas;

namespace MusicVault.Tests;

public sealed class NasTrackCacheServiceTests : IDisposable
{
    private readonly string _dir;

    public NasTrackCacheServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mv_nas_cache_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task EnsureCachedAsync_copies_and_reuses_cache()
    {
        var sourceDir = Path.Combine(_dir, "source");
        Directory.CreateDirectory(sourceDir);
        var sourcePath = Path.Combine(sourceDir, "song.flac");
        await File.WriteAllTextAsync(sourcePath, "fake-audio-content");

        var track = new Track
        {
            Id = 42,
            FilePath = sourcePath,
            Title = "Song",
            Artist = "Artist"
        };

        var cache = new MusicVault.Services.Library.NasTrackCacheService(_dir);
        var first = await cache.EnsureCachedAsync(track);
        var second = cache.TryGetCachedPath(track);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.True(File.Exists(first!));
    }

    [Fact]
    public async Task GetStats_and_ClearAll_remove_cache_entries()
    {
        var sourceDir = Path.Combine(_dir, "source2");
        Directory.CreateDirectory(sourceDir);
        var sourcePath = Path.Combine(sourceDir, "song.mp3");
        await File.WriteAllTextAsync(sourcePath, "fake-audio-content");

        var track = new Track { Id = 99, FilePath = sourcePath, Title = "T", Artist = "A" };
        var cache = new MusicVault.Services.Library.NasTrackCacheService(_dir);
        await cache.EnsureCachedAsync(track);

        var stats = cache.GetStats();
        Assert.Equal(1, stats.EntryCount);
        Assert.True(stats.TotalBytes > 0);

        cache.ClearAll();

        Assert.Null(cache.TryGetCachedPath(track));
        Assert.Equal(0, cache.GetStats().EntryCount);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch { /* ignore */ }
    }
}
