using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;

namespace MusicVault.Tests;

public sealed class TrackRepositoryTopPlayedTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TrackRepository _tracks;

    public TrackRepositoryTopPlayedTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "mv_top_played_" + Guid.NewGuid().ToString("N") + ".db");
        DatabaseInitializer.InitializeAsync(_dbPath).GetAwaiter().GetResult();
        _tracks = new TrackRepository(_dbPath);
    }

    [Fact]
    public async Task GetTopPlayedAsync_orders_by_play_count_and_respects_minimum()
    {
        await InsertTrackAsync("low.flac", playCount: 1);
        var mid = await InsertTrackAsync("mid.flac", playCount: 3);
        var top = await InsertTrackAsync("top.flac", playCount: 8);

        var result = await _tracks.GetTopPlayedAsync(limit: 5, minPlayCount: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(top, result[0].Id);
        Assert.Equal(mid, result[1].Id);
    }

    [Fact]
    public async Task GetTopPlayedExcludingAsync_skips_excluded_ids()
    {
        var keep = await InsertTrackAsync("keep.flac", playCount: 5);
        var skip = await InsertTrackAsync("skip.flac", playCount: 9);

        var result = await _tracks.GetTopPlayedExcludingAsync(
            limit: 5,
            minPlayCount: 2,
            excludeTrackIds: [keep]);

        Assert.Single(result);
        Assert.Equal(skip, result[0].Id);
    }

    [Fact]
    public async Task GetRecentlyPlayedAsync_orders_by_last_played_and_excludes_ids()
    {
        var older = await InsertTrackAsync("older.flac", playCount: 1);
        var newer = await InsertTrackAsync("newer.flac", playCount: 1);
        await _tracks.RecordTrackPlayedAsync(older);
        await Task.Delay(10);
        await _tracks.RecordTrackPlayedAsync(newer);

        var result = await _tracks.GetRecentlyPlayedAsync(limit: 5, excludeTrackIds: [older]);

        Assert.Single(result);
        Assert.Equal(newer, result[0].Id);
    }

    [Fact]
    public async Task GetRecentByArtistsExcludingAsync_returns_same_artist_tracks()
    {
        var artist = "Jay";
        var frequent = await InsertTrackAsync("freq.flac", playCount: 5, artist: artist);
        var sibling = await InsertTrackAsync("sibling.flac", playCount: 1, artist: artist);
        _ = await InsertTrackAsync("other.flac", playCount: 1, artist: "Other");

        var result = await _tracks.GetRecentByArtistsExcludingAsync(
            [artist],
            [frequent],
            limit: 5);

        Assert.Single(result);
        Assert.Equal(sibling, result[0].Id);
    }

    [Fact]
    public async Task GetSimilarByAcoustIdAsync_returns_matching_tracks()
    {
        const string acoustId = "0123456789abcdef0123456789abcdef";
        var seed = await InsertTrackAsync("seed.flac", playCount: 5, acoustId: acoustId);
        var match = await InsertTrackAsync("match.flac", playCount: 2, acoustId: acoustId);
        _ = await InsertTrackAsync("other.flac", playCount: 2, acoustId: "ffffffffffffffffffffffffffffffff");

        var result = await _tracks.GetSimilarByAcoustIdAsync(acoustId, [seed], limit: 5);

        Assert.Single(result);
        Assert.Equal(match, result[0].Id);
    }

    [Fact]
    public async Task GetSimilarByAlbumAsync_returns_same_album_tracks()
    {
        var artist = "Jay";
        const string album = "Fantasy";
        var seed = await InsertTrackAsync("seed.flac", playCount: 5, artist: artist, album: album);
        var sibling = await InsertTrackAsync("sibling.flac", playCount: 1, artist: artist, album: album);
        _ = await InsertTrackAsync("other.flac", playCount: 1, artist: artist, album: "Other");

        var result = await _tracks.GetSimilarByAlbumAsync(artist, album, [seed], limit: 5);

        Assert.Single(result);
        Assert.Equal(sibling, result[0].Id);
    }

    private async Task<long> InsertTrackAsync(
        string fileName,
        int playCount,
        string artist = "Artist",
        string? album = null,
        string? acoustId = null)
    {
        var path = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(path, "audio");

        return await _tracks.UpsertAsync(new Track
        {
            FilePath = path,
            Title = fileName,
            Artist = artist,
            Album = album ?? string.Empty,
            AcoustId = acoustId,
            PlayCount = playCount,
            LastPlayedAt = DateTime.UtcNow
        });
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // ignore
        }
    }
}
