using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Features;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class ArtistAlbumCatalogBuilderTests
{
    private const string Artist = "周杰伦";

    [Fact]
    public void Build_MergesLibraryAndOfficialAlbums()
    {
        var library = new[]
        {
            Track("七里香", "七里香", 1, 2004),
            Track("我的地盘", "七里香", 2, 2004),
            Track("搁浅", "七里香", 3, 2004),
            Track("晴天", "叶惠美", 1, 2003),
        };
        var official = new[]
        {
            Official("七里香", "album-7", 11, 2004),
            Official("叶惠美", "album-y", 11, 2003),
            Official("依然范特西", "album-f", 10, 2006),
        };

        var items = ArtistAlbumCatalogBuilder.Build(library, Artist, official);

        Assert.Contains(items, i => i.Album == "七里香" && i.LibraryTrackCount == 3 && i.PlatformAlbumId == "album-7");
        Assert.Contains(items, i => i.Album == "叶惠美" && i.LibraryTrackCount == 1);
        Assert.Contains(items, i => i.Album == "依然范特西" && i.LibraryTrackCount == 0);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void Build_FiltersNonOfficialOnlineReleases()
    {
        var library = Array.Empty<Track>();
        var official = new[]
        {
            Official("晴天", "single", 1, 2003, albumType: "单曲"),
            Official("叶惠美", "album-y", 11, 2003),
        };

        var items = ArtistAlbumCatalogBuilder.Build(library, Artist, official);

        Assert.Single(items);
        Assert.Equal("叶惠美", items[0].Album);
    }

    [Fact]
    public void Build_EmptyOfficial_KeepsLibraryAlbums()
    {
        var library = new[]
        {
            Track("爱在西元前", "范特西", 1, 2001),
            Track("简单爱", "范特西", 2, 2001),
        };

        var items = ArtistAlbumCatalogBuilder.Build(library, Artist, []);

        Assert.Single(items);
        Assert.Equal("范特西", items[0].Album);
        Assert.Equal(2, items[0].LibraryTrackCount);
        Assert.Null(items[0].PlatformAlbumId);
    }

    [Fact]
    public void Build_EmptyOfficial_DoesNotReturnEmptyWhenLibraryHasAlbums()
    {
        var library = new[]
        {
            Track("Intro", "八度空间", 1, 2002),
            Track("半兽人", "八度空间", 2, 2002),
        };

        var items = ArtistAlbumCatalogBuilder.Build(library, Artist, Array.Empty<OnlineArtistAlbum>());

        Assert.NotEmpty(items);
        Assert.Equal("八度空间", items[0].Album);
    }

    [Fact]
    public void ResolveSourceHint_EmptyOfficial_ReturnsLibraryHint()
    {
        Assert.Equal(
            ServiceMessageCodec.Key("Album.Source.LibraryAggregated"),
            ArtistAlbumCatalogBuilder.ResolveSourceHint([]));
    }

    [Fact]
    public void ResolveSourceHint_WithOfficial_ReturnsNetEaseHint()
    {
        var official = new[] { Official("叶惠美", "id", 11, 2003) };
        Assert.Equal(
            ServiceMessageCodec.Key("Album.Source.NetEaseMatched"),
            ArtistAlbumCatalogBuilder.ResolveSourceHint(official));
    }

    private static Track Track(string title, string album, int number, int year) => new()
    {
        Title = title,
        Album = album,
        Artist = Artist,
        TrackNumber = number,
        Year = year
    };

    private static OnlineArtistAlbum Official(
        string name,
        string id,
        int trackCount,
        int year,
        string albumType = "专辑") =>
        new(name, "https://example.com/pic.jpg", year, id, trackCount, albumType, "录音室版");
}
