using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistFieldParserTests
{
    [Fact]
    public void Split_SeparatesCollaborationArtists()
    {
        var names = ArtistFieldParser.Split("张三、李四");
        Assert.Equal(2, names.Count);
        Assert.Contains("张三", names);
        Assert.Contains("李四", names);
    }

    [Fact]
    public void Split_HandlesFeatMarker()
    {
        var names = ArtistFieldParser.Split("周杰伦 feat. 蔡依林");
        Assert.Equal(2, names.Count);
        Assert.Equal("周杰伦", names[0]);
        Assert.Equal("蔡依林", names[1]);
    }

    [Fact]
    public void MatchesArtist_FindsArtistInCombinedField()
    {
        Assert.True(ArtistFieldParser.MatchesArtist("张三 / 李四", "李四"));
        Assert.False(ArtistFieldParser.MatchesArtist("张三 / 李四", "王五"));
    }

    [Fact]
    public void Split_SingleArtist_ReturnsOne()
    {
        var names = ArtistFieldParser.Split("张学友");
        Assert.Single(names);
        Assert.Equal("张学友", names[0]);
    }

    [Fact]
    public void Split_PeelsGuofengCollectivePrefix()
    {
        var names = ArtistFieldParser.Split("国风堂欧楼");
        Assert.Contains("欧楼", names);
        Assert.DoesNotContain("国风堂", names);
    }

    [Fact]
    public void Split_StandaloneCollective_IsFilteredByPerformerClassifier()
    {
        var names = ArtistFieldParser.Split("国风堂");
        Assert.Contains("国风堂", names);
        Assert.False(ArtistPerformerClassifier.IsLikelyPerformer("国风堂"));
    }

    [Fact]
    public void MatchesArtist_FindsMemberBehindCollective()
    {
        Assert.True(ArtistFieldParser.MatchesArtist("国风堂欧楼", "欧楼"));
        Assert.True(ArtistFieldParser.MatchesArtist("国风堂·欧楼", "欧楼"));
    }

    [Fact]
    public void Split_DoesNotBreakNormalArtistName()
    {
        var names = ArtistFieldParser.Split("周杰伦");
        Assert.Single(names);
        Assert.Equal("周杰伦", names[0]);
    }
}
