using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services.Api;
using MusicVault.Services.Search;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>批量在线刮削：搜索匹配、补全元数据并写入文件。</summary>
public sealed class BatchOnlineScrapeService
{
    private const int MinMatchScore = 75;
    private const int MediaCandidateCount = 8;

    private readonly MusicSearchAggregator _search;
    private readonly IMetadataService _metadata;
    private readonly TrackRepository _tracks;

    public BatchOnlineScrapeService(
        MusicSearchAggregator search,
        IMetadataService metadata,
        TrackRepository tracks)
    {
        _search = search;
        _metadata = metadata;
        _tracks = tracks;
    }

    public async Task<BatchScrapeTrackResult> ScrapeOneAsync(
        Track track,
        MusicPlatform platform,
        BatchScrapeOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
            return Fail("Scrape.FileNotFound");

        await HydrateFromFileIfNeededAsync(track, ct).ConfigureAwait(false);

        var title = track.Title.Trim();
        var artist = track.Artist.Trim();
        var album = track.Album.Trim();
        var query = BuildSearchQuery(title, artist, track.FilePath);
        if (string.IsNullOrWhiteSpace(query))
            return Fail("Scrape.MissingTitleArtist");

        var ranked = await FindBestMatchesAsync(query, title, artist, album, platform, track.DurationMs, ct)
            .ConfigureAwait(false);
        if (ranked.Count == 0)
            return Fail("Scrape.NoOnlineMatch");

        ranked = await OnlineTrackBatchEnricher.EnrichMatchesAsync(_search, ranked, maxCount: MediaCandidateCount, ct)
            .ConfigureAwait(false);
        if (ranked.Count == 0)
            return Fail("Scrape.IncompleteMetadata");

        var (best, score) = ranked[0];
        if (score < MinMatchScore)
            return Fail("Scrape.LowMatchScoreDetail", score);

        var enriched = best;
        var tagsWritten = false;
        var coverEmbedded = false;
        var lyricsEmbedded = false;

        if (options.UpdatesAnyTag)
        {
            ApplyOnlineTags(track, enriched, options);
            await _metadata.WriteTagsAsync(track, ct).ConfigureAwait(false);

            if (options.UpdateComposer && !string.IsNullOrWhiteSpace(enriched.Composer))
            {
                var (composer, comment, disc, lyricist) = await _metadata.ReadExtendedTagsAsync(track.FilePath, ct)
                    .ConfigureAwait(false);
                await _metadata.WriteExtendedTagsAsync(
                    track.FilePath,
                    enriched.Composer.Trim(),
                    comment,
                    disc,
                    lyricist,
                    ct).ConfigureAwait(false);
            }

            tagsWritten = true;
        }

        if (options.EmbedCover || options.EmbedLyrics)
        {
            var mediaCandidates = await BuildMediaCandidatesAsync(
                enriched,
                ranked.Select(x => x.Track).ToList(),
                ct).ConfigureAwait(false);

            if (options.EmbedCover)
                coverEmbedded = await TryEmbedCoverAsync(track, mediaCandidates, ct).ConfigureAwait(false);

            if (options.EmbedLyrics)
                lyricsEmbedded = await TryEmbedLyricsAsync(track, enriched, mediaCandidates, ct).ConfigureAwait(false);
        }

        await _tracks.UpsertAsync(track, ct, validateArtistNames: false).ConfigureAwait(false);

        var parts = new List<string>();
        if (tagsWritten) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Tags"));
        if (coverEmbedded) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Cover"));
        if (lyricsEmbedded) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Lyrics"));

        if (parts.Count == 0)
        {
            return new BatchScrapeTrackResult
            {
                Success = false,
                Message = BatchScrapeMessageFormatter.FormatPartialFailure(options, tagsWritten, coverEmbedded, lyricsEmbedded),
                TagsWritten = tagsWritten,
                CoverEmbedded = coverEmbedded,
                LyricsEmbedded = lyricsEmbedded
            };
        }

        var allRequestedDone =
            (!options.UpdatesAnyTag || tagsWritten)
            && (!options.EmbedCover || coverEmbedded)
            && (!options.EmbedLyrics || lyricsEmbedded);

        return new BatchScrapeTrackResult
        {
            Success = allRequestedDone,
            Message = allRequestedDone
                ? BatchScrapeMessageFormatter.FormatSuccessSummary(tagsWritten, coverEmbedded, lyricsEmbedded)
                : BatchScrapeMessageFormatter.FormatPartialFailure(options, tagsWritten, coverEmbedded, lyricsEmbedded),
            TagsWritten = tagsWritten,
            CoverEmbedded = coverEmbedded,
            LyricsEmbedded = lyricsEmbedded
        };
    }

    private static async Task<List<OnlineTrack>> BuildMediaCandidatesAsync(
        OnlineTrack primary,
        IReadOnlyList<OnlineTrack> candidates,
        CancellationToken ct)
    {
        var list = OrderMediaCandidates(primary, candidates).ToList();

        if (GoMusicApiClient.IsConfigured
            && OnlineTrackQuality.IsMetadataRichPlatform(primary.Platform)
            && !string.IsNullOrWhiteSpace(primary.Title))
        {
            try
            {
                var switched = await GoMusicApiClient.TrySwitchSourceAsync(primary, ct).ConfigureAwait(false);
                if (switched != null)
                {
                    var key = $"{switched.Platform}|{switched.Id}";
                    if (list.All(t => $"{t.Platform}|{t.Id}" != key))
                        list.Add(switched);
                }
            }
            catch
            {
                // ignore switch failures
            }
        }

        return list;
    }

    private async Task<bool> TryEmbedCoverAsync(
        Track track,
        IReadOnlyList<OnlineTrack> candidates,
        CancellationToken ct)
    {
        foreach (var candidate in candidates)
        {
            var cover = await OnlineCoverFetcher.DownloadBytesAsync(candidate, ct).ConfigureAwait(false);
            if (cover is { Length: > 0 })
            {
                await _metadata.EmbedCoverAsync(track.FilePath, cover, ct).ConfigureAwait(false);
                return true;
            }
        }

        var primary = candidates[0];
        var fallbackArtist = !string.IsNullOrWhiteSpace(primary.Artist) ? primary.Artist : track.Artist;
        var fallbackAlbum = !string.IsNullOrWhiteSpace(primary.Album) ? primary.Album : track.Album;
        if (!string.IsNullOrWhiteSpace(fallbackArtist) && !string.IsNullOrWhiteSpace(fallbackAlbum))
        {
            var cover = await _metadata.DownloadCoverAsync(fallbackArtist, fallbackAlbum, 600, ct)
                .ConfigureAwait(false);
            if (cover is { Length: > 0 })
            {
                await _metadata.EmbedCoverAsync(track.FilePath, cover, ct).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryEmbedLyricsAsync(
        Track track,
        OnlineTrack primary,
        IReadOnlyList<OnlineTrack> candidates,
        CancellationToken ct)
    {
        var lyricArtist = !string.IsNullOrWhiteSpace(primary.Artist) ? primary.Artist : track.Artist;
        var lyricTitle = !string.IsNullOrWhiteSpace(primary.Title) ? primary.Title : track.Title;
        var durationMs = track.DurationMs > 0 ? track.DurationMs : primary.DurationMs;

        var lrc = await OnlineLyricsFetcher.DownloadAsync(
            candidates,
            lyricArtist,
            lyricTitle,
            durationMs > 0 ? durationMs : null,
            ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(lrc))
            return false;

        await _metadata.EmbedLyricsAsync(track.FilePath, lrc, ct).ConfigureAwait(false);
        track.LyricsPath = Path.ChangeExtension(track.FilePath, ".lrc");
        return true;
    }

    private static IEnumerable<OnlineTrack> OrderMediaCandidates(OnlineTrack primary, IReadOnlyList<OnlineTrack> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        yield return primary;

        foreach (var track in candidates
                     .Where(t => !string.IsNullOrWhiteSpace(t.Id))
                     .OrderByDescending(t => ReferenceEquals(t, primary))
                     .ThenByDescending(t => OnlineTrackQuality.IsMetadataRichPlatform(t.Platform))
                     .ThenByDescending(t => !string.IsNullOrWhiteSpace(t.CoverUrl))
                     .ThenByDescending(t => OnlineTrackQuality.CompletenessScore(t)))
        {
            var key = $"{track.Platform}|{track.Id}";
            if (seen.Add(key))
                yield return track;
        }
    }

    public static void ApplyOnlineTags(Track track, OnlineTrack online, BatchScrapeOptions options)
    {
        if (options.UpdateTitle && !string.IsNullOrWhiteSpace(online.Title))
            track.Title = online.Title.Trim();

        if (options.UpdateArtist && !string.IsNullOrWhiteSpace(online.Artist))
            track.Artist = online.Artist.Trim();

        if (options.UpdateAlbum && !string.IsNullOrWhiteSpace(online.Album))
            track.Album = online.Album.Trim();

        if (options.UpdateAlbumArtist)
        {
            if (!string.IsNullOrWhiteSpace(online.AlbumArtist))
                track.AlbumArtist = online.AlbumArtist.Trim();
            else if (!string.IsNullOrWhiteSpace(online.Artist))
                track.AlbumArtist = online.Artist.Trim();
        }

        if (options.UpdateYear && online.Year is > 0)
            track.Year = online.Year;

        if (options.UpdateGenre && !string.IsNullOrWhiteSpace(online.Genre))
            track.Genre = online.Genre.Trim();

        if (options.UpdateTrackNumber && online.TrackNumber is > 0)
            track.TrackNumber = online.TrackNumber;
    }

    public static string BuildSearchQuery(string title, string artist, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            return $"{artist.Trim()} {title.Trim()}";
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();
        if (!string.IsNullOrWhiteSpace(artist))
            return artist.Trim();

        var name = Path.GetFileNameWithoutExtension(filePath);
        return name.Trim();
    }

    private async Task<List<(OnlineTrack Track, int Score)>> FindBestMatchesAsync(
        string primaryQuery,
        string title,
        string artist,
        string album,
        MusicPlatform platform,
        int localDurationMs,
        CancellationToken ct)
    {
        var ranked = await CollectRankedMatchesAsync(
                primaryQuery, title, artist, album, platform, localDurationMs, ct)
            .ConfigureAwait(false);

        if (platform != MusicPlatform.All && ShouldFallbackToAllPlatforms(ranked))
        {
            var allRanked = await CollectRankedMatchesAsync(
                    primaryQuery, title, artist, album, MusicPlatform.All, localDurationMs, ct)
                .ConfigureAwait(false);
            ranked = MergeRankedMatches(ranked, allRanked);
        }

        return ranked;
    }

    private async Task<List<(OnlineTrack Track, int Score)>> CollectRankedMatchesAsync(
        string primaryQuery,
        string title,
        string artist,
        string album,
        MusicPlatform platform,
        int localDurationMs,
        CancellationToken ct)
    {
        var merged = await MetadataSearchCollector.CollectTracksAsync(
            _search,
            BuildSearchQueries(primaryQuery, title, artist, album),
            platform,
            ct: ct).ConfigureAwait(false);

        return OnlineMetadataMatcher.BuildRankedMatches(
            merged, title, artist, platform, album, localDurationMs: localDurationMs);
    }

    private static IEnumerable<string> BuildSearchQueries(string primaryQuery, string title, string artist, string album)
    {
        yield return primaryQuery;

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
        {
            yield return $"{artist} - {title}";
            yield return $"{title} {artist}";
            yield return title;
            if (!string.IsNullOrWhiteSpace(album))
                yield return $"{artist} {title} {album}".Trim();
        }
        else if (!string.IsNullOrWhiteSpace(title))
        {
            yield return title;
        }
        else if (!string.IsNullOrWhiteSpace(artist))
        {
            yield return artist;
        }
    }

    private static bool ShouldFallbackToAllPlatforms(List<(OnlineTrack Track, int Score)> ranked)
    {
        if (ranked.Count < 8)
            return true;

        var topMatch = ranked.Count > 0 ? ranked[0].Score : 0;
        return topMatch < 120;
    }

    private static List<(OnlineTrack Track, int Score)> MergeRankedMatches(
        List<(OnlineTrack Track, int Score)> primary,
        List<(OnlineTrack Track, int Score)> secondary)
    {
        var map = new Dictionary<string, (OnlineTrack Track, int Score)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in primary.Concat(secondary))
        {
            var key = $"{item.Track.Platform}|{item.Track.Id}";
            if (!map.TryGetValue(key, out var existing)
                || item.Score > existing.Score
                || (item.Score == existing.Score
                    && OnlineTrackQuality.CompletenessScore(item.Track)
                    > OnlineTrackQuality.CompletenessScore(existing.Track)))
            {
                map[key] = item;
            }
        }

        return map.Values
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => OnlineTrackQuality.CompletenessScore(x.Track))
            .ToList();
    }

    private async Task HydrateFromFileIfNeededAsync(Track track, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(track.Title)
            && !string.IsNullOrWhiteSpace(track.Artist)
            && !string.IsNullOrWhiteSpace(track.Album))
        {
            return;
        }

        try
        {
            var fromFile = await _metadata.ReadTagsAsync(track.FilePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(track.Title) && !string.IsNullOrWhiteSpace(fromFile.Title))
                track.Title = fromFile.Title;
            if (string.IsNullOrWhiteSpace(track.Artist) && !string.IsNullOrWhiteSpace(fromFile.Artist))
                track.Artist = fromFile.Artist;
            if (string.IsNullOrWhiteSpace(track.Album) && !string.IsNullOrWhiteSpace(fromFile.Album))
                track.Album = fromFile.Album;
            if (string.IsNullOrWhiteSpace(track.AlbumArtist) && !string.IsNullOrWhiteSpace(fromFile.AlbumArtist))
                track.AlbumArtist = fromFile.AlbumArtist;
            if (!track.Year.HasValue && fromFile.Year is > 0)
                track.Year = fromFile.Year;
            if (string.IsNullOrWhiteSpace(track.Genre) && !string.IsNullOrWhiteSpace(fromFile.Genre))
                track.Genre = fromFile.Genre;
            if (!track.TrackNumber.HasValue && fromFile.TrackNumber is > 0)
                track.TrackNumber = fromFile.TrackNumber;
            if (track.DurationMs <= 0 && fromFile.DurationMs > 0)
                track.DurationMs = fromFile.DurationMs;
        }
        catch
        {
            // 读取失败时仍用库内字段继续尝试搜索
        }
    }

    private static BatchScrapeTrackResult Fail(string messageKey, params object?[] args) =>
        new()
        {
            Success = false,
            Message = args.Length > 0
                ? ServiceMessageCodec.Format(messageKey, args)
                : ServiceMessageCodec.Key(messageKey)
        };
}
