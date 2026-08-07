using MusicVault.Core.Artists;
using MusicVault.Services.Api;

namespace MusicVault.Services.Metadata;

/// <summary>校验艺术家名是否被网易云或 QQ 音乐识别；同会话缓存 + 并发限制。</summary>
public sealed class ArtistNameValidator : IArtistNameValidator
{
    /// <summary>true=匹配，false=检索成功无匹配，null=未检索或失败。</summary>
    public delegate Task<bool?> PlatformSearchDelegate(string name, CancellationToken ct);

    private readonly PlatformSearchDelegate _neteaseSearch;
    private readonly PlatformSearchDelegate _qqSearch;
    private readonly bool _useInjectedSearch;
    private readonly Dictionary<string, ArtistNameValidationStatus> _statusCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(4);
    private bool? _platformsAvailable;

    public ArtistNameValidator(
        PlatformSearchDelegate? neteaseSearch = null,
        PlatformSearchDelegate? qqSearch = null)
    {
        _useInjectedSearch = neteaseSearch != null || qqSearch != null;
        _neteaseSearch = neteaseSearch ?? SearchNetEaseAsync;
        _qqSearch = qqSearch ?? SearchQqAsync;
    }

    public async Task<ArtistNameValidationStatus> ValidateArtistNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = ArtistNameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return ArtistNameValidationStatus.NotRecognized;

        if (!ArtistPerformerClassifier.IsLikelyPerformer(normalized))
            return ArtistNameValidationStatus.NotRecognized;

        if (_statusCache.TryGetValue(normalized, out var cached))
            return cached;

        if (!await ArePlatformsAvailableAsync(ct).ConfigureAwait(false))
        {
            _statusCache[normalized] = ArtistNameValidationStatus.Inconclusive;
            return ArtistNameValidationStatus.Inconclusive;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_statusCache.TryGetValue(normalized, out cached))
                return cached;

            var status = await SearchPlatformsAsync(normalized, ct).ConfigureAwait(false);
            _statusCache[normalized] = status;
            return status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsRecognizedArtistAsync(string name, CancellationToken ct = default) =>
        await ValidateArtistNameAsync(name, ct).ConfigureAwait(false) == ArtistNameValidationStatus.Recognized;

    public async Task<string> FilterArtistFieldAsync(string? artistField, CancellationToken ct = default)
    {
        var parts = ArtistFieldParser.Split(artistField);
        if (parts.Count == 0)
            return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            var normalized = ArtistNameNormalizer.Normalize(part);
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            var status = await ValidateArtistNameAsync(normalized, ct).ConfigureAwait(false);
            if (status is ArtistNameValidationStatus.Recognized or ArtistNameValidationStatus.Inconclusive)
                list.Add(normalized);
        }

        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(" / ", list)
        };
    }

    private async Task<ArtistNameValidationStatus> SearchPlatformsAsync(string normalized, CancellationToken ct)
    {
        bool? netease = null;
        foreach (var lookupName in ArtistLookupNameVariants.Enumerate(normalized))
        {
            var result = await _neteaseSearch(lookupName, ct).ConfigureAwait(false);
            if (result == true)
                return ArtistNameValidationStatus.Recognized;
            if (result == false)
                netease = false;
        }

        bool? qq = null;
        foreach (var lookupName in ArtistLookupNameVariants.Enumerate(normalized))
        {
            var result = await _qqSearch(lookupName, ct).ConfigureAwait(false);
            if (result == true)
                return ArtistNameValidationStatus.Recognized;
            if (result == false)
                qq = false;
        }

        if (netease == false && qq == false)
            return ArtistNameValidationStatus.NotRecognized;

        // 仅一个平台可用且返回「无匹配」时不能认定无效，避免治理误删艺术家。
        return ArtistNameValidationStatus.Inconclusive;
    }

    private static bool IsNetEaseConfigured() =>
        !string.IsNullOrWhiteSpace(MusicServiceEndpoints.ResolveNetEaseApi()) || GoMusicApiClient.IsConfigured;

    private static bool IsQqConfigured() =>
        !string.IsNullOrWhiteSpace(QqMusicArtistResolver.ResolveApiBase());

    private async Task<bool> ArePlatformsAvailableAsync(CancellationToken ct)
    {
        if (_useInjectedSearch)
            return true;

        if (_platformsAvailable.HasValue)
            return _platformsAvailable.Value;

        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);
        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        _platformsAvailable = IsNetEaseConfigured() || IsQqConfigured();
        return _platformsAvailable.Value;
    }

    private static async Task<bool?> SearchNetEaseAsync(string name, CancellationToken ct)
    {
        var searched = false;
        var neteaseApi = MusicServiceEndpoints.ResolveNetEaseApi();
        if (!string.IsNullOrWhiteSpace(neteaseApi))
        {
            searched = true;
            try
            {
                var id = await NetEaseArtistIdResolver.SearchArtistIdAsync(neteaseApi, name, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(id))
                    return true;
            }
            catch
            {
                return null;
            }
        }

        var goMusicApi = new GoMusicApiArtistProfileFetcher();
        if (goMusicApi.IsAvailable)
        {
            searched = true;
            try
            {
                var goId = await goMusicApi.ResolveNeteaseArtistIdAsync(name, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(goId))
                    return true;
            }
            catch
            {
                return null;
            }
        }

        return searched ? false : null;
    }

    private static async Task<bool?> SearchQqAsync(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(QqMusicArtistResolver.ResolveApiBase()))
            return null;

        try
        {
            var found = await QqMusicArtistResolver.IsRecognizedAsync(name, ct).ConfigureAwait(false);
            return found;
        }
        catch
        {
            return null;
        }
    }
}
