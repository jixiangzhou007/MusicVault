using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Playlist;

namespace MusicVault.Tests;

public sealed class FavoritesMetadataPersistenceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mv-fav-" + Guid.NewGuid().ToString("N"));
    private string _dbPath = null!;
    private TrackRepository _tracks = null!;
    private FavoritesService _favorites = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "musicvault.db");
        await DatabaseInitializer.InitializeAsync(_dbPath);

        _tracks = new TrackRepository(_dbPath);
        var playlists = new PlaylistRepository(_dbPath);
        var playlistService = new PlaylistService(
            playlists,
            _tracks,
            new PlaylistImportService(),
            null!,
            [],
            new DownloadFailureRepository(_dbPath));
        _favorites = new FavoritesService(playlists, playlistService, _dbPath);
        await _favorites.EnsureInitializedAsync();
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpsertMetadata_DoesNotRemoveFavorite()
    {
        var track = await SeedTrackAsync("Song A", "Artist A");
        await _favorites.SetFavoriteAsync(track.Id, favorite: true);
        Assert.True(await _favorites.IsFavoriteAsync(track.Id));

        track.Title = "Song A (Edited)";
        track.Artist = "Artist A (Edited)";
        track.Id = await _tracks.UpsertAsync(track);
        await _favorites.SetFavoriteAsync(track.Id, favorite: true);

        Assert.True(await _favorites.IsFavoriteAsync(track.Id));
    }

    [Fact]
    public async Task MigrateTrackAsync_MovesFavoriteToNewTrackId()
    {
        var oldTrack = await SeedTrackAsync("Old", "Artist");
        await _favorites.SetFavoriteAsync(oldTrack.Id, favorite: true);

        var newTrack = new Track
        {
            FilePath = Path.Combine(_root, "new-path.mp3"),
            Title = "New",
            Artist = "Artist",
            AddedAt = DateTime.UtcNow
        };
        newTrack.Id = await _tracks.UpsertAsync(newTrack);

        await _favorites.MigrateTrackAsync(oldTrack.Id, newTrack.Id);

        Assert.False(await _favorites.IsFavoriteAsync(oldTrack.Id));
        Assert.True(await _favorites.IsFavoriteAsync(newTrack.Id));
    }

    [Fact]
    public async Task SetFavoriteAsync_IsIdempotent()
    {
        var track = await SeedTrackAsync("Idempotent", "Artist");
        await _favorites.SetFavoriteAsync(track.Id, favorite: true);
        await _favorites.SetFavoriteAsync(track.Id, favorite: true);
        Assert.True(await _favorites.IsFavoriteAsync(track.Id));

        await _favorites.SetFavoriteAsync(track.Id, favorite: false);
        await _favorites.SetFavoriteAsync(track.Id, favorite: false);
        Assert.False(await _favorites.IsFavoriteAsync(track.Id));
    }

    private async Task<Track> SeedTrackAsync(string title, string artist)
    {
        var path = Path.Combine(_root, $"{title}-{Guid.NewGuid():N}.mp3");
        await File.WriteAllTextAsync(path, "fake");
        var track = new Track
        {
            FilePath = path,
            Title = title,
            Artist = artist,
            AddedAt = DateTime.UtcNow
        };
        track.Id = await _tracks.UpsertAsync(track);
        return track;
    }
}
