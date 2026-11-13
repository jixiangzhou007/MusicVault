using MusicVault.Core.Models;

namespace MusicVault.App.Services;

public static class DiscoverTrackBatchSlicer
{
    public static IReadOnlyList<OnlineTrack> Slice(
        IReadOnlyList<OnlineTrack> pool,
        int take,
        int offset,
        IReadOnlyCollection<string>? excludeIds)
    {
        if (pool.Count == 0)
            return [];

        take = Math.Clamp(take, 1, 20);
        offset = Math.Max(0, offset);

        var exclude = excludeIds is { Count: > 0 }
            ? new HashSet<string>(excludeIds, StringComparer.Ordinal)
            : null;

        var filtered = exclude == null
            ? pool.ToList()
            : pool.Where(t => string.IsNullOrEmpty(t.Id) || !exclude.Contains(t.Id)).ToList();

        if (filtered.Count == 0)
            filtered = pool.ToList();

        var start = offset % filtered.Count;
        var batch = new List<OnlineTrack>(take);
        for (var i = 0; i < take; i++)
            batch.Add(filtered[(start + i) % filtered.Count]);
        return batch;
    }
}
