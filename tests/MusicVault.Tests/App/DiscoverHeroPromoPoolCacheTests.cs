using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverHeroPromoPoolCacheTests
{
    public DiscoverHeroPromoPoolCacheTests() => DiscoverHeroPromoPoolCache.Invalidate();

    [Fact]
    public void TryGet_miss_then_hit_within_ttl()
    {
        Assert.False(DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.HotSongs, out _));

        var tracks = new[]
        {
            new OnlineTrack
            {
                Id = "1",
                Title = "A",
                Artist = "B",
                Platform = MusicPlatform.NetEase
            }
        };
        DiscoverHeroPromoPoolCache.Set(DiscoverHeroPromoPoolKind.HotSongs, tracks);

        Assert.True(DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.HotSongs, out var cached));
        Assert.Single(cached);
        Assert.Equal("1", cached[0].Id);
    }

    [Fact]
    public void TryGet_false_when_ttl_expired()
    {
        var tracks = new[]
        {
            new OnlineTrack
            {
                Id = "2",
                Title = "X",
                Artist = "Y",
                Platform = MusicPlatform.NetEase
            }
        };
        DiscoverHeroPromoPoolCache.SetForTests(
            DiscoverHeroPromoPoolKind.NewSongs,
            tracks,
            DateTime.UtcNow.AddMinutes(-20));

        Assert.False(DiscoverHeroPromoPoolCache.TryGet(
            DiscoverHeroPromoPoolKind.NewSongs,
            out _,
            DiscoverHeroPromoPoolCache.DefaultTtl));
    }

    [Fact]
    public void Invalidate_clears_all_kinds()
    {
        DiscoverHeroPromoPoolCache.Set(
            DiscoverHeroPromoPoolKind.GuessYouLike,
            [
                new OnlineTrack
                {
                    Id = "3",
                    Title = "G",
                    Artist = "H",
                    Platform = MusicPlatform.NetEase
                }
            ]);
        DiscoverHeroPromoLoader.InvalidatePoolCache();
        Assert.False(DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.GuessYouLike, out _));
    }

    [Fact]
    public void Set_ignores_empty_list()
    {
        DiscoverHeroPromoPoolCache.Set(DiscoverHeroPromoPoolKind.HotSongs, []);
        Assert.False(DiscoverHeroPromoPoolCache.TryGet(DiscoverHeroPromoPoolKind.HotSongs, out _));
    }
}
