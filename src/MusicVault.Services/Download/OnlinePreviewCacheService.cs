using System.Text.Json;
using System.Text.Json.Serialization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Http;

namespace MusicVault.Services.Download;

/// <summary>在线试听缓存：持久化音频文件与最近试听记录，避免重复下载。</summary>
public sealed class OnlinePreviewCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const int MaxRecentListens = 120;
    private readonly string _cacheDir;
    private readonly string _indexPath;
    private readonly object _lock = new();
    private OnlinePreviewCacheIndex _index = new();

    public event EventHandler? Changed;

    public OnlinePreviewCacheService(string dataDirectory)
    {
        _cacheDir = Path.Combine(dataDirectory, "online-preview-cache");
        _indexPath = Path.Combine(dataDirectory, "online-preview-index.json");
        Directory.CreateDirectory(_cacheDir);
        LoadIndex();
    }

    public bool IsCached(OnlineTrack track) => TryGetBestCachedPath(track, null) != null;

    public string? TryGetCachedPath(OnlineTrack track, AudioQuality quality)
    {
        lock (_lock)
        {
            var entry = FindEntry(track.Platform, track.Id, quality);
            return entry != null && File.Exists(entry.FilePath) ? entry.FilePath : null;
        }
    }

    public string? TryGetBestCachedPath(OnlineTrack track, IEnumerable<AudioQuality>? preferredOrder = null)
    {
        var order = preferredOrder?.ToList();
        if (order is { Count: > 0 })
        {
            foreach (var q in order)
            {
                var path = TryGetCachedPath(track, q);
                if (path != null)
                    return path;
            }
        }

        lock (_lock)
        {
            var best = _index.Entries
                .Where(e => e.Platform == track.Platform
                            && string.Equals(e.Id, track.Id, StringComparison.Ordinal)
                            && File.Exists(e.FilePath))
                .OrderByDescending(e => e.Quality)
                .FirstOrDefault();
            return best?.FilePath;
        }
    }

    public IReadOnlyList<OnlineTrack> GetRecentListenTracks(int max = 80)
    {
        lock (_lock)
        {
            return _index.RecentListens
                .OrderByDescending(r => r.LastPlayedAtUtcTicks)
                .Take(max)
                .Select(r => CloneTrack(r.Track))
                .ToList();
        }
    }

    public async Task<(string Path, bool FromCache)> EnsureCachedAsync(
        OnlineTrack track,
        AudioQuality quality,
        Func<Task<string?>> resolveUrl,
        CancellationToken ct = default,
        IProgress<double>? downloadProgress = null)
    {
        var existing = TryGetCachedPath(track, quality);
        if (existing != null)
            return (existing, true);

        var url = await resolveUrl().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(ServiceMessageCodec.Key("Preview.NoPlayLink"));

        var targetPath = BuildCacheFilePath(track, quality);
        var tempPath = targetPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        using var response = await HttpClients.Download
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;

        await using (var net = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var fs = File.Create(tempPath))
        {
            var buffer = new byte[81920];
            long read = 0;
            int count;
            while ((count = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
                read += count;
                if (totalBytes is > 0)
                    downloadProgress?.Report(read / (double)totalBytes);
            }
        }

        downloadProgress?.Report(1);

        if (File.Exists(targetPath))
        {
            try { File.Delete(targetPath); } catch { /* ignore */ }
        }

        File.Move(tempPath, targetPath);

        lock (_lock)
        {
            UpsertEntry(track, quality, targetPath);
            SaveIndexLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return (targetPath, false);
    }

    public void RecordListen(OnlineTrack track, AudioQuality quality, string filePath)
    {
        lock (_lock)
        {
            UpsertEntry(track, quality, filePath);
            var key = TrackKey(track.Platform, track.Id);
            _index.RecentListens.RemoveAll(r => TrackKey(r.Track.Platform, r.Track.Id) == key);
            _index.RecentListens.Insert(0, new OnlinePreviewListenRecord
            {
                Track = CloneTrack(track),
                Quality = (int)quality,
                FilePath = filePath,
                LastPlayedAtUtcTicks = DateTime.UtcNow.Ticks
            });
            if (_index.RecentListens.Count > MaxRecentListens)
                _index.RecentListens.RemoveRange(MaxRecentListens, _index.RecentListens.Count - MaxRecentListens);
            SaveIndexLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>删除指定曲目的全部试听缓存文件与索引项；试听记录条目保留。</summary>
    public bool ClearCache(OnlineTrack track)
    {
        var removedAny = false;
        lock (_lock)
        {
            removedAny = RemoveCacheEntriesLocked(track);

            foreach (var listen in _index.RecentListens)
            {
                if (listen.Track.Platform != track.Platform
                    || !string.Equals(listen.Track.Id, track.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(listen.FilePath)
                    && !File.Exists(listen.FilePath))
                {
                    listen.FilePath = string.Empty;
                }
            }

            if (removedAny)
                SaveIndexLocked();
        }

        if (removedAny)
            Changed?.Invoke(this, EventArgs.Empty);

        return removedAny;
    }

    /// <summary>从试听记录移除指定曲目，并删除其全部试听缓存。</summary>
    public bool RemoveFromListenHistory(OnlineTrack track)
    {
        var changed = false;
        lock (_lock)
        {
            var key = TrackKey(track.Platform, track.Id);
            foreach (var listen in _index.RecentListens.Where(r => TrackKey(r.Track.Platform, r.Track.Id) == key))
                TryDeleteFile(listen.FilePath);

            changed |= RemoveCacheEntriesLocked(track);

            if (_index.RecentListens.RemoveAll(r => TrackKey(r.Track.Platform, r.Track.Id) == key) > 0)
                changed = true;

            if (changed)
                SaveIndexLocked();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);

        return changed;
    }

    private bool RemoveCacheEntriesLocked(OnlineTrack track)
    {
        var removedAny = false;
        var entries = _index.Entries
            .Where(e => e.Platform == track.Platform
                        && string.Equals(e.Id, track.Id, StringComparison.Ordinal))
            .ToList();
        foreach (var entry in entries)
        {
            TryDeleteFile(entry.FilePath);
            _index.Entries.Remove(entry);
            removedAny = true;
        }

        return removedAny;
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    private void LoadIndex()
    {
        if (!File.Exists(_indexPath))
            return;

        try
        {
            var json = File.ReadAllText(_indexPath);
            var loaded = JsonSerializer.Deserialize<OnlinePreviewCacheIndex>(json, JsonOptions);
            if (loaded == null)
                return;

            loaded.Entries = loaded.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.FilePath) && File.Exists(e.FilePath))
                .ToList();
            loaded.RecentListens = loaded.RecentListens
                .Where(r => !string.IsNullOrWhiteSpace(r.FilePath) && File.Exists(r.FilePath))
                .ToList();
            _index = loaded;
        }
        catch
        {
            _index = new OnlinePreviewCacheIndex();
        }
    }

    private void SaveIndexLocked()
    {
        var json = JsonSerializer.Serialize(_index, JsonOptions);
        File.WriteAllText(_indexPath, json);
    }

    private OnlinePreviewCacheEntry? FindEntry(MusicPlatform platform, string id, AudioQuality quality)
    {
        return _index.Entries.FirstOrDefault(e =>
            e.Platform == platform
            && string.Equals(e.Id, id, StringComparison.Ordinal)
            && e.Quality == (int)quality);
    }

    private void UpsertEntry(OnlineTrack track, AudioQuality quality, string filePath)
    {
        var entry = FindEntry(track.Platform, track.Id, quality);
        if (entry == null)
        {
            entry = new OnlinePreviewCacheEntry
            {
                Platform = track.Platform,
                Id = track.Id
            };
            _index.Entries.Add(entry);
        }

        entry.Quality = (int)quality;
        entry.FilePath = filePath;
        entry.Title = track.Title;
        entry.Artist = track.Artist;
        entry.CachedAtUtcTicks = DateTime.UtcNow.Ticks;
    }

    private string BuildCacheFilePath(OnlineTrack track, AudioQuality quality)
    {
        var safeId = SanitizeFileToken(track.Id);
        var fileName = $"{(int)track.Platform}_{safeId}_{(int)quality}.mp3";
        return Path.Combine(_cacheDir, fileName);
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var text = new string(chars);
        return text.Length > 80 ? text[..80] : text;
    }

    private static string TrackKey(MusicPlatform platform, string id) =>
        $"{(int)platform}:{id}";

    private static OnlineTrack CloneTrack(OnlineTrack source) => new()
    {
        Id = source.Id,
        Platform = source.Platform,
        Title = source.Title,
        Artist = source.Artist,
        ArtistId = source.ArtistId,
        AlbumArtist = source.AlbumArtist,
        Album = source.Album,
        AlbumId = source.AlbumId,
        Composer = source.Composer,
        Year = source.Year,
        Genre = source.Genre,
        TrackNumber = source.TrackNumber,
        DiscNumber = source.DiscNumber,
        DurationMs = source.DurationMs,
        SizeBytes = source.SizeBytes,
        BitrateKbps = source.BitrateKbps,
        CoverUrl = source.CoverUrl,
        PreviewUrl = source.PreviewUrl,
        DirectDownloadUrl = source.DirectDownloadUrl,
        HasFlac = source.HasFlac,
        SupportsMp3128 = source.SupportsMp3128,
        SupportsMp3320 = source.SupportsMp3320,
        QuarkShareUrl = source.QuarkShareUrl,
        DownloadVariants = source.DownloadVariants.ToList()
    };

    private sealed class OnlinePreviewCacheIndex
    {
        public List<OnlinePreviewCacheEntry> Entries { get; set; } = [];
        public List<OnlinePreviewListenRecord> RecentListens { get; set; } = [];
    }

    private sealed class OnlinePreviewCacheEntry
    {
        public MusicPlatform Platform { get; set; }
        public string Id { get; set; } = string.Empty;
        public int Quality { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public long CachedAtUtcTicks { get; set; }
    }

    private sealed class OnlinePreviewListenRecord
    {
        public OnlineTrack Track { get; set; } = new();
        public int Quality { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long LastPlayedAtUtcTicks { get; set; }
    }
}
