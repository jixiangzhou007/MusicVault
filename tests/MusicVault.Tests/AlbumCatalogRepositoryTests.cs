using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Tests;

public sealed class AlbumCatalogRepositoryTests
{
    [Fact]
    public void Serialize_roundtrip_preserves_tracks()
    {
        var tracks = new List<OnlineTrack>
        {
            new()
            {
                Id = "1",
                Platform = MusicPlatform.NetEase,
                Title = "又到天黑",
                Artist = "告五人",
                Album = "带你飞",
                AlbumId = "159967997",
                DurationMs = 289_000,
                TrackNumber = 1
            }
        };

        var json = AlbumCatalogRepository.SerializeTracks(tracks);
        var restored = AlbumCatalogRepository.DeserializeTracks(json);

        Assert.Single(restored);
        Assert.Equal("又到天黑", restored[0].Title);
        Assert.Equal(289_000, restored[0].DurationMs);
    }
}
