using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Search;

/// <summary>多源歌词拉取：匹配曲目 → 跨平台搜索 → LRCLIB。</summary>
public static class OnlineLyricsFetcher
{
    private static readonly LyricsFetchService LrcLib = new();

    public static async Task<string?> DownloadAsync(
        IReadOnlyList<OnlineTrack> candidates,
        string artist,
        string title,
        int? durationMs = null,
        CancellationToken ct = default)
    {
        foreach (var track in OrderCandidates(candidates))
        {
            ct.ThrowIfCancellationRequested();
            if (GoMusicApiClient.IsConfigured && OnlineTrackQuality.IsMetadataRichPlatform(track.Platform))
            {
                var lrc = await GoMusicApiClient.FetchLyricsAsync(track, ct).ConfigureAwait(false);
                if (GoMusicApiClient.IsUsableLyrics(lrc))
                    return lrc;
            }
        }

        if (GoMusicApiClient.IsConfigured)
        {
            var searched = await GoMusicApiClient.FetchLyricsBySearchAsync(artist, title, durationMs, ct)
                .ConfigureAwait(false);
            if (GoMusicApiClient.IsUsableLyrics(searched))
                return searched;
        }

        return await LrcLib.DownloadAsync(artist, title, ct).ConfigureAwait(false);
    }

    private static IEnumerable<OnlineTrack> OrderCandidates(IReadOnlyList<OnlineTrack> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in candidates
                     .Where(t => !string.IsNullOrWhiteSpace(t.Id))
                     .OrderByDescending(t => OnlineTrackQuality.IsMetadataRichPlatform(t.Platform))
                     .ThenByDescending(t => OnlineTrackQuality.CompletenessScore(t)))
        {
            var key = $"{track.Platform}|{track.Id}";
            if (seen.Add(key))
                yield return track;
        }
    }
}
