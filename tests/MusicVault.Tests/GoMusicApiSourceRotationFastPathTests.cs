using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

/// <summary>
/// 需本机已配置且可达的统一 API。未配置或探活失败时跳过，不挡 CI / 发版。
/// </summary>
public sealed class GoMusicApiSourceRotationFastPathTests
{
    [Fact]
    public async Task TryResolve_original_netease_before_cross_source_search()
    {
        if (!await IsLiveUnifiedApiAsync().ConfigureAwait(true))
            return;

        var track = new OnlineTrack
        {
            Id = "3448738711",
            Title = "无意",
            Artist = "加木",
            Platform = MusicPlatform.NetEase,
            SupportsMp3128 = true,
            SupportsMp3320 = true
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await GoMusicApiClient.TryResolveDownloadWithSourceRotationAsync(
            track,
            AudioQuality.High320);
        sw.Stop();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.Value.Url));
        Assert.Equal(MusicPlatform.NetEase, result.Value.Track.Platform);
        Assert.Equal("3448738711", result.Value.Track.Id);
        // 快路径应在数秒内返回；旧逻辑会先搜已登录 QQ 再解析，常超过 8s
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8), $"resolve took {sw.Elapsed}");
    }

    private static async Task<bool> IsLiveUnifiedApiAsync()
    {
        var url = MusicApiConfig.Resolve();
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync(url.TrimEnd('/') + "/", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }
}
