using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>发现页 Hero/为你精选曲目池会话缓存（进程内 TTL）。</summary>
public enum DiscoverHeroPromoPoolKind
{
    NewSongs = 0,
    HotSongs = 1,
    GuessYouLike = 2
}

public static class DiscoverHeroPromoPoolCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    private static readonly object Gate = new();
    private static readonly Dictionary<DiscoverHeroPromoPoolKind, Entry> Entries = new();

    public static void Invalidate()
    {
        lock (Gate)
            Entries.Clear();
    }

    public static bool TryGet(
        DiscoverHeroPromoPoolKind kind,
        out IReadOnlyList<OnlineTrack> tracks,
        TimeSpan? ttl = null)
    {
        var limit = ttl ?? DefaultTtl;
        lock (Gate)
        {
            if (!Entries.TryGetValue(kind, out var entry)
                || DateTime.UtcNow - entry.CachedAtUtc > limit
                || entry.Tracks.Count == 0)
            {
                tracks = [];
                return false;
            }

            tracks = entry.Tracks;
            return true;
        }
    }

    public static void Set(DiscoverHeroPromoPoolKind kind, IReadOnlyList<OnlineTrack> tracks)
    {
        if (tracks.Count == 0)
            return;

        lock (Gate)
        {
            Entries[kind] = new Entry(tracks.ToList(), DateTime.UtcNow);
        }
    }

    /// <summary>单测：写入指定时间戳的缓存条目。</summary>
    public static void SetForTests(
        DiscoverHeroPromoPoolKind kind,
        IReadOnlyList<OnlineTrack> tracks,
        DateTime cachedAtUtc)
    {
        lock (Gate)
        {
            Entries[kind] = new Entry(tracks.ToList(), cachedAtUtc);
        }
    }

    private sealed record Entry(IReadOnlyList<OnlineTrack> Tracks, DateTime CachedAtUtc);
}
