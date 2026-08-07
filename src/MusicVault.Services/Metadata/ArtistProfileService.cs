using System.Text.Json;
using MusicVault.Core.Artists;
using MusicVault.Core.Browse;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>艺术家资料：音乐平台 + 百度百科，落库缓存。</summary>
public sealed class ArtistProfileService
{
    private static readonly JsonSerializerOptions AlbumsJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(42);
    private const int MaxLookupVariants = 4;
    private const int MaxBaiduSummaryLength = 230;

    private readonly ArtistProfileRepository _repo;
    private readonly OnlineMusicArtistProfileFetcher _online;
    private readonly BaiduBaikeArtistFetcher _baidu = new();
    private readonly PlatformArtistPortraitFetcher _portrait = new();
    private readonly GoMusicApiArtistProfileFetcher _goMusicApi = new();
    private readonly IArtistNameValidator? _artistValidator;
    private readonly string _photoDir;

    public ArtistProfileService(
        string databasePath,
        string dataDirectory,
        IEnumerable<IMusicSourceProvider>? platformProviders = null,
        IArtistNameValidator? artistValidator = null)
    {
        _repo = new ArtistProfileRepository(databasePath);
        _online = new OnlineMusicArtistProfileFetcher(platformProviders);
        _artistValidator = artistValidator;
        _photoDir = Path.Combine(dataDirectory, "ArtistPhotos");
        Directory.CreateDirectory(_photoDir);
    }

    public Task<IReadOnlyDictionary<string, ArtistProfile>> GetManyAsync(
        IEnumerable<string> names, CancellationToken ct = default) =>
        _repo.GetManyAsync(names, ct);

    /// <summary>仅读数据库缓存，不发起网络请求。</summary>
    public Task<ArtistProfile?> GetCachedAsync(string artistName, CancellationToken ct = default) =>
        _repo.GetAsync(artistName, ct);

    public IReadOnlyList<OnlineArtistAlbum> GetCachedAlbums(ArtistProfile? profile) =>
        DeserializeAlbums(profile?.AlbumsJson);

    public async Task<IReadOnlyList<OnlineArtistAlbum>> GetCachedAlbumsAsync(
        string artistName, CancellationToken ct = default)
    {
        var profile = await _repo.GetAsync(artistName, ct).ConfigureAwait(false);
        return GetCachedAlbums(profile);
    }

    public async Task SaveAlbumsCacheAsync(
        string artistName,
        IReadOnlyList<OnlineArtistAlbum> albums,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return;

        await _repo.UpsertAlbumsCacheAsync(artistName, SerializeAlbums(albums), ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<OnlineArtistAlbum>> FetchOfficialAlbumsAsync(
        ArtistProfile? profile, CancellationToken ct = default) =>
        _online.FetchOfficialAlbumsAsync(profile, ct);

    public Task<IReadOnlyList<OnlineArtistAlbum>> FetchOnlineAlbumsAsync(
        ArtistProfile? profile, CancellationToken ct = default) =>
        FetchOfficialAlbumsAsync(profile, ct);

    public byte[]? LoadPhotoBytes(ArtistProfile? profile)
    {
        if (profile?.PhotoFileName is not { Length: > 0 } file)
            return null;
        var path = Path.Combine(_photoDir, file);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public bool NeedsFetch(ArtistProfile? profile, bool force = false)
    {
        if (force) return true;
        if (profile == null) return true;
        if (profile.FetchStatus is ArtistProfileFetchStatus.Error or ArtistProfileFetchStatus.None)
            return true;
        if (profile.FetchStatus == ArtistProfileFetchStatus.NotFound)
            return true;
        if (NeedsBaiduEnrich(profile)) return true;
        if (NeedsPortraitRefresh(profile)) return true;
        if (!IsComplete(profile))
            return true;
        return !IsFresh(profile);
    }

    public async Task<ArtistProfile> GetOrFetchAsync(string artistName, bool forceRefresh = false, CancellationToken ct = default)
    {
        var existing = await _repo.GetAsync(artistName, ct);
        if (!forceRefresh && existing != null && IsComplete(existing) && IsFresh(existing) && !NeedsBaiduEnrich(existing))
            return existing;

        var enrichBaidu = existing == null || NeedsBaiduEnrich(existing);
        return await RefreshFromBaiduAsync(artistName, existing, ct, enrichBaidu: enrichBaidu)
            .ConfigureAwait(false);
    }

    /// <summary>刷新：爬虫更新简介/头像；保留或补全音乐平台 LemmaId（供专辑 API）。</summary>
    /// <param name="resolveLemmaId">为 false 时仅保留已有 LemmaId，不发起音乐平台 ID 检索（批量列表刷新时更快）。</param>
    /// <param name="enrichBaidu">为 false 时跳过百度百科（已有完整平台资料时加快打开详情）。</param>
    public async Task<ArtistProfile> RefreshFromBaiduAsync(
        string artistName,
        ArtistProfile? existing = null,
        CancellationToken ct = default,
        bool resolveLemmaId = true,
        bool enrichBaidu = true)
    {
        existing ??= await _repo.GetAsync(artistName, ct).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RefreshTimeout);
        var token = timeoutCts.Token;

        try
        {
            await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(token).ConfigureAwait(false);

            ArtistExternalFetchResult? platform = null;
            if (resolveLemmaId || string.IsNullOrWhiteSpace(existing?.LemmaId))
            {
                var variantCount = 0;
                foreach (var lookupName in ArtistNameEquivalence.EnumerateLookupNames(artistName))
                {
                    if (++variantCount > MaxLookupVariants)
                        break;

                    var attempt = await _online.FetchAsync(lookupName, token).ConfigureAwait(false);
                    if (!HasAnyContent(attempt))
                        continue;

                    platform = platform == null ? attempt : MergePlatform(platform, attempt);
                    var hasLemma = !string.IsNullOrWhiteSpace(platform.LemmaId);
                    var hasSummary = !string.IsNullOrWhiteSpace(platform.Summary);
                    var hasPhoto = !string.IsNullOrWhiteSpace(platform.PhotoUrl);
                    if (hasLemma && hasSummary && hasPhoto)
                        break;
                }
            }

            string? lemmaId = FirstNonEmpty(existing?.LemmaId, platform?.LemmaId);
            if ((resolveLemmaId || string.IsNullOrWhiteSpace(lemmaId))
                && string.IsNullOrWhiteSpace(lemmaId))
            {
                var goId = await _goMusicApi.ResolveNeteaseArtistIdAsync(artistName, token)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(goId))
                    lemmaId = $"netease:{goId}";
            }

            ArtistExternalFetchResult? baidu = null;
            if (enrichBaidu)
                baidu = await _baidu.FetchAsync(artistName, token).ConfigureAwait(false);

            var summary = FirstNonEmpty(baidu?.Summary, platform?.Summary);
            var description = FirstNonEmpty(baidu?.Description, platform?.Description);
            var sourceName = !string.IsNullOrWhiteSpace(baidu?.Summary)
                ? ArtistSourceKeys.BaiduBaike
                : platform?.SourceName ?? ArtistSourceKeys.NetEaseMusic;
            var sourceUrl = FirstNonEmpty(platform?.SourceUrl, baidu?.SourceUrl);

            var hasPlatformId = !string.IsNullOrWhiteSpace(lemmaId);
            var hasText = !string.IsNullOrWhiteSpace(summary) || !string.IsNullOrWhiteSpace(description);
            var portraitCandidates = await CollectPortraitCandidatesAsync(
                artistName, baidu, platform, token).ConfigureAwait(false);
            var hasPhotoSource = portraitCandidates.Count > 0;

            if (_artistValidator != null)
            {
                var status = await _artistValidator.ValidateArtistNameAsync(artistName, token).ConfigureAwait(false);
                if (status == ArtistNameValidationStatus.NotRecognized && !hasPlatformId && !hasText && !hasPhotoSource)
                {
                    var unrecognized = new ArtistProfile
                    {
                        Name = artistName,
                        LemmaId = lemmaId,
                        FetchStatus = ArtistProfileFetchStatus.NotFound,
                        FetchedAt = DateTime.UtcNow,
                        ErrorMessage = ServiceMessageCodec.Key("Artist.Profile.NotFoundOnPlatforms")
                    };
                    await _repo.UpsertAsync(unrecognized, token).ConfigureAwait(false);
                    return unrecognized;
                }
            }

            if (!hasText && !hasPhotoSource && !hasPlatformId)
            {
                if (existing is { FetchStatus: ArtistProfileFetchStatus.Ok })
                    return existing;

                var notFound = new ArtistProfile
                {
                    Name = artistName,
                    LemmaId = lemmaId,
                    FetchStatus = ArtistProfileFetchStatus.NotFound,
                    FetchedAt = DateTime.UtcNow,
                    ErrorMessage = ServiceMessageCodec.Key("Artist.Profile.NotFoundCached")
                };
                await _repo.UpsertAsync(notFound, token).ConfigureAwait(false);
                return notFound;
            }

            string? photoFile = existing?.PhotoFileName;
            if (portraitCandidates.Count > 0)
            {
                var downloaded = await DownloadBestPortraitAsync(artistName, portraitCandidates, token)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(downloaded))
                    photoFile = downloaded;
            }

            var profile = new ArtistProfile
            {
                Name = artistName,
                Summary = summary,
                Description = description,
                PhotoFileName = photoFile,
                SourceUrl = sourceUrl,
                SourceName = sourceName,
                LemmaId = lemmaId,
                BrowseRegion = ResolveBrowseRegion(artistName, existing, platform, summary, description),
                FetchStatus = ArtistProfileFetchStatus.Ok,
                FetchedAt = DateTime.UtcNow
            };
            await _repo.UpsertAsync(profile, token).ConfigureAwait(false);
            return profile;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            if (existing is { FetchStatus: ArtistProfileFetchStatus.Ok })
                return existing;

            var timedOut = new ArtistProfile
            {
                Name = artistName,
                FetchStatus = ArtistProfileFetchStatus.Error,
                ErrorMessage = ServiceMessageCodec.Key("Artist.Profile.Timeout"),
                FetchedAt = DateTime.UtcNow
            };
            await _repo.UpsertAsync(timedOut, CancellationToken.None).ConfigureAwait(false);
            return timedOut;
        }
        catch (Exception ex)
        {
            var err = new ArtistProfile
            {
                Name = artistName,
                FetchStatus = ArtistProfileFetchStatus.Error,
                ErrorMessage = ServiceMessageCodec.Format("General.ExternalDetail", ex.Message),
                FetchedAt = DateTime.UtcNow
            };
            await _repo.UpsertAsync(err, ct).ConfigureAwait(false);
            return err;
        }
    }

    private bool NeedsBaiduEnrich(ArtistProfile profile)
    {
        if (profile.FetchStatus != ArtistProfileFetchStatus.Ok)
            return true;

        if (ArtistSourceKeys.IsBaiduBaike(profile.SourceName))
        {
            return string.IsNullOrWhiteSpace(profile.Summary)
                || (profile.Summary?.Length ?? 0) > MaxBaiduSummaryLength
                || (profile.Summary?.Contains("播报", StringComparison.Ordinal) ?? false)
                || (profile.Summary?.Contains("展开", StringComparison.Ordinal) ?? false)
                || (profile.Summary?.Contains("同名词条", StringComparison.Ordinal) ?? false)
                || LooksLikeStaleBaiduSummary(profile.Summary);
        }

        if (!string.IsNullOrWhiteSpace(profile.Summary) && profile.Summary.Length >= 30)
            return false;

        return string.IsNullOrWhiteSpace(profile.Summary);
    }

    public bool NeedsPortraitRefresh(ArtistProfile? profile) =>
        profile == null
        || string.IsNullOrWhiteSpace(profile.PhotoFileName)
        || LooksLikeLowQualityPortrait(profile.PhotoFileName);

    /// <summary>平台缩略图通常很小；歌手接口大图应明显更大。</summary>
    private bool LooksLikeLowQualityPortrait(string? photoFileName)
    {
        if (string.IsNullOrWhiteSpace(photoFileName))
            return true;

        var path = Path.Combine(_photoDir, photoFileName);
        if (!File.Exists(path))
            return true;

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 6 * 1024)
                return true;

            var dims = ImageDimensionProbe.TryReadJpegDimensions(bytes);
            if (dims is { Width: var w, Height: var h } && w > h * 1.15)
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool LooksLikeStaleBaiduSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return true;

        // 新版词条卡片只有身份标签、无出生/经历信息时视为旧缓存
        if (summary.Length < 50
            && !summary.Contains("，", StringComparison.Ordinal)
            && !summary.Contains("。", StringComparison.Ordinal)
            && (summary.Contains("歌手", StringComparison.Ordinal)
                || summary.Contains("演员", StringComparison.Ordinal)))
            return true;

        return false;
    }

    private static bool HasAnyContent(ArtistExternalFetchResult? r) =>
        r != null && (
            !string.IsNullOrWhiteSpace(r.Summary)
            || !string.IsNullOrWhiteSpace(r.Description)
            || !string.IsNullOrWhiteSpace(r.PhotoUrl));

    private static ArtistExternalFetchResult MergePlatform(ArtistExternalFetchResult a, ArtistExternalFetchResult b) =>
        new(
            FirstNonEmpty(a.Summary, b.Summary),
            FirstNonEmpty(a.Description, b.Description),
            FirstNonEmpty(a.PhotoUrl, b.PhotoUrl),
            FirstNonEmpty(a.SourceUrl, b.SourceUrl),
            FirstNonEmpty(a.LemmaId, b.LemmaId),
            !string.IsNullOrWhiteSpace(a.Summary) ? a.SourceName : b.SourceName,
            a.NetEaseArea ?? b.NetEaseArea);

    private static ArtistBrowseRegion? ResolveBrowseRegion(
        string artistName,
        ArtistProfile? existing,
        ArtistExternalFetchResult? platform,
        string? summary,
        string? description)
    {
        if (platform?.NetEaseArea is int area && NetEaseArtistAreaMapper.Map(area) is { } fromNetEase)
            return fromNetEase;

        var profileText = string.Join(' ',
            new[] { summary, description }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (ArtistRegionClassifier.InferFromProfileText(profileText) is { } fromText)
            return fromText;

        var normalizedName = ArtistNameNormalizer.Normalize(artistName);
        if (existing?.BrowseRegion is ArtistBrowseRegion stored)
            return stored;

        return null;
    }

    /// <summary>为纯拉丁名歌手批量解析网易云分区并写库。</summary>
    public async Task<int> ReconcileBrowseRegionsAsync(
        IEnumerable<string> artistNames,
        CancellationToken ct = default)
    {
        var names = artistNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0)
            return 0;

        var profiles = await _repo.GetManyAsync(names, ct).ConfigureAwait(false);
        var pending = names
            .Where(n => ArtistRegionClassifier.NeedsPlatformRegionResolution(
                n, profiles.GetValueOrDefault(n)?.BrowseRegion))
            .ToList();
        if (pending.Count == 0)
            return 0;

        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);
        var api = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(api))
            return 0;

        using var gate = new SemaphoreSlim(4);
        var updated = 0;

        var tasks = pending.Select(async name =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                profiles.TryGetValue(name, out var profile);
                var region = ResolveRegionFromProfileText(profile);
                if (region is null)
                {
                    var match = await NetEaseArtistIdResolver.SearchArtistMatchAsync(api, name, ct)
                        .ConfigureAwait(false);
                    if (match?.Area is int area)
                        region = NetEaseArtistAreaMapper.Map(area);
                }

                if (region is null)
                    return;

                await _repo.UpdateBrowseRegionAsync(name, region.Value, ct).ConfigureAwait(false);
                Interlocked.Increment(ref updated);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return updated;
    }

    private static ArtistBrowseRegion? ResolveRegionFromProfileText(ArtistProfile? profile)
    {
        if (profile == null)
            return null;

        var text = string.Join(' ',
            new[] { profile.Summary, profile.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return ArtistRegionClassifier.InferFromProfileText(text);
    }

    private static bool IsComplete(ArtistProfile profile) =>
        profile.FetchStatus == ArtistProfileFetchStatus.Ok
        && (!string.IsNullOrWhiteSpace(profile.Summary)
            || !string.IsNullOrWhiteSpace(profile.PhotoFileName));

    private static bool IsFresh(ArtistProfile profile) =>
        profile.FetchedAt.HasValue && DateTime.UtcNow - profile.FetchedAt.Value < CacheTtl;

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> CollectPortraitCandidatesAsync(
        string artistName,
        ArtistExternalFetchResult? baidu,
        ArtistExternalFetchResult? platform,
        CancellationToken ct)
    {
        var list = new List<string>();

        void Add(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (url.Contains("bcebos", StringComparison.OrdinalIgnoreCase)
                || url.Contains("bkimg", StringComparison.OrdinalIgnoreCase))
                url = BaiduBaikeSummaryExtractor.UpgradeBaiduPortraitUrl(url);
            else
                url = PlatformArtistPortraitFetcher.UpgradeImageUrl(url);

            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!list.Contains(url, StringComparer.OrdinalIgnoreCase))
                list.Add(url);
        }

        var baiduPortrait = await _baidu.FetchPortraitUrlAsync(artistName, ct).ConfigureAwait(false);
        Add(baiduPortrait);
        Add(baidu?.PhotoUrl);

        var platformPortrait = await _portrait.FetchPortraitUrlAsync(artistName, ct).ConfigureAwait(false);
        Add(platformPortrait);
        Add(platform?.PhotoUrl);

        return list;
    }

    private async Task<string?> DownloadBestPortraitAsync(
        string artistName,
        IReadOnlyList<string> urls,
        CancellationToken ct)
    {
        foreach (var url in urls)
        {
            var file = await TryDownloadPortraitFileAsync(artistName, url, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(file))
                continue;

            if (!LooksLikeLowQualityPortrait(file))
                return file;

            try
            {
                File.Delete(Path.Combine(_photoDir, file));
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private async Task<string?> TryDownloadPortraitFileAsync(string artistName, string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            req.Headers.Referrer = ResolvePhotoReferrer(url);
            req.Headers.TryAddWithoutValidation("Accept", "image/*,*/*");

            using var resp = await HttpClients.Download.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (bytes.Length < 256)
                return null;

            var dims = ImageDimensionProbe.TryReadJpegDimensions(bytes);
            if (dims is { Width: var w, Height: var h } && w > h * 1.15)
                return null;

            var file = BuildPhotoFileName(artistName);
            var path = Path.Combine(_photoDir, file);
            if (File.Exists(path))
                File.Delete(path);
            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
            return file;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> DownloadArtistPhotoAsync(string artistName, string url, CancellationToken ct) =>
        await TryDownloadPortraitFileAsync(artistName, url, ct).ConfigureAwait(false);

    private static Uri ResolvePhotoReferrer(string url)
    {
        if (url.Contains("bkimg", StringComparison.OrdinalIgnoreCase)
            || url.Contains("bcebos", StringComparison.OrdinalIgnoreCase)
            || url.Contains("baike.baidu.com", StringComparison.OrdinalIgnoreCase))
            return new Uri("https://baike.baidu.com/");

        if (url.Contains("gtimg.cn", StringComparison.OrdinalIgnoreCase)
            || url.Contains("y.qq.com", StringComparison.OrdinalIgnoreCase))
            return new Uri("https://y.qq.com/");

        if (url.Contains("126.net", StringComparison.OrdinalIgnoreCase)
            || url.Contains("music.163.com", StringComparison.OrdinalIgnoreCase))
            return new Uri("https://music.163.com/");

        return new Uri("https://music.163.com/");
    }

    private static string BuildPhotoFileName(string artistName)
    {
        var safe = string.Join('_', artistName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safe))
            safe = "artist";
        if (safe.Length > 80)
            safe = safe[..80];
        return safe + ".jpg";
    }

    private static string SerializeAlbums(IReadOnlyList<OnlineArtistAlbum> albums) =>
        JsonSerializer.Serialize(albums, AlbumsJsonOptions);

    private static IReadOnlyList<OnlineArtistAlbum> DeserializeAlbums(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<OnlineArtistAlbum>>(json, AlbumsJsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
