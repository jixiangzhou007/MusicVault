using System.Text.Json;
using System.Text.Json.Serialization;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;

namespace MusicVault.Services.Library;

/// <summary>NAS 曲目播放缓存：边播边复制到本地，下次优先读缓存。</summary>
public sealed class NasTrackCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _cacheDir;
    private readonly string _indexPath;
    private readonly WebDavClient? _webDav;
    private readonly WebDavLibraryService? _webDavLibraries;
    private readonly object _lock = new();
    private readonly Dictionary<long, NasPlaybackCacheEntry> _entriesByTrackId = new();
    private readonly HashSet<long> _copyInFlight = new();

    public event EventHandler? Changed;

    public NasTrackCacheService(
        string dataDirectory,
        WebDavClient? webDav = null,
        WebDavLibraryService? webDavLibraries = null)
    {
        _webDav = webDav;
        _webDavLibraries = webDavLibraries;
        _cacheDir = Path.Combine(dataDirectory, "nas-playback-cache");
        _indexPath = Path.Combine(dataDirectory, "nas-playback-cache-index.json");
        Directory.CreateDirectory(_cacheDir);
        LoadIndex();
    }

    public bool IsCached(Track track) => TryGetCachedPath(track) != null;

    public NasPlaybackCacheStats GetStats()
    {
        lock (_lock)
        {
            long totalBytes = 0;
            foreach (var entry in _entriesByTrackId.Values)
            {
                try
                {
                    if (File.Exists(entry.CachePath))
                        totalBytes += new FileInfo(entry.CachePath).Length;
                }
                catch
                {
                    // ignore unreadable cache file
                }
            }

            return new NasPlaybackCacheStats(_entriesByTrackId.Count, totalBytes);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            foreach (var entry in _entriesByTrackId.Values)
            {
                try
                {
                    if (File.Exists(entry.CachePath))
                        File.Delete(entry.CachePath);
                }
                catch
                {
                    // ignore delete failure
                }
            }

            _entriesByTrackId.Clear();
            _copyInFlight.Clear();

            try
            {
                if (File.Exists(_indexPath))
                    File.Delete(_indexPath);
            }
            catch
            {
                // ignore index delete failure
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string? TryGetCachedPath(Track track)
    {
        if (track.Id <= 0 || string.IsNullOrWhiteSpace(track.FilePath))
            return null;

        lock (_lock)
        {
            if (!_entriesByTrackId.TryGetValue(track.Id, out var entry))
                return null;

            if (!string.Equals(entry.SourcePath, track.FilePath, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(entry.CachePath))
                return null;

            if (!SourceMatches(entry, track.FilePath))
                return null;

            return entry.CachePath;
        }
    }

    public void StartBackgroundCache(Track track)
    {
        if (track.Id <= 0 || !IsRemoteStreamingPath(track.FilePath))
            return;

        if (TryGetCachedPath(track) != null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureCachedAsync(track, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 后台缓存失败不影响当前播放
            }
        });
    }

    public async Task<string?> EnsureCachedAsync(Track track, CancellationToken ct = default)
    {
        var existing = TryGetCachedPath(track);
        if (existing != null)
            return existing;

        if (track.Id > 0)
        {
            while (true)
            {
                var acquireCopy = false;
                lock (_lock)
                {
                    if (!_copyInFlight.Contains(track.Id))
                    {
                        _copyInFlight.Add(track.Id);
                        acquireCopy = true;
                    }
                }

                if (acquireCopy)
                {
                    try
                    {
                        return await CopyToCacheAsync(track, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        lock (_lock)
                            _copyInFlight.Remove(track.Id);
                    }
                }

                var waited = await WaitForInFlightCacheAsync(track, ct).ConfigureAwait(false);
                if (waited != null)
                    return waited;

                existing = TryGetCachedPath(track);
                if (existing != null)
                    return existing;
            }
        }

        return await CopyToCacheAsync(track, ct).ConfigureAwait(false);
    }

    private async Task<string?> WaitForInFlightCacheAsync(Track track, CancellationToken ct)
    {
        for (var i = 0; i < 600; i++)
        {
            ct.ThrowIfCancellationRequested();

            var cached = TryGetCachedPath(track);
            if (cached != null)
                return cached;

            var inFlight = false;
            lock (_lock)
                inFlight = track.Id > 0 && _copyInFlight.Contains(track.Id);

            if (!inFlight)
                return null;

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return TryGetCachedPath(track);
    }

    private async Task<string?> CopyToCacheAsync(Track track, CancellationToken ct)
    {
        var existing = TryGetCachedPath(track);
        if (existing != null)
            return existing;

        var ext = Path.GetExtension(track.FilePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".mp3";

        var cachePath = Path.Combine(_cacheDir, $"{track.Id}{ext.ToLowerInvariant()}");
        var tempPath = cachePath + ".part";
        Directory.CreateDirectory(_cacheDir);

        long sourceLength;
        long sourceTicks;

        if (WebDavPathHelper.IsWebDavPath(track.FilePath))
        {
            if (_webDav == null || _webDavLibraries == null || track.LibraryId is not > 0)
                return null;

            var endpoint = await _webDavLibraries.ResolveStoredEndpointAsync(track.LibraryId.Value, ct)
                .ConfigureAwait(false);
            if (endpoint == null)
                return null;

            await _webDav.CopyToFileAsync(endpoint, track.FilePath, tempPath, ct).ConfigureAwait(false);
            sourceLength = track.FileSizeBytes > 0 ? track.FileSizeBytes : new FileInfo(tempPath).Length;
            sourceTicks = 0;
        }
        else
        {
            if (!File.Exists(track.FilePath))
                return null;

            await using (var src = File.OpenRead(track.FilePath))
            await using (var dst = File.Create(tempPath))
                await src.CopyToAsync(dst, ct).ConfigureAwait(false);

            var info = new FileInfo(track.FilePath);
            sourceLength = info.Length;
            sourceTicks = info.LastWriteTimeUtc.Ticks;
        }

        if (File.Exists(cachePath))
            File.Delete(cachePath);
        File.Move(tempPath, cachePath);

        var entry = new NasPlaybackCacheEntry
        {
            TrackId = track.Id,
            SourcePath = track.FilePath,
            CachePath = cachePath,
            SourceLength = sourceLength,
            SourceWriteTimeUtcTicks = sourceTicks,
            CachedAtUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            _entriesByTrackId[track.Id] = entry;
            SaveIndexLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return cachePath;
    }

    private static bool SourceMatches(NasPlaybackCacheEntry entry, string sourcePath)
    {
        if (WebDavPathHelper.IsWebDavPath(sourcePath))
            return true;

        try
        {
            var info = new FileInfo(sourcePath);
            return info.Length == entry.SourceLength
                   && info.LastWriteTimeUtc.Ticks == entry.SourceWriteTimeUtcTicks;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRemoteStreamingPath(string? path) =>
        NasPathHelper.IsUncPath(path) || WebDavPathHelper.IsWebDavPath(path);

    private void LoadIndex()
    {
        if (!File.Exists(_indexPath))
            return;

        try
        {
            var json = File.ReadAllText(_indexPath);
            var index = JsonSerializer.Deserialize<NasPlaybackCacheIndex>(json, JsonOptions);
            if (index?.Entries == null)
                return;

            foreach (var entry in index.Entries)
            {
                if (entry.TrackId > 0 && File.Exists(entry.CachePath))
                    _entriesByTrackId[entry.TrackId] = entry;
            }
        }
        catch
        {
            // ignore corrupt index
        }
    }

    private void SaveIndexLocked()
    {
        var index = new NasPlaybackCacheIndex
        {
            Entries = _entriesByTrackId.Values.OrderBy(e => e.TrackId).ToList()
        };
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index, JsonOptions));
    }

    private sealed class NasPlaybackCacheIndex
    {
        public List<NasPlaybackCacheEntry> Entries { get; set; } = [];
    }

    private sealed class NasPlaybackCacheEntry
    {
        public long TrackId { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string CachePath { get; set; } = string.Empty;
        public long SourceLength { get; set; }
        public long SourceWriteTimeUtcTicks { get; set; }
        public DateTime CachedAtUtc { get; set; }
    }
}

public sealed record NasPlaybackCacheStats(int EntryCount, long TotalBytes);
