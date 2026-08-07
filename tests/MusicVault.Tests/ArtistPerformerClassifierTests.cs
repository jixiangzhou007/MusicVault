using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistPerformerClassifierTests
{
    [Theory]
    [InlineData("国风堂", false)]
    [InlineData("汐音社", false)]
    [InlineData("欧楼", true)]
    [InlineData("周杰伦", true)]
    [InlineData("Taylor Swift", true)]
    [InlineData("某某工作室", false)]
    public void IsLikelyPerformer_ClassifiesNames(string name, bool expected) =>
        Assert.Equal(expected, ArtistPerformerClassifier.IsLikelyPerformer(name));

    [Fact]
    public void IsLikelyPerformerFromBaidu_RejectsLabelEntry()
    {
        var fields = new Dictionary<string, string> { ["性质"] = "音乐厂牌" };
        var ok = ArtistPerformerClassifier.IsLikelyPerformerFromBaidu(
            "国风堂",
            "国风堂是一家专注国风音乐的文化厂牌。",
            fields);
        Assert.False(ok);
    }

    [Fact]
    public void IsLikelyPerformerFromBaidu_AcceptsSingerEntry()
    {
        var fields = new Dictionary<string, string> { ["职业"] = "歌手" };
        var ok = ArtistPerformerClassifier.IsLikelyPerformerFromBaidu(
            "欧楼",
            "欧楼，中国内地男歌手。",
            fields);
        Assert.True(ok);
    }
}
