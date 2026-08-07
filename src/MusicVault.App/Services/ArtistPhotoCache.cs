using System.Collections.Concurrent;
using MusicVault.App.Services;

namespace MusicVault.App.Services;

/// <summary>艺术家头像内存缓存（列表缩略图）。</summary>
public static class ArtistPhotoCache
{
    public const int ListThumbEdge = 96;

    private static readonly ConcurrentDictionary<string, byte[]?> Entries = new(StringComparer.Ordinal);

    public static bool TryGet(string artistName, out byte[]? bytes) =>
        Entries.TryGetValue(artistName, out bytes);

    public static void Set(string artistName, byte[]? bytes) =>
        Entries[artistName] = bytes;

    public static void Invalidate(string artistName) => Entries.TryRemove(artistName, out _);

    public static byte[]? GetOrLoad(string artistName, byte[]? raw, bool forceReload = false)
    {
        if (!forceReload && TryGet(artistName, out var cached))
            return cached;

        byte[]? thumb = null;
        if (raw is { Length: > 0 })
            thumb = CoverImageScaler.ScaleToMaxEdge(raw, ListThumbEdge);

        Set(artistName, thumb);
        return thumb;
    }
}
