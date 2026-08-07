using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Search;

/// <summary>组合标签 / 批量刮削：并行多关键词搜索并去重合并。</summary>
public static class MetadataSearchCollector
{
    public const int DefaultMergeCap = 80;

    public static async Task<List<OnlineTrack>> CollectTracksAsync(
        MusicSearchAggregator search,
        IEnumerable<string> queries,
        MusicPlatform platform,
        int mergeCap = DefaultMergeCap,
        CancellationToken ct = default)
    {
        var queryList = queries as IReadOnlyList<string> ?? queries.ToList();
        if (queryList.Count == 0)
            return [];

        var tasks = queryList
            .Select(q => search.SearchForMetadataDetailedAsync(q, platform, ct))
            .ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var merged = new List<OnlineTrack>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            foreach (var track in result.Tracks)
            {
                var key = $"{track.Platform}|{track.Id}";
                if (!seen.Add(key))
                    continue;

                merged.Add(track);
                if (merged.Count >= mergeCap)
                    return merged;
            }
        }

        return merged;
    }
}
