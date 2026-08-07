using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Download;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class DownloadHistoryPersistenceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mv-hist-" + Guid.NewGuid().ToString("N"));
    private DownloadHistoryRepository _history = null!;
    private DownloadQueueService _downloads = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        var downloadDir = Path.Combine(_root, "Downloads");
        var dbPath = Path.Combine(_root, "musicvault.db");
        await DatabaseInitializer.InitializeAsync(dbPath);

        _history = new DownloadHistoryRepository(dbPath, _root);
        var tracks = new TrackRepository(dbPath);
        var libraries = new LibraryRepository(dbPath);
        var metadata = new TagLibMetadataService();
        var scanner = TestLibraryScanFactory.Create(tracks, libraries, metadata);
        var libraryManager = new LibraryManagerService(libraries, tracks, scanner);

        _downloads = new DownloadQueueService(
            [],
            metadata,
            tracks,
            libraries,
            libraryManager,
            downloadDir,
            history: _history);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ClearQueue_KeepsLedgerRecords()
    {
        await SeedAndLoadCompletedItemAsync();

        _downloads.ClearQueue();
        await _downloads.FlushHistoryAsync();

        var ledger = await _downloads.GetAllHistoryItemsAsync();
        Assert.Single(ledger);
        Assert.Equal(DownloadStatus.Completed, ledger[0].Status);
        Assert.Empty(_downloads.Items);
    }

    [Fact]
    public async Task ClearCompleted_KeepsLedgerRecords()
    {
        await SeedAndLoadCompletedItemAsync();

        _downloads.ClearCompleted();
        await _downloads.FlushHistoryAsync();

        var ledger = await _downloads.GetAllHistoryItemsAsync();
        Assert.Single(ledger);
        Assert.Empty(_downloads.Items);
    }

    [Fact]
    public async Task ClearAllHistory_RemovesLedgerRecords()
    {
        await SeedAndLoadCompletedItemAsync();

        await _downloads.ClearAllHistoryAsync();

        Assert.Empty(await _downloads.GetAllHistoryItemsAsync());
        Assert.Equal(0, await _history.CountAsync());
    }

    private async Task SeedAndLoadCompletedItemAsync()
    {
        var item = new DownloadItem
        {
            Track = new OnlineTrack
            {
                Id = "track-1",
                Platform = MusicPlatform.NetEase,
                Title = "Song",
                Artist = "Artist"
            },
            Quality = AudioQuality.High320,
            Status = DownloadStatus.Completed,
            TargetPath = Path.Combine(_root, "Downloads", "Artist - Song.mp3"),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            FinishedAtUtc = DateTime.UtcNow
        };

        await _history.SaveNowAsync([item]);
        await _downloads.LoadHistoryAsync();
        Assert.Single(_downloads.Items);
    }
}
