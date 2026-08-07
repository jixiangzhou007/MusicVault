using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.Metadata;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Metadata;
using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels.Sidebar;

public partial class MetadataEditorViewModel
{
    private AppModalHostViewModel? _modalHost;
    private FormDrawerHostViewModel? _formDrawerHost;

    public void BindModalHost(AppModalHostViewModel modalHost) => _modalHost = modalHost;

    public void BindFormDrawerHost(FormDrawerHostViewModel formDrawerHost) =>
        _formDrawerHost = formDrawerHost;

    [RelayCommand]
    private Task IdentifyOnlineAsync()
    {
        if (!IsOnlineSourceMode) return Task.CompletedTask;
        return IdentifyAsync();
    }

    [RelayCommand]
    private async Task FetchCoverOnlineAsync()
    {
        if (!IsOnlineSourceMode || Track == null) return;

        IsBusy = true;
        try
        {
            var primary = LastOnlineMatch ?? BuildEditorFallbackTrack();
            var candidates = await BuildMediaCandidatesAsync(primary);
            await FetchCoverPreviewFromCandidatesAsync(candidates, primary);
            StatusMessage = HasCover ? L10n.Get("Metadata.Status.CoverFetched") : L10n.Get("Metadata.Status.CoverNotFound");
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.CoverFetchFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FetchLyricsOnlineAsync()
    {
        if (!IsOnlineSourceMode || Track == null) return;

        IsBusy = true;
        try
        {
            var primary = LastOnlineMatch ?? BuildEditorFallbackTrack();
            var candidates = await BuildMediaCandidatesAsync(primary);
            var filled = await TryFillLyricsFromMatchAsync(primary, candidates);
            StatusMessage = filled ? L10n.Get("Metadata.Status.LyricsFetched") : L10n.Get("Metadata.Status.LyricsNotFound");
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.LyricsFetchFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task IdentifyAsync()
    {
        if (Track == null) return;
        if (_formDrawerHost == null)
        {
            StatusMessage = L10n.Get("Metadata.Status.SearchLayerFailed");
            return;
        }

        var query = BuildSearchQuery();
        if (string.IsNullOrWhiteSpace(query))
        {
            await RunOnUiAsync(() =>
                StatusMessage = L10n.Get("Metadata.Status.NeedTitleOrArtist"));
            return;
        }

        await RunOnUiAsync(() =>
        {
            IsBusy = true;
            StatusMessage = L10n.Get("Metadata.Status.Searching");
        });

        try
        {
            var platform = MusicPlatformCatalog.FromIndex(MatchPlatformIndex);
            var searchTitle = Title.Trim();
            var searchArtist = Artist.Trim();
            var searchAlbum = Album.Trim();
            var ranked = await SearchRankedMatchesAsync(query, searchTitle, searchArtist, searchAlbum, platform);

            if (ranked.Count == 0)
            {
                await RunOnUiAsync(() =>
                    StatusMessage = platform == MusicPlatform.All
                        ? L10n.Get("Metadata.Status.SearchNoResult")
                        : string.Format(
                            L10n.Get("Metadata.Status.SearchPlatformNoResult"),
                            L10n.GetPlatformLabel(platform)));
                return;
            }

            var header = BuildSearchHeaderText();
            var pickerVm = new CombinedLabelsSearchViewModel(_formDrawerHost, L10n, header, ranked, FetchMediaOnIdentify);
            pickerVm.StartBackgroundEnrichment(Services.Search);

            await RunOnUiAsync(() => StatusMessage = string.Empty);
            var selected = await _formDrawerHost.ShowCombinedLabelsSearchAsync(pickerVm);
            if (selected == null)
            {
                await RunOnUiAsync(() => StatusMessage = L10n.Get("Metadata.Status.SearchCancelled"));
                return;
            }

            FetchMediaOnIdentify = pickerVm.FetchMedia;
            await ApplySelectedMatchToEditorAsync(selected, enrichFromApi: true);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => StatusMessage = ServiceMessageLocalizer.Format(L10n, "Metadata.Status.SearchFailed", ex.Message));
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false);
        }
    }

    private async Task<List<(OnlineTrack Track, int Score)>> SearchRankedMatchesAsync(
        string primaryQuery,
        string title,
        string artist,
        string album,
        MusicPlatform platform)
    {
        var ranked = await CollectRankedMatchesAsync(primaryQuery, title, artist, album, platform);

        if (platform != MusicPlatform.All && ShouldFallbackToAllPlatforms(ranked))
        {
            var allRanked = await CollectRankedMatchesAsync(primaryQuery, title, artist, album, MusicPlatform.All);
            ranked = MergeRankedMatches(ranked, allRanked);
        }

        return ranked;
    }

    private async Task<List<(OnlineTrack Track, int Score)>> CollectRankedMatchesAsync(
        string primaryQuery,
        string title,
        string artist,
        string album,
        MusicPlatform platform)
    {
        var merged = await MetadataSearchCollector.CollectTracksAsync(
            Services.Search,
            BuildSearchQueries(primaryQuery, title, artist, album),
            platform);

        var localDurationMs = Track?.DurationMs ?? 0;
        return MetadataMatchRanker.BuildRankedMatches(
            merged, title, artist, platform, album, localDurationMs: localDurationMs);
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

    private string BuildSearchHeaderText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Title))
            parts.Add(Title.Trim());
        if (!string.IsNullOrWhiteSpace(Artist))
            parts.Add(Artist.Trim());
        if (!string.IsNullOrWhiteSpace(Album))
            parts.Add(Album.Trim());
        return parts.Count > 0 ? string.Join(" | ", parts) : BuildSearchQuery();
    }

    /// <summary>用户于组合标签搜索中确认后，仅回填侧栏编辑区，不写入磁盘。</summary>
    private async Task ApplySelectedMatchToEditorAsync(OnlineTrack selected, bool enrichFromApi = false)
    {
        var track = selected;
        if (enrichFromApi)
        {
            await RunOnUiAsync(() => StatusMessage = L10n.Get("Metadata.Status.FetchingTrackDetail"));
            track = await OnlineTrackEnricher.EnrichAsync(Services.Search, selected);
        }

        await RunOnUiAsync(() => ApplyOnlineMatch(track));

        if (FetchMediaOnIdentify)
        {
            _ = FetchCoverAndLyricsInBackgroundAsync(track);
        }

        await RunOnUiAsync(() =>
            StatusMessage = string.Format(
                L10n.Get("Metadata.Status.ResultApplied"),
                L10n.GetPlatformLabel(track.Platform),
                track.Title.Trim()));
    }

    /// <summary>用编辑器中的歌手 + 歌名，按所选数据源搜索。</summary>
    private string BuildSearchQuery()
    {
        var title = Title.Trim();
        var artist = Artist.Trim();
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            return $"{artist} {title}";
        if (!string.IsNullOrWhiteSpace(title))
            return title;
        if (!string.IsNullOrWhiteSpace(artist))
            return artist;

        var name = Path.GetFileNameWithoutExtension(Track?.FilePath ?? string.Empty);
        return name.Trim();
    }

    /// <summary>用在线匹配结果覆盖编辑区字段（空值则清空，便于与文件原标签区分）。</summary>
    private void ApplyOnlineMatch(OnlineTrack best)
    {
        LastOnlineMatch = best;

        Title = best.Title?.Trim() ?? string.Empty;
        Artist = best.Artist?.Trim() ?? string.Empty;
        Album = best.Album?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(best.AlbumArtist))
            AlbumArtist = best.AlbumArtist.Trim();
        else if (!string.IsNullOrWhiteSpace(best.Artist))
            AlbumArtist = best.Artist.Trim();
        else
            AlbumArtist = string.Empty;

        Composer = best.Composer?.Trim() ?? string.Empty;
        YearText = best.Year is > 0 ? best.Year.Value.ToString() : string.Empty;

        var genre = MetadataGenreHeuristic.Sanitize(best.Genre?.Trim(), best.Title?.Trim());
        Genre = genre;
        if (!string.IsNullOrWhiteSpace(genre))
            EnsureGenreOption(genre);

        TrackNumberText = best.TrackNumber is > 0 ? best.TrackNumber.Value.ToString() : string.Empty;
        DiscNumberText = best.DiscNumber is > 0 ? best.DiscNumber.Value.ToString() : string.Empty;
    }

    private async Task FetchCoverAndLyricsInBackgroundAsync(OnlineTrack track)
    {
        var generation = LoadGeneration;

        await RunOnUiAsync(() =>
        {
            if (LoadGeneration != generation) return;
            SetCoverImage(null);
            LyricsText = string.Empty;
        });

        if (LoadGeneration != generation) return;

        var candidates = await BuildMediaCandidatesAsync(track);
        if (LoadGeneration != generation) return;

        await FetchCoverPreviewFromCandidatesAsync(candidates, track, generation);
        if (LoadGeneration != generation) return;

        await TryFillLyricsFromMatchAsync(track, candidates, generation);
    }

    private async Task<List<OnlineTrack>> BuildMediaCandidatesAsync(OnlineTrack primary)
    {
        var list = new List<OnlineTrack> { primary };

        if (GoMusicApiClient.IsConfigured
            && OnlineTrackQuality.IsMetadataRichPlatform(primary.Platform)
            && !string.IsNullOrWhiteSpace(primary.Title))
        {
            try
            {
                var switched = await GoMusicApiClient.TrySwitchSourceAsync(primary);
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

    private OnlineTrack BuildEditorFallbackTrack() =>
        new()
        {
            Title = Title.Trim(),
            Artist = Artist.Trim(),
            Album = Album.Trim(),
            DurationMs = Track?.DurationMs ?? 0
        };

    /// <summary>仅更新编辑区封面预览，写入文件需用户点封面右侧保存按钮。</summary>
    private async Task FetchCoverPreviewFromCandidatesAsync(
        IReadOnlyList<OnlineTrack> candidates,
        OnlineTrack primary,
        int? generation = null)
    {
        foreach (var candidate in candidates)
        {
            if (generation.HasValue && LoadGeneration != generation.Value) return;

            var cover = await OnlineCoverFetcher.DownloadBytesAsync(candidate);
            if (cover is { Length: > 0 })
            {
                await RunOnUiAsync(() =>
                {
                    if (generation.HasValue && LoadGeneration != generation.Value) return;
                    SetCoverImage(cover, "image/jpeg");
                });
                return;
            }
        }

        var fallbackArtist = !string.IsNullOrWhiteSpace(primary.Artist) ? primary.Artist : Artist.Trim();
        var fallbackAlbum = !string.IsNullOrWhiteSpace(primary.Album) ? primary.Album : Album.Trim();
        if (string.IsNullOrWhiteSpace(fallbackArtist) || string.IsNullOrWhiteSpace(fallbackAlbum))
            return;

        if (generation.HasValue && LoadGeneration != generation.Value) return;

        var fallbackCover = await Services.Metadata.DownloadCoverAsync(fallbackArtist, fallbackAlbum, 600);
        if (fallbackCover is not { Length: > 0 }) return;

        await RunOnUiAsync(() =>
        {
            if (generation.HasValue && LoadGeneration != generation.Value) return;
            SetCoverImage(fallbackCover, "image/jpeg");
        });
    }

    private async Task<bool> TryFillLyricsFromMatchAsync(
        OnlineTrack primary,
        IReadOnlyList<OnlineTrack> candidates,
        int? generation = null)
    {
        if (Track == null) return false;

        var lyricArtist = !string.IsNullOrWhiteSpace(primary.Artist) ? primary.Artist : Artist.Trim();
        var lyricTitle = !string.IsNullOrWhiteSpace(primary.Title) ? primary.Title : Title.Trim();
        if (string.IsNullOrWhiteSpace(lyricArtist) || string.IsNullOrWhiteSpace(lyricTitle))
            return false;

        if (generation.HasValue && LoadGeneration != generation.Value) return false;

        var durationMs = Track.DurationMs > 0 ? Track.DurationMs : primary.DurationMs;
        var lrc = await OnlineLyricsFetcher.DownloadAsync(
            candidates,
            lyricArtist,
            lyricTitle,
            durationMs > 0 ? durationMs : null);

        if (string.IsNullOrWhiteSpace(lrc)) return false;

        await RunOnUiAsync(() =>
        {
            if (generation.HasValue && LoadGeneration != generation.Value) return;
            LyricsText = lrc;
        });
        return true;
    }
}
