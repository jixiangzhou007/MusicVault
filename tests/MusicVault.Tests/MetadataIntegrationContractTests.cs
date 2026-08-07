using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Application;
using MusicVault.Services.Features;

namespace MusicVault.Tests;

public sealed class MetadataIntegrationContractTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mv-metadata-contract-" + Guid.NewGuid().ToString("N"));
    private string _dbPath = null!;
    private MetadataApplicationService _metadataApp = null!;
    private TrackRepository _tracks = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "musicvault.db");
        await DatabaseInitializer.InitializeAsync(_dbPath);

        _tracks = new TrackRepository(_dbPath);
        var persistence = new TrackMetadataPersistenceService(new NoopMetadataService(), _tracks);
        _metadataApp = new MetadataApplicationService(persistence);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MetadataIntegration_SaveTrackMetadata_PersistsToDatabase()
    {
        var path = Path.Combine(_root, "metadata-contract.mp3");
        await File.WriteAllBytesAsync(path, new byte[64 * 1024]);

        var track = new Track
        {
            FilePath = path,
            Title = "Old Title",
            Artist = "Old Artist",
            Album = "Old Album",
            AddedAt = DateTime.UtcNow
        };

        var fields = new TrackMetadataEditorFields(
            Title: "New Title",
            Artist: "New Artist",
            Album: "New Album",
            AlbumArtist: "New Album Artist",
            TrackNumberText: "3",
            YearText: "2026",
            Genre: "Pop",
            Composer: "Composer",
            Comment: "Comment",
            DiscNumberText: "1",
            Lyricist: "Lyricist",
            LyricsText: "lyrics",
            CoverImage: null);

        var result = await _metadataApp.SaveTrackMetadataAsync(track, fields);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Data);

        var stored = await _tracks.GetByIdAsync(result.Data!.Id);
        Assert.NotNull(stored);
        Assert.Equal("New Title", stored!.Title);
        Assert.Equal("New Artist", stored.Artist);
        Assert.Equal("New Album", stored.Album);
    }

    private sealed class NoopMetadataService : IMetadataService
    {
        public Task<Track> ReadTagsAsync(string filePath, CancellationToken ct = default) => Task.FromResult(new Track { FilePath = filePath });
        public Task WriteTagsAsync(Track track, CancellationToken ct = default) => Task.CompletedTask;
        public Task EmbedCoverAsync(string filePath, byte[] imageData, CancellationToken ct = default) => Task.CompletedTask;
        public Task EmbedLyricsAsync(string filePath, string lrcContent, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearLyricsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> LookupByFingerprintAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<byte[]?> DownloadCoverAsync(string artist, string album, int size = 600, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<string?> DownloadLyricsAsync(string artist, string title, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<byte[]?> ReadEmbeddedCoverAsync(string filePath, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<(byte[] Data, string MimeType, string PictureTypeLabel)?> ReadEmbeddedCoverDetailsAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult<(byte[] Data, string MimeType, string PictureTypeLabel)?>(null);
        public Task ClearCoverAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(string Composer, string Comment, string DiscNumberText, string Lyricist)> ReadExtendedTagsAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult((string.Empty, string.Empty, string.Empty, string.Empty));
        public Task WriteExtendedTagsAsync(string filePath, string composer, string comment, string? discNumberText, string lyricist, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<string> ReadLyricsAsync(string filePath, CancellationToken ct = default) => Task.FromResult(string.Empty);
    }
}
