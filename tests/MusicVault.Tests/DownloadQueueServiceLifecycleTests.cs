using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Download;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class DownloadQueueServiceLifecycleTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mv-dl-" + Guid.NewGuid().ToString("N"));
    private readonly StallDownloadProvider _provider = new();
    private DownloadQueueService _downloads = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        var downloadDir = Path.Combine(_root, "Downloads");
        var dbPath = Path.Combine(_root, "musicvault.db");
        await DatabaseInitializer.InitializeAsync(dbPath);

        var tracks = new TrackRepository(dbPath);
        var libraries = new LibraryRepository(dbPath);
        var metadata = new TagLibMetadataService();
        var scanner = TestLibraryScanFactory.Create(tracks, libraries, metadata);
        var libraryManager = new LibraryManagerService(libraries, tracks, scanner);
        var history = new DownloadHistoryRepository(dbPath, _root);

        _downloads = new DownloadQueueService(
            [_provider],
            metadata,
            tracks,
            libraries,
            libraryManager,
            downloadDir,
            history: history);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pause_Resume_Cancel_WhileUrlResolveIsPending()
    {
        var track = new OnlineTrack
        {
            Id = "bench-1",
            Platform = MusicPlatform.NetEase,
            Title = "Bench",
            Artist = "Artist"
        };

        Assert.True(_downloads.Enqueue(track, AudioQuality.High320));
        var item = await WaitForItemStatusAsync(DownloadStatus.Downloading, TimeSpan.FromSeconds(8));

        _downloads.Pause(item.Id);
        Assert.Equal(DownloadStatus.Paused, GetItem(item.Id).Status);

        _downloads.Resume(item.Id);
        await WaitForItemStatusAsync(DownloadStatus.Downloading, TimeSpan.FromSeconds(8), item.Id);

        _downloads.Cancel(item.Id);
        await WaitForItemStatusAsync(DownloadStatus.Cancelled, TimeSpan.FromSeconds(8), item.Id);
    }

    private DownloadItem GetItem(Guid id) =>
        _downloads.Items.First(i => i.Id == id);

    private async Task<DownloadItem> WaitForItemStatusAsync(
        DownloadStatus status,
        TimeSpan timeout,
        Guid? id = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var item = id.HasValue
                ? _downloads.Items.FirstOrDefault(i => i.Id == id.Value)
                : _downloads.Items.FirstOrDefault();
            if (item?.Status == status)
                return item;

            await Task.Delay(50);
        }

        var snapshot = string.Join(", ", _downloads.Items.Select(i => $"{i.Id}:{i.Status}"));
        throw new TimeoutException($"Timed out waiting for {status}. Items: {snapshot}");
    }

    private sealed class StallDownloadProvider : IMusicSourceProvider
    {
        public MusicPlatform Platform => MusicPlatform.NetEase;

        public Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

        public Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default) =>
            Task.FromResult<OnlineTrack?>(null);

        public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

        public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

        public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

        public async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            return "http://127.0.0.1/unreachable";
        }

        public Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public bool TryParseLink(string url, out string resourceType, out string resourceId)
        {
            resourceType = string.Empty;
            resourceId = string.Empty;
            return false;
        }
    }
}
