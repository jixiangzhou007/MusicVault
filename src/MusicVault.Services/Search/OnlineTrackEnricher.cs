using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Providers;

namespace MusicVault.Services.Search;

/// <summary>组合标签确认前：拉取平台曲目详情并尝试用统一 API 补全年份、专辑等字段。</summary>
public static class OnlineTrackEnricher
{
    public static async Task<OnlineTrack> EnrichAsync(
        MusicSearchAggregator search,
        OnlineTrack track,
        CancellationToken ct = default)
    {
        var merged = track;
        OnlineTrackQuality.InferMissingFields(merged);

        if (!string.IsNullOrWhiteSpace(track.Id))
        {
            var provider = search.GetProvider(track.Platform);
            if (provider != null)
            {
                try
                {
                    var detail = await provider.GetTrackAsync(track.Id, ct).ConfigureAwait(false);
                    merged = OnlineTrackMerger.Merge(merged, detail);
                }
                catch
                {
                    // 详情接口失败时仍使用搜索摘要
                }
            }

            if (merged.Platform == MusicPlatform.NetEase && provider is NetEaseMusicProvider netEase)
                merged = await netEase.TryEnrichFromAlbumAsync(merged, ct).ConfigureAwait(false);
            else if (merged.Platform == MusicPlatform.QQMusic && provider is QQMusicProvider qq)
                merged = await qq.TryEnrichFromAlbumAsync(merged, ct).ConfigureAwait(false);
        }

        if (merged.Year is not > 0
            || merged.TrackNumber is not > 0
            || merged.DiscNumber is not > 0
            || string.IsNullOrWhiteSpace(merged.Genre)
            || string.IsNullOrWhiteSpace(merged.Composer)
            || (string.IsNullOrWhiteSpace(merged.AlbumId) && !string.IsNullOrWhiteSpace(merged.Album)))
        {
            merged = await GoMusicApiAlbumEnricher.TryEnrichFromAlbumDetailAsync(merged, ct)
                .ConfigureAwait(false);
        }

        if ((NeedsSupplement(merged) || string.IsNullOrWhiteSpace(merged.Composer))
            && GoMusicApiClient.IsConfigured
            && merged.Platform is not (MusicPlatform.MyFreeMp3 or MusicPlatform.Jbsou or MusicPlatform.All))
        {
            try
            {
                var query = BuildRefineQuery(merged);
                var (candidates, _) = await GoMusicApiClient.SearchAsync(query, merged.Platform, ct)
                    .ConfigureAwait(false);
                var best = PickBestCandidate(merged, candidates);
                if (best != null)
                    merged = OnlineTrackMerger.Merge(merged, best);
            }
            catch
            {
                // ignore
            }
        }

        if (string.IsNullOrWhiteSpace(merged.Title)
            && !string.IsNullOrWhiteSpace(merged.Artist)
            && GoMusicApiClient.IsConfigured)
        {
            try
            {
                var switched = await GoMusicApiClient.TrySwitchSourceAsync(merged, ct).ConfigureAwait(false);
                if (switched != null)
                    merged = OnlineTrackMerger.Merge(merged, switched);
            }
            catch
            {
                // ignore
            }
        }

        OnlineTrackQuality.InferMissingFields(merged);
        return merged;
    }

    private static bool NeedsSupplement(OnlineTrack t) =>
        string.IsNullOrWhiteSpace(t.Album)
        || t.Year is not > 0
        || string.IsNullOrWhiteSpace(t.Genre)
        || string.IsNullOrWhiteSpace(t.Composer)
        || t.TrackNumber is not > 0
        || t.DiscNumber is not > 0
        || string.IsNullOrWhiteSpace(t.CoverUrl);

    private static string BuildRefineQuery(OnlineTrack t)
    {
        if (!string.IsNullOrWhiteSpace(t.Artist) && !string.IsNullOrWhiteSpace(t.Title))
            return $"{t.Artist.Trim()} {t.Title.Trim()}";
        return !string.IsNullOrWhiteSpace(t.Title) ? t.Title.Trim() : t.Artist.Trim();
    }

    private static OnlineTrack? PickBestCandidate(OnlineTrack seed, IReadOnlyList<OnlineTrack> candidates)
    {
        if (candidates.Count == 0)
            return null;

        var titleKey = Normalize(seed.Title);
        var artistKey = Normalize(seed.Artist);

        OnlineTrack? best = null;
        var bestScore = int.MinValue;
        foreach (var c in candidates)
        {
            var score = 0;
            if (!string.IsNullOrEmpty(titleKey))
            {
                var ct = Normalize(c.Title);
                if (ct == titleKey) score += 80;
                else if (ct.Contains(titleKey, StringComparison.Ordinal) || titleKey.Contains(ct, StringComparison.Ordinal))
                    score += 40;
            }

            if (!string.IsNullOrEmpty(artistKey))
            {
                var ca = Normalize(c.Artist);
                if (ca == artistKey) score += 60;
                else if (ca.Contains(artistKey, StringComparison.Ordinal) || artistKey.Contains(ca, StringComparison.Ordinal))
                    score += 30;
            }

            if (c.Platform == seed.Platform)
                score += 15;
            if (!string.IsNullOrWhiteSpace(c.Album))
                score += 8;
            if (c.Year is > 0)
                score += 6;
            if (!string.IsNullOrWhiteSpace(c.Genre))
                score += 4;
            if (!string.IsNullOrWhiteSpace(c.Composer))
                score += 3;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
