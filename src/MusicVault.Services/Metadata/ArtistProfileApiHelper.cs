using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;

namespace MusicVault.Services.Metadata;

/// <summary>抓取艺术家/专辑资料前确保 go-music-api 与网易云 API 已配置并可访问。</summary>
public static class ArtistProfileApiHelper
{
    private static readonly TimeSpan ApiReadyTimeout = TimeSpan.FromSeconds(45);
    private static readonly object ReadyLock = new();
    private static Task<string?>? _musicApiReadyTask;
    private static Task<string?>? _netEaseApiReadyTask;

    public static Task<string?> EnsureNetEaseApiReadyAsync(CancellationToken ct = default) =>
        GetOrStartReadyTask(ref _netEaseApiReadyTask, EnsureNetEaseApiReadyCoreAsync, ct);

    private static async Task<string?> EnsureNetEaseApiReadyCoreAsync(CancellationToken ct)
    {
        MusicServiceEndpoints.ApplyFromEnvironment();

        var baseDir = AppContext.BaseDirectory;
        BundledNetEaseApiHost.Instance.Discover(baseDir);

        var url = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(url) && BundledNetEaseApiHost.Instance.HasBundledApi)
        {
            url = BundledNetEaseApiHost.DefaultBaseUrl;
            MusicServiceEndpoints.NetEaseApiUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", url);
        }
        else if (!string.IsNullOrWhiteSpace(url))
        {
            MusicServiceEndpoints.NetEaseApiUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", url);
        }

        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (BundledNetEaseApiHost.IsLocalApiUrl(url) && BundledNetEaseApiHost.Instance.CanAutoStart)
        {
            var effective = await BundledNetEaseApiHost.Instance
                .EnsureRunningWithPortFallbackAsync(url, ct)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(effective))
                return null;

            url = effective;
            MusicServiceEndpoints.NetEaseApiUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", url);
        }

        return url.TrimEnd('/');
    }

    public static Task<string?> EnsureMusicApiReadyAsync(CancellationToken ct = default) =>
        GetOrStartReadyTask(ref _musicApiReadyTask, EnsureMusicApiReadyCoreAsync, ct);

    private static Task<string?> GetOrStartReadyTask(
        ref Task<string?>? slot,
        Func<CancellationToken, Task<string?>> core,
        CancellationToken ct)
    {
        lock (ReadyLock)
        {
            if (slot is { IsFaulted: true } or { IsCanceled: true })
                slot = null;

            // 失败(null) 不永久缓存，允许下次重试拉起
            if (slot is { IsCompletedSuccessfully: true })
            {
                if (slot.Result != null)
                    return slot;
                slot = null;
            }

            slot = RunReadyWithTimeoutAsync(core, ct);
            return slot;
        }
    }

    private static async Task<string?> RunReadyWithTimeoutAsync(
        Func<CancellationToken, Task<string?>> core,
        CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ApiReadyTimeout);
        return await core(timeout.Token).ConfigureAwait(false);
    }

    private static async Task<string?> EnsureMusicApiReadyCoreAsync(CancellationToken ct)
    {
        MusicServiceEndpoints.ApplyFromEnvironment();

        var baseDir = AppContext.BaseDirectory;
        BundledMusicApiHost.Instance.Discover(baseDir);

        var url = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(MusicApiConfig.BaseUrl);
        if (string.IsNullOrWhiteSpace(url) && BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            url = BundledMusicApiHost.DefaultBaseUrl;
            MusicApiConfig.BaseUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", url);
        }
        else if (!string.IsNullOrWhiteSpace(url))
        {
            MusicApiConfig.BaseUrl = url;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", url);
        }

        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (BundledMusicApiHost.IsLocalApiUrl(url) && BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            var ok = await BundledMusicApiHost.Instance.EnsureRunningAsync(url, ct).ConfigureAwait(false);
            if (!ok)
                return null;
        }

        return url.TrimEnd('/');
    }
}
