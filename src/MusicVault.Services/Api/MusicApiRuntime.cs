using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>统一拉起本地 go-music-api，并在端口回退时写回 <see cref="MusicApiConfig"/>。</summary>
public static class MusicApiRuntime
{
    /// <summary>
    /// 本地 URL 不可达时尝试启动；8080 失败则回退 18080。
    /// 成功时始终写回 <see cref="MusicApiConfig.BaseUrl"/>。
    /// </summary>
    public static async Task<string?> EnsureLocalUnifiedReadyAsync(CancellationToken ct = default)
    {
        var configured = MusicApiConfig.Resolve();
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var url = configured.Trim().TrimEnd('/');
        if (!BundledMusicApiHost.IsLocalApiUrl(url))
            return url;

        // 健康探测/设置页可能先于 Bootstrap 调用：此处补 Discover，避免误判无内置 exe
        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);

        if (await BundledMusicApiHost.IsHealthyAsync(url, quick: true, ct).ConfigureAwait(false))
        {
            MusicApiConfig.BaseUrl = url;
            return url;
        }

        if (!BundledMusicApiHost.Instance.HasBundledExecutable)
            return null;

        var effective = await BundledMusicApiHost.Instance
            .EnsureRunningWithPortFallbackAsync(url, ct)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(effective))
            return null;

        MusicApiConfig.BaseUrl = effective;
        return effective;
    }

    /// <summary>健康探测发现回退端口时同步配置（不启动进程）。</summary>
    public static void ApplyResolvedBaseUrl(string? resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return;
        MusicApiConfig.BaseUrl = resolved.Trim().TrimEnd('/');
    }
}
