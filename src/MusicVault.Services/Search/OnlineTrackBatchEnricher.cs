using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;

namespace MusicVault.Services.Search;

/// <summary>组合标签弹窗打开前：并行补全曲目详情，避免列表只有歌名/歌手。</summary>
public static class OnlineTrackBatchEnricher
{
    public static async Task<List<(OnlineTrack Track, int Score)>> EnrichMatchesAsync(
        MusicSearchAggregator search,
        IReadOnlyList<(OnlineTrack Track, int Score)> matches,
        int maxCount = 48,
        CancellationToken ct = default)
    {
        if (matches.Count == 0)
            return [];

        var slice = matches.Take(maxCount).ToArray();
        var bag = new (OnlineTrack Track, int Score)[slice.Length];
        using var gate = new SemaphoreSlim(6);

        var tasks = Enumerable.Range(0, slice.Length).Select(async i =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var enriched = await OnlineTrackEnricher.EnrichAsync(search, slice[i].Track, ct)
                    .ConfigureAwait(false);
                bag[i] = (enriched, slice[i].Score);
            }
            catch
            {
                bag[i] = slice[i];
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await SupplementMissingYearsAsync(bag, ct).ConfigureAwait(false);

        return bag
            .Where(x => OnlineTrackQuality.IsViableMetadataCandidate(x.Track))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => OnlineTrackQuality.CompletenessScore(x.Track))
            .ToList();
    }

    /// <summary>后台补全年份/风格等专辑级字段（不阻塞弹窗打开）。</summary>
    public static async Task SupplementAlbumYearsForRowsAsync(
        IReadOnlyList<OnlineTrack> tracks,
        CancellationToken ct = default)
    {
        if (tracks.Count == 0 || !GoMusicApiClient.IsConfigured)
            return;

        var bag = tracks.Select(t => (Track: t, Score: 0)).ToArray();
        await SupplementMissingYearsAsync(bag, ct).ConfigureAwait(false);
    }

    private static async Task SupplementMissingYearsAsync(
        (OnlineTrack Track, int Score)[] bag,
        CancellationToken ct)
    {
        if (!GoMusicApiClient.IsConfigured)
            return;

        var groups = bag
            .Where(x => x.Track.Year is not > 0 && OnlineTrackQuality.IsMetadataRichPlatform(x.Track.Platform))
            .GroupBy(x => (
                x.Track.Platform,
                Key: !string.IsNullOrWhiteSpace(x.Track.AlbumId) ? x.Track.AlbumId! : x.Track.Album.Trim()))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key.Key))
            .Take(20)
            .ToList();

        foreach (var group in groups)
        {
            var seed = group.First().Track;
            var enriched = await GoMusicApiAlbumEnricher.TryEnrichFromAlbumDetailAsync(seed, ct)
                .ConfigureAwait(false);
            if (enriched.Year is not > 0 && enriched.TrackNumber is not > 0
                && string.IsNullOrWhiteSpace(enriched.Genre))
                continue;

            foreach (var item in group)
            {
                if (enriched.Year is > 0 && item.Track.Year is not > 0)
                    item.Track.Year = enriched.Year;
                if (!string.IsNullOrWhiteSpace(enriched.Genre) && string.IsNullOrWhiteSpace(item.Track.Genre))
                    item.Track.Genre = enriched.Genre;
                if (string.IsNullOrWhiteSpace(item.Track.CoverUrl) && !string.IsNullOrWhiteSpace(enriched.CoverUrl))
                    item.Track.CoverUrl = enriched.CoverUrl;
                if (string.IsNullOrWhiteSpace(item.Track.AlbumId) && !string.IsNullOrWhiteSpace(enriched.AlbumId))
                    item.Track.AlbumId = enriched.AlbumId;

                if (item.Track.TrackNumber is not > 0)
                {
                    var copy = item.Track;
                    await GoMusicApiAlbumEnricher.TryEnrichFromAlbumDetailAsync(copy, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
