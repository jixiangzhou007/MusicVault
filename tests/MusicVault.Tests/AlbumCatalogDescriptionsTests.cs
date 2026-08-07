using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class AlbumCatalogDescriptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("发行：音之邦")]
    public void NeedsRichDescription_true_for_stubs(string? text) =>
        Assert.True(AlbumCatalogService.NeedsRichDescription(text));

    [Fact]
    public void NeedsRichDescription_false_for_baidu()
    {
        var text = new string('专', 80);
        Assert.False(AlbumCatalogService.NeedsRichDescription(text, "百度百科"));
    }

    [Fact]
    public void NeedsRichDescription_true_for_netease_track_blurbs()
    {
        var text = string.Join(' ',
            Enumerable.Range(1, 6).Select(i => $"《曲目{i}》是某某演唱的歌曲，讲述爱情与成长。"));
        Assert.True(AlbumCatalogService.NeedsRichDescription(text, "网易云音乐"));
    }

    [Fact]
    public void NormalizeDescription_strips_per_track_blurbs()
    {
        var text = "《带你飞》是某某于2020年发行的专辑，收录了多首作品。"
                   + "《啊我忘了带伞》是专辑第一主打，讲述雨季邂逅。"
                   + "《另一首》是第二主打，描述夏日回忆。";

        var normalized = AlbumCatalogService.NormalizeDescriptionForDisplay(text, "网易云音乐", "带你飞");

        Assert.NotNull(normalized);
        Assert.Contains("2020年发行", normalized);
        Assert.DoesNotContain("啊我忘了带伞", normalized);
        Assert.DoesNotContain("另一首", normalized);
    }

    [Fact]
    public void IsSubstantial_false_for_english_metadata_only_stubs()
    {
        Assert.False(AlbumCatalogDescriptions.IsSubstantial("Genre: Pop · Released: 2020"));
    }
}
