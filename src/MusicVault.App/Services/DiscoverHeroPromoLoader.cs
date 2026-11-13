using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services.Metadata;
using MusicVault.Services.Search;

namespace MusicVault.App.Services;

/// <summary>发现页 Hero 动态广告区曲目源：新歌榜 / 猜你喜欢。</summary>
public enum DiscoverHeroPromoSource
{
    NewSongsChart = 0,
    GuessYouLike = 1
}

/// <summary>发现页横向曲目轨数据源：热歌榜 / 猜你喜欢。</summary>
public enum DiscoverRailSource
{
    HotSongs = 0,
    GuessYouLike = 1
}

public static class DiscoverHeroPromoLoader
{
    public const int DefaultTake = 10;
    /// <summary>换一批时尽量从更大池子里切片，避免总是同一批。</summary>
    public const int PoolTake = 50;
    /// <summary>网易云官方「新歌榜」歌单 id。</summary>
    public const string NetEaseNewSongsPlaylistId = "3779629";

    private static readonly HttpClient Http = MusicSearchHttp.Client;

    /// <summary>清空新歌/热歌/猜你喜欢曲目池会话缓存（登录或 API 配置变更时调用）。</summary>
    public static void InvalidatePoolCache() => DiscoverHeroPromoPoolCache.Invalidate();

    public static async Task<IReadOnlyList<OnlineTrack>> LoadAsync(
        DiscoverHeroPromoSource source,
        int take = DefaultTake,
        int offset = 0,
        IReadOnlyCollection<string>? excludeIds = null,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 20);
        offset = Math.Max(0, offset);
        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        var pool = source switch
        {
            DiscoverHeroPromoSource.GuessYouLike => await LoadGuessYouLikePoolAsync(ct).ConfigureAwait(false),
            _ => await LoadNewSongsPoolAsync(ct).ConfigureAwait(false)
        };

        return SliceBatch(pool, take, offset, excludeIds);
    }

    public static async Task<IReadOnlyList<OnlineTrack>> LoadRailAsync(
        DiscoverRailSource rail,
        int take = DefaultTake,
        int offset = 0,
        IReadOnlyCollection<string>? excludeIds = null,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 20);
        offset = Math.Max(0, offset);
        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        IReadOnlyList<OnlineTrack> pool = rail switch
        {
            DiscoverRailSource.GuessYouLike => await LoadGuessYouLikePoolAsync(ct).ConfigureAwait(false),
            _ => await LoadHotSongsPoolAsync(ct).ConfigureAwait(false)
        };

        return DiscoverTrackBatchSlicer.Slice(pool, take, offset, excludeIds);
    }

    private static IReadOnlyList<OnlineTrack> SliceBatch(
        IReadOnlyList<OnlineTrack> pool,
        int take,
        int offset,
        IReadOnlyCollection<string>? excludeIds) =>
        DiscoverTrackBatchSlicer.Slice(pool, take, offset, excludeIds);

    private static async Task<IReadOnlyList<OnlineTrack>> LoadHotSongsPoolAsync(CancellationToken ct)
    {
        if (DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.HotSongs, out var cached))
            return cached;

        var hot = await TryLoadFromNamedChartAsync(["热歌", "飙升"], PoolTake, ct).ConfigureAwait(false);
        if (hot.Count == 0)
            hot = await LoadNewSongsPoolAsync(ct).ConfigureAwait(false);

        DiscoverHeroPromoPoolCache.Set(DiscoverHeroPromoPoolKind.HotSongs, hot);
        return hot;
    }

    private static async Task<IReadOnlyList<OnlineTrack>> LoadNewSongsPoolAsync(CancellationToken ct)
    {
        if (DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.NewSongs, out var cached))
            return cached;

        var fromChart = await TryLoadFromNamedChartAsync(["新歌"], PoolTake, ct).ConfigureAwait(false);
        if (fromChart.Count == 0)
            fromChart = await LoadPlaylistTracksAsync(NetEaseNewSongsPlaylistId, PoolTake, ct)
                .ConfigureAwait(false);

        DiscoverHeroPromoPoolCache.Set(DiscoverHeroPromoPoolKind.NewSongs, fromChart);
        return fromChart;
    }

    private static async Task<IReadOnlyList<OnlineTrack>> LoadGuessYouLikePoolAsync(CancellationToken ct)
    {
        if (DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.GuessYouLike, out var cached))
            return cached;

        // 需登录态的每日推荐；失败则退到个性化新歌 / 热歌
        IReadOnlyList<OnlineTrack> pool;
        var recommend = await TryFetchRecommendSongsAsync(PoolTake, ct).ConfigureAwait(false);
        if (recommend.Count > 0)
            pool = ShuffleCopy(recommend);
        else
        {
            var personalized = await TryFetchPersonalizedNewSongsAsync(PoolTake, ct).ConfigureAwait(false);
            if (personalized.Count > 0)
                pool = ShuffleCopy(personalized);
            else
            {
                var hot = await TryLoadFromNamedChartAsync(["热歌", "飙升"], PoolTake, ct).ConfigureAwait(false);
                pool = hot.Count > 0
                    ? ShuffleCopy(hot)
                    : await LoadNewSongsPoolAsync(ct).ConfigureAwait(false);
            }
        }

        DiscoverHeroPromoPoolCache.Set(DiscoverHeroPromoPoolKind.GuessYouLike, pool);
        return pool;
    }

    private static IReadOnlyList<OnlineTrack> ShuffleCopy(IReadOnlyList<OnlineTrack> source)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static async Task<IReadOnlyList<OnlineTrack>> TryLoadFromNamedChartAsync(
        IReadOnlyList<string> nameHints,
        int take,
        CancellationToken ct)
    {
        var (charts, _) = await NetEaseChartsService.FetchChartsAsync(forceRefresh: false, ct)
            .ConfigureAwait(false);
        foreach (var hint in nameHints)
        {
            var chart = charts.FirstOrDefault(c =>
                c.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (chart == null || string.IsNullOrWhiteSpace(chart.Id))
                continue;
            var tracks = await LoadPlaylistTracksAsync(chart.Id, take, ct).ConfigureAwait(false);
            if (tracks.Count > 0)
                return tracks;
        }

        return [];
    }

    private static async Task<IReadOnlyList<OnlineTrack>> LoadPlaylistTracksAsync(
        string playlistId,
        int take,
        CancellationToken ct)
    {
        try
        {
            var provider = new MusicVault.Services.Providers.NetEaseMusicProvider();
            var tracks = await provider.GetPlaylistTracksAsync(playlistId, ct).ConfigureAwait(false);
            return tracks.Take(take).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<OnlineTrack>> TryFetchRecommendSongsAsync(
        int take,
        CancellationToken ct)
    {
        var apiBase = MusicServiceEndpoints.ResolveNetEaseApi()?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBase))
            return [];

        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/recommend/songs", ct)
                .ConfigureAwait(false);
            if (doc == null)
                return [];

            if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                return [];

            JsonElement data;
            if (doc.RootElement.TryGetProperty("data", out var dataEl)
                && dataEl.TryGetProperty("dailySongs", out var daily))
                data = daily;
            else if (doc.RootElement.TryGetProperty("recommend", out var recommend))
                data = recommend;
            else
                return [];

            if (data.ValueKind != JsonValueKind.Array)
                return [];

            return ParseTracks(data).Take(take).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<OnlineTrack>> TryFetchPersonalizedNewSongsAsync(
        int take,
        CancellationToken ct)
    {
        var apiBase = MusicServiceEndpoints.ResolveNetEaseApi()?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBase))
            return [];

        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/personalized/newsong", ct)
                .ConfigureAwait(false);
            if (doc == null)
                return [];

            if (!doc.RootElement.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<OnlineTrack>();
            foreach (var item in result.EnumerateArray())
            {
                if (item.TryGetProperty("song", out var song))
                {
                    var t = ParseSongElement(song);
                    if (t != null)
                        list.Add(t);
                }
                else
                {
                    var t = ParseSongElement(item);
                    if (t != null)
                        list.Add(t);
                }

                if (list.Count >= take)
                    break;
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<OnlineTrack> ParseTracks(JsonElement array) =>
        array.EnumerateArray()
            .Select(ParseSongElement)
            .Where(t => t != null)
            .Select(t => t!);

    private static OnlineTrack? ParseSongElement(JsonElement el)
    {
        try
        {
            var id = el.TryGetProperty("id", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.GetString()
                : null;
            var title = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                return null;

            var artists = "";
            if (el.TryGetProperty("ar", out var ar) && ar.ValueKind == JsonValueKind.Array)
                artists = string.Join(" / ", ar.EnumerateArray()
                    .Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            else if (el.TryGetProperty("artists", out var artistsEl) && artistsEl.ValueKind == JsonValueKind.Array)
                artists = string.Join(" / ", artistsEl.EnumerateArray()
                    .Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            var album = "";
            string? cover = null;
            if (el.TryGetProperty("al", out var al))
            {
                album = al.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                cover = al.TryGetProperty("picUrl", out var pic) ? pic.GetString() : null;
            }
            else if (el.TryGetProperty("album", out var albumEl))
            {
                album = albumEl.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                cover = albumEl.TryGetProperty("picUrl", out var pic) ? pic.GetString()
                    : albumEl.TryGetProperty("blurPicUrl", out var blur) ? blur.GetString() : null;
            }

            return new OnlineTrack
            {
                Id = id!,
                Title = title!,
                Artist = artists ?? "",
                Album = album ?? "",
                Platform = MusicPlatform.NetEase,
                CoverUrl = cover
            };
        }
        catch
        {
            return null;
        }
    }
}
