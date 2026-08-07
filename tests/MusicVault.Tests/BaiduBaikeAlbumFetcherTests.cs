using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class BaiduBaikeAlbumFetcherTests
{
    [Fact]
    public async Task Fetch_accusefive_dai_ni_fei_from_live_baike()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_METADATA_PROBE"), "1", StringComparison.Ordinal))
            return;

        var fetcher = new BaiduBaikeAlbumFetcher();
        var result = await fetcher.FetchAsync("告五人", "带你飞");

        if (result == null)
        {
            // 部分网络环境无法访问百度百科搜索/词条页
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.True(result.Summary.Length >= 40, $"summary length={result.Summary.Length}");
        Assert.False(AlbumCatalogService.NeedsRichDescription(result.Summary, "百度百科"));
    }
}
