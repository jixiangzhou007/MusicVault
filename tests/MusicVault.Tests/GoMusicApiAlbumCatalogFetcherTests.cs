using MusicVault.Services.Api;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class GoMusicApiAlbumCatalogFetcherTests
{
    [Fact]
    public async Task FetchCatalog_accusefive_dai_ni_fei_from_live_api()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_METADATA_PROBE"), "1", StringComparison.Ordinal))
            return;

        MusicApiConfig.BaseUrl = "http://localhost:8080";
        MusicServiceEndpoints.NetEaseApiUrl = "http://localhost:3000";

        var catalog = await GoMusicApiAlbumCatalogFetcher.FetchCatalogAsync("告五人", "带你飞");

        Assert.NotNull(catalog);
        Assert.False(string.IsNullOrWhiteSpace(catalog!.PlatformAlbumId), "albumId missing");
        Assert.True(catalog.Tracks.Count >= 10, $"tracks={catalog.Tracks.Count}, id={catalog.PlatformAlbumId}");
        Assert.False(string.IsNullOrWhiteSpace(catalog.Description), "description missing");
    }
}
