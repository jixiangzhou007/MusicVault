using MusicVault.Services.Api;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class NetEaseAlbumCatalogSupplementTests
{
    [Fact]
    public async Task TryFetch_accusefive_album_from_live_api()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_METADATA_PROBE"), "1", StringComparison.Ordinal))
            return;

        MusicServiceEndpoints.NetEaseApiUrl = "http://localhost:3000";

        var result = await NetEaseAlbumCatalogSupplement.TryFetchAsync("159967997");

        if (result == null)
        {
            // 网易云 API 未启动时跳过（曲目由 go-music-api 提供）
            return;
        }

        Assert.True(result.Tracks.Count >= 10, $"tracks={result.Tracks.Count}");
        Assert.False(string.IsNullOrWhiteSpace(result.Description), "description missing");
    }
}
