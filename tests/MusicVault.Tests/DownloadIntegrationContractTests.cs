using System.Net;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Application;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Library;

namespace MusicVault.Tests;

public sealed class DownloadIntegrationContractTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mv-download-contract-" + Guid.NewGuid().ToString("N"));
    private string _dbPath = null!;
    private DownloadQueueService _downloads = null!;
    private DownloadApplicationService _downloadApp = null!;
    private LocalAudioServer _server = null!;
    private string? _savedMusicApiBaseUrl;
    private string? _savedMusicApiEnv;

    public async Task InitializeAsync()
    {
        _savedMusicApiBaseUrl = MusicApiConfig.BaseUrl;
        _savedMusicApiEnv = Environment.GetEnvironmentVariable("MUSICVAULT_MUSIC_API");
        MusicApiConfig.BaseUrl = null;
        Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", null);

        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "musicvault.db");
        await DatabaseInitializer.InitializeAsync(_dbPath);

        var tracks = new TrackRepository(_dbPath);
        var libraries = new LibraryRepository(_dbPath);
        var metadata = new NoopMetadataService();
        var scanner = TestLibraryScanFactory.Create(tracks, libraries, metadata);
        var manager = new LibraryManagerService(libraries, tracks, scanner);
        var history = new DownloadHistoryRepository(_dbPath, _root);

        _server = await LocalAudioServer.StartAsync();

        _downloads = new DownloadQueueService(
            [new FixedUrlProvider(_server.AudioUrl)],
            metadata,
            tracks,
            libraries,
            manager,
            Path.Combine(_root, "Downloads"),
            history: history);

        _downloadApp = new DownloadApplicationService(_downloads);
    }

    public async Task DisposeAsync()
    {
        MusicApiConfig.BaseUrl = _savedMusicApiBaseUrl;
        Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", _savedMusicApiEnv);

        await _server.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task DownloadIntegration_CompleteAndIndexTrack()
    {
        var request = new DownloadEnqueueRequest(
            new OnlineTrack
            {
                Id = "download-success",
                Platform = MusicPlatform.NetEase,
                Title = "Contract Song",
                Artist = "Contract Artist"
            },
            AudioQuality.High320);

        var enqueue = _downloadApp.Enqueue(request);
        Assert.True(enqueue.Succeeded);
        Assert.True(enqueue.Data!.Accepted);

        var completed = await WaitForStatusAsync(DownloadStatus.Completed, TimeSpan.FromSeconds(12));
        Assert.Equal(DownloadStatus.Completed, completed.Status);

        var tracks = new TrackRepository(_dbPath);
        var stored = await tracks.GetByPathAsync(completed.TargetPath);
        Assert.NotNull(stored);
        Assert.Contains("Contract Song", stored!.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadIntegration_FailureRetriesAndEndsFailed()
    {
        var tracks = new TrackRepository(_dbPath);
        var libraries = new LibraryRepository(_dbPath);
        var metadata = new NoopMetadataService();
        var scanner = TestLibraryScanFactory.Create(tracks, libraries, metadata);
        var manager = new LibraryManagerService(libraries, tracks, scanner);
        var history = new DownloadHistoryRepository(_dbPath, _root);

        var failingQueue = new DownloadQueueService(
            [new ThrowingProvider()],
            metadata,
            tracks,
            libraries,
            manager,
            Path.Combine(_root, "DownloadsFail"),
            history: history);
        var app = new DownloadApplicationService(failingQueue);

        var enqueue = app.Enqueue(new DownloadEnqueueRequest(
            new OnlineTrack
            {
                Id = "download-fail",
                Platform = MusicPlatform.NetEase,
                Title = "Fail Song",
                Artist = "Fail Artist"
            },
            AudioQuality.High320));
        Assert.True(enqueue.Succeeded);
        Assert.True(enqueue.Data!.Accepted);

        var failed = await WaitForStatusAsync(failingQueue, DownloadStatus.Failed, TimeSpan.FromSeconds(15));
        Assert.Equal(DownloadStatus.Failed, failed.Status);
        Assert.Equal(3, failed.RetryCount);
    }

    private Task<DownloadItem> WaitForStatusAsync(DownloadStatus status, TimeSpan timeout) =>
        WaitForStatusAsync(_downloads, status, timeout);

    private static async Task<DownloadItem> WaitForStatusAsync(
        DownloadQueueService downloads,
        DownloadStatus status,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var item = downloads.Items.FirstOrDefault();
            if (item?.Status == status)
                return item;

            await Task.Delay(100);
        }

        var snapshot = string.Join(", ", downloads.Items.Select(i => $"{i.Id}:{i.Status}:{i.RetryCount}"));
        throw new TimeoutException($"等待状态 {status} 超时。当前队列: {snapshot}");
    }

    private sealed class FixedUrlProvider(string url) : IMusicSourceProvider
    {
        public MusicPlatform Platform => MusicPlatform.NetEase;
        public Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default) => Task.FromResult<OnlineTrack?>(null);
        public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct) => Task.FromResult(url);
        public Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public bool TryParseLink(string input, out string resourceType, out string resourceId)
        {
            resourceType = string.Empty;
            resourceId = string.Empty;
            return false;
        }
    }

    private sealed class ThrowingProvider : IMusicSourceProvider
    {
        public MusicPlatform Platform => MusicPlatform.NetEase;
        public Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default) => Task.FromResult<OnlineTrack?>(null);
        public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);
        public Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct) =>
            throw new InvalidOperationException("provider unavailable");
        public Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public bool TryParseLink(string input, out string resourceType, out string resourceId)
        {
            resourceType = string.Empty;
            resourceId = string.Empty;
            return false;
        }
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

    private sealed class LocalAudioServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly Task _loopTask;

        private LocalAudioServer(HttpListener listener, CancellationTokenSource cts, Task loopTask, string audioUrl)
        {
            _listener = listener;
            _cts = cts;
            _loopTask = loopTask;
            AudioUrl = audioUrl;
        }

        public string AudioUrl { get; }

        public static Task<LocalAudioServer> StartAsync()
        {
            var port = GetRandomPort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            var audioBytes = new byte[40 * 1024];
            new Random(42).NextBytes(audioBytes);

            var cts = new CancellationTokenSource();
            var loop = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    HttpListenerContext? ctx = null;
                    try
                    {
                        ctx = await listener.GetContextAsync().WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        break;
                    }

                    if (ctx == null) continue;
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "audio/mpeg";
                    ctx.Response.ContentLength64 = audioBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(audioBytes, cts.Token);
                    ctx.Response.OutputStream.Close();
                }
            }, cts.Token);

            return Task.FromResult(new LocalAudioServer(listener, cts, loop, $"{prefix}audio.mp3"));
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            try { await _loopTask; } catch { /* ignore */ }
            _cts.Dispose();
        }

        private static int GetRandomPort()
        {
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
