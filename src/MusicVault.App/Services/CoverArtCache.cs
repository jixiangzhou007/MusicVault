using System.Collections.Concurrent;
using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>曲目封面双尺寸缓存：内存 + 磁盘；大图（正在播放/全屏）、小图（列表/底部栏）。</summary>
public static class CoverArtCache
{
    public const int ThumbEdge = 120;
    public const int LargeEdge = 600;

    private static readonly ConcurrentDictionary<string, CoverArtEntry> Entries = new();
    private static CoverArtDiskCache? _disk;

    public sealed record CoverArtEntry(byte[]? Thumb, byte[]? Large);

    public static void Initialize(string dataDirectory) =>
        _disk = new CoverArtDiskCache(dataDirectory);

    public static string BuildKey(Track track)
    {
        if (!string.IsNullOrWhiteSpace(track.FilePath))
            return $"path:{track.FilePath}";
        return $"meta:{track.Artist}|{track.Album}";
    }

    public static string BuildUrlKey(string url) => $"url:{url.Trim()}";

    public static bool TryGet(string key, out CoverArtEntry entry)
    {
        if (Entries.TryGetValue(key, out entry!))
            return entry.Thumb is { Length: > 0 } || entry.Large is { Length: > 0 };

        if (_disk != null && _disk.TryGet(key, out var thumb, out var large)
            && (thumb is { Length: > 0 } || large is { Length: > 0 }))
        {
            entry = new CoverArtEntry(thumb, large);
            Entries[key] = entry;
            return true;
        }

        entry = new CoverArtEntry(null, null);
        return false;
    }

    public static void Set(string key, byte[]? thumb, byte[]? large)
    {
        var normalizedThumb = thumb is { Length: > 0 } ? thumb : null;
        byte[]? normalizedLarge = large is { Length: > 0 } ? large : null;
        if (normalizedLarge == null && normalizedThumb is { Length: > 0 })
            normalizedLarge = normalizedThumb;

        Entries[key] = new CoverArtEntry(normalizedThumb, normalizedLarge);
        _disk?.Set(key, normalizedThumb, normalizedLarge);
    }

    public static void InvalidateAlbum(string artist, string album)
    {
        var key = AlbumCoverLoader.BuildAlbumKey(artist, album);
        Entries.TryRemove(key, out _);
        _disk?.Remove(key);
    }

    public static void InvalidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var key = BuildUrlKey(url);
        Entries.TryRemove(key, out _);
        _disk?.Remove(key);
    }
}
