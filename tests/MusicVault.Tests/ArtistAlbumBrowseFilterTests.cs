using MusicVault.Core.Browse;
using MusicVault.Core.Models;

namespace MusicVault.Tests;

public sealed class ArtistAlbumBrowseFilterTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown")]
    [InlineData("Various Artists")]
    [InlineData("未知专辑")]
    [InlineData("—")]
    public void IsDisplayableName_RejectsPlaceholders(string album) =>
        Assert.False(ArtistAlbumBrowseFilter.IsDisplayableName(album));

    [Theory]
    [InlineData("叶惠美")]
    [InlineData("The Dark Side of the Moon")]
    public void IsDisplayableName_AcceptsRealAlbums(string album) =>
        Assert.True(ArtistAlbumBrowseFilter.IsDisplayableName(album));

    [Fact]
    public void IsDisplayableLibraryAlbum_RejectsAlbumEqualsArtist()
    {
        var tracks = new[] { new Track { Title = "Song A", Album = "周杰伦" } };
        Assert.False(ArtistAlbumBrowseFilter.IsDisplayableLibraryAlbum("周杰伦", "周杰伦", tracks));
    }

    [Fact]
    public void IsDisplayableLibraryAlbum_RejectsTitlePseudoAlbum()
    {
        var tracks = new[]
        {
            new Track { Title = "晴天", Album = "晴天" },
            new Track { Title = "晴天", Album = "晴天" }
        };
        Assert.False(ArtistAlbumBrowseFilter.IsDisplayableLibraryAlbum("晴天", "周杰伦", tracks));
    }

    [Fact]
    public void IsDisplayableLibraryAlbum_AcceptsMultiTrackAlbum()
    {
        var tracks = new[]
        {
            new Track { Title = "Intro", Album = "范特西" },
            new Track { Title = "爱在西元前", Album = "范特西" }
        };
        Assert.True(ArtistAlbumBrowseFilter.IsDisplayableLibraryAlbum("范特西", "周杰伦", tracks));
    }

    [Theory]
    [InlineData("Disc 1")]
    [InlineData("CD2")]
    public void IsDisplayableOnlineAlbum_RejectsDiscLabels(string album) =>
        Assert.False(ArtistAlbumBrowseFilter.IsDisplayableOnlineAlbum(album));
}
