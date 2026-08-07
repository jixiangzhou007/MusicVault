using MusicVault.Core.Enums;

using MusicVault.Core.Models;

using MusicVault.Data.Repositories;

using MusicVault.Services.Api;



namespace MusicVault.Services.Metadata;



/// <summary>在线专辑目录：曲目来自音乐 API，简介优先百度百科并落库缓存。</summary>

public sealed class AlbumCatalogService

{

    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(14);



    private readonly AlbumCatalogRepository _repo;

    private readonly BaiduBaikeAlbumFetcher _baidu = new();



    public AlbumCatalogService(string databasePath) =>

        _repo = new AlbumCatalogRepository(databasePath);



    public Task<CachedAlbumCatalog?> GetCachedAsync(

        string artistName,

        string albumName,

        CancellationToken ct = default) =>

        _repo.GetAsync(artistName, albumName, ct);



    public static bool NeedsRichDescription(string? description, string? source = null, string? albumName = null) =>

        AlbumCatalogDescriptions.NeedsBetterDescription(description, source, albumName);

    public static string? NormalizeDescriptionForDisplay(string? description, string? source, string? albumName = null) =>
        AlbumCatalogDescriptions.NormalizeForDisplay(description, source, albumName);



    public async Task<AlbumOnlineCatalog?> GetOrFetchAsync(

        string artistName,

        string albumName,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(albumName))

            return null;



        var cached = await _repo.GetAsync(artistName, albumName, ct).ConfigureAwait(false);

        if (cached != null && IsFresh(cached) && HasUsableTracks(cached))

            return await ReturnCatalogWithDescriptionAsync(artistName, albumName, cached, ct)

                .ConfigureAwait(false);



        return await FetchAndPersistAsync(artistName, albumName, cached, ct).ConfigureAwait(false);

    }



    /// <summary>补全专辑简介（优先百度百科，其次网易云）。</summary>

    public async Task<(string? Description, string? Source)> TryEnrichDescriptionAsync(

        string artistName,

        string albumName,

        string? platformAlbumId = null,

        CancellationToken ct = default)

    {

        var baidu = await _baidu.FetchAsync(artistName, albumName, ct).ConfigureAwait(false);

        if (baidu != null && !string.IsNullOrWhiteSpace(baidu.Summary))

            return (baidu.Summary, AlbumCatalogDescriptions.BaiduSource);



        string? fromNetEase = null;

        if (!string.IsNullOrWhiteSpace(platformAlbumId))

        {

            await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

            var supplement = await NetEaseAlbumCatalogSupplement.TryFetchAsync(platformAlbumId, ct)

                .ConfigureAwait(false);

            fromNetEase = supplement?.Description;

        }



        GoMusicApiAlbumCatalogFetcher.AlbumSearchMetadata? searchHit = null;

        if (string.IsNullOrWhiteSpace(platformAlbumId) && GoMusicApiClient.IsConfigured)

        {

            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

            searchHit = await GoMusicApiAlbumCatalogFetcher.ResolveSearchMetadataAsync(artistName, albumName, ct)

                .ConfigureAwait(false);

            platformAlbumId = searchHit?.PlatformAlbumId;



            if (string.IsNullOrWhiteSpace(fromNetEase) && !string.IsNullOrWhiteSpace(platformAlbumId))

            {

                await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

                var supplement = await NetEaseAlbumCatalogSupplement.TryFetchAsync(platformAlbumId, ct)

                    .ConfigureAwait(false);

                fromNetEase = supplement?.Description;

            }

        }



        var fromSearch = searchHit?.Description;

        if (searchHit == null && GoMusicApiClient.IsConfigured)

        {

            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

            searchHit = await GoMusicApiAlbumCatalogFetcher.ResolveSearchMetadataAsync(artistName, albumName, ct)

                .ConfigureAwait(false);

            fromSearch = searchHit?.Description;

        }



        if (!string.IsNullOrWhiteSpace(fromNetEase)
            && AlbumCatalogDescriptions.LooksLikeTrackListing(fromNetEase, albumName))
            fromNetEase = AlbumCatalogDescriptions.ExtractAlbumIntroOnly(fromNetEase, albumName);

        var text = AlbumCatalogDescriptions.PickBest(
            fromNetEase,
            AlbumCatalogDescriptions.NetEaseSource,
            fromSearch,
            null,
            albumName);
        var source = AlbumCatalogDescriptions.PickSource(
            fromNetEase,
            AlbumCatalogDescriptions.NetEaseSource,
            fromSearch,
            null,
            albumName);

        return (text, source);

    }



    /// <summary>强制从 API 刷新并写回数据库。</summary>

    public async Task<AlbumOnlineCatalog?> RefreshAsync(

        string artistName,

        string albumName,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(albumName))

            return null;



        var cached = await _repo.GetAsync(artistName, albumName, ct).ConfigureAwait(false);

        return await FetchAndPersistAsync(artistName, albumName, cached, ct).ConfigureAwait(false);

    }



    /// <summary>解析专辑封面 URL（先读库，再轻量搜索）。</summary>

    public async Task<string?> TryResolveCoverUrlAsync(

        string artistName,

        string albumName,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(albumName))

            return null;



        var cached = await _repo.GetAsync(artistName, albumName, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(cached?.CoverUrl))

            return cached.CoverUrl;



        if (!GoMusicApiClient.IsConfigured)

            return null;



        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);



        var hit = await GoMusicApiAlbumCatalogFetcher.ResolveSearchMetadataAsync(artistName, albumName, ct)

            .ConfigureAwait(false);

        if (hit == null || string.IsNullOrWhiteSpace(hit.CoverUrl))

            return null;



        await UpsertCoverHitAsync(artistName, albumName, cached, hit, ct).ConfigureAwait(false);

        return hit.CoverUrl;

    }



    private async Task<AlbumOnlineCatalog?> FetchAndPersistAsync(

        string artistName,

        string albumName,

        CachedAlbumCatalog? existing,

        CancellationToken ct)

    {

        string? description = null;

        string? descriptionSource = null;



        var baidu = await _baidu.FetchAsync(artistName, albumName, ct).ConfigureAwait(false);

        if (baidu != null)

        {

            description = baidu.Summary;

            descriptionSource = AlbumCatalogDescriptions.BaiduSource;

        }



        AlbumOnlineCatalog? live = null;

        if (GoMusicApiClient.IsConfigured)

        {

            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

            await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);



            live = await GoMusicApiAlbumCatalogFetcher.FetchCatalogAsync(artistName, albumName, ct)

                .ConfigureAwait(false);

        }



        if (live == null && existing == null && description == null)

            return null;



        if (description == null && live != null)

        {

            var enriched = await TryEnrichDescriptionAsync(

                    artistName,

                    albumName,

                    live.PlatformAlbumId,

                    ct)

                .ConfigureAwait(false);

            description = enriched.Description;

            descriptionSource = enriched.Source;

        }



        description = AlbumCatalogDescriptions.PickBest(

            description,

            descriptionSource,

            live?.Description,

            live?.DescriptionSource,

            albumName);



        descriptionSource = AlbumCatalogDescriptions.PickSource(

            description,

            descriptionSource,

            live?.Description,

            live?.DescriptionSource,

            albumName);



        var tracks = live?.Tracks ?? AlbumCatalogRepository.DeserializeTracks(existing?.TracksJson ?? "[]");

        var platformId = live?.PlatformAlbumId ?? existing?.PlatformAlbumId ?? string.Empty;

        var platform = live?.Platform ?? existing?.Platform ?? MusicPlatform.NetEase;

        var cover = live?.CoverUrl ?? existing?.CoverUrl;

        var year = live?.Year ?? existing?.Year;



        var catalog = new AlbumOnlineCatalog(

            description,

            descriptionSource,

            platformId,

            platform,

            cover,

            year,

            tracks);



        if (tracks.Count > 0

            || !string.IsNullOrWhiteSpace(description)

            || !string.IsNullOrWhiteSpace(cover))

        {

            await PersistAsync(artistName, albumName, catalog, ct).ConfigureAwait(false);

        }



        return catalog;

    }



    private async Task<AlbumOnlineCatalog> ReturnCatalogWithDescriptionAsync(

        string artistName,

        string albumName,

        CachedAlbumCatalog cached,

        CancellationToken ct)

    {

        if (AlbumCatalogDescriptions.IsDisplayReady(cached.Description, cached.DescriptionSource, cached.Album))

            return ToCatalog(cached);



        var (description, source) = await TryEnrichDescriptionAsync(

                artistName,

                albumName,

                cached.PlatformAlbumId,

                ct)

            .ConfigureAwait(false);



        description = AlbumCatalogDescriptions.PickBest(

            cached.Description,

            cached.DescriptionSource,

            description,

            source,

            albumName);

        source = AlbumCatalogDescriptions.PickSource(

            cached.Description,

            cached.DescriptionSource,

            description,

            source,

            albumName);



        if (!AlbumCatalogDescriptions.IsDisplayReady(description, source, albumName))

            return ToCatalog(cached);



        var entry = new CachedAlbumCatalog

        {

            Artist = cached.Artist,

            Album = cached.Album,

            Platform = cached.Platform,

            PlatformAlbumId = cached.PlatformAlbumId,

            Description = description,

            DescriptionSource = source,

            CoverUrl = cached.CoverUrl,

            Year = cached.Year,

            TracksJson = cached.TracksJson,

            FetchedAt = cached.FetchedAt

        };

        await _repo.UpsertAsync(entry, ct).ConfigureAwait(false);



        return ToCatalog(entry);

    }



    private async Task PersistAsync(

        string artistName,

        string albumName,

        AlbumOnlineCatalog catalog,

        CancellationToken ct)

    {

        var entry = new CachedAlbumCatalog

        {

            Artist = artistName.Trim(),

            Album = albumName.Trim(),

            Platform = catalog.Platform,

            PlatformAlbumId = catalog.PlatformAlbumId ?? string.Empty,

            Description = catalog.Description,

            DescriptionSource = catalog.DescriptionSource,

            CoverUrl = catalog.CoverUrl,

            Year = catalog.Year,

            TracksJson = AlbumCatalogRepository.SerializeTracks(catalog.Tracks),

            FetchedAt = DateTimeOffset.UtcNow

        };

        await _repo.UpsertAsync(entry, ct).ConfigureAwait(false);

    }



    private async Task UpsertCoverHitAsync(

        string artistName,

        string albumName,

        CachedAlbumCatalog? existing,

        GoMusicApiAlbumCatalogFetcher.AlbumSearchMetadata hit,

        CancellationToken ct)

    {

        var description = AlbumCatalogDescriptions.PickBest(

            existing?.Description,

            existing?.DescriptionSource,

            hit.Description,

            null,

            albumName);

        var source = AlbumCatalogDescriptions.PickSource(

            existing?.Description,

            existing?.DescriptionSource,

            hit.Description,

            null,

            albumName);



        var entry = new CachedAlbumCatalog

        {

            Artist = artistName.Trim(),

            Album = albumName.Trim(),

            Platform = existing?.Platform ?? MusicPlatform.NetEase,

            PlatformAlbumId = hit.PlatformAlbumId,

            Description = description,

            DescriptionSource = source,

            CoverUrl = hit.CoverUrl,

            Year = existing?.Year ?? hit.Year,

            TracksJson = existing?.TracksJson ?? "[]",

            FetchedAt = DateTimeOffset.UtcNow

        };

        await _repo.UpsertAsync(entry, ct).ConfigureAwait(false);

    }



    private static bool IsFresh(CachedAlbumCatalog cached) =>

        DateTimeOffset.UtcNow - cached.FetchedAt < CacheTtl;



    private static bool HasUsableTracks(CachedAlbumCatalog cached) =>

        AlbumCatalogRepository.DeserializeTracks(cached.TracksJson).Count > 0;



    private static AlbumOnlineCatalog ToCatalog(CachedAlbumCatalog cached) =>

        new(

            AlbumCatalogDescriptions.NormalizeForDisplay(cached.Description, cached.DescriptionSource, cached.Album),

            cached.DescriptionSource,

            cached.PlatformAlbumId,

            cached.Platform,

            cached.CoverUrl,

            cached.Year,

            AlbumCatalogRepository.DeserializeTracks(cached.TracksJson));

}


