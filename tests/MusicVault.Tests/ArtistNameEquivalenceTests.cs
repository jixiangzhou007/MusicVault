using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistNameEquivalenceTests
{
    [Theory]
    [InlineData("Coco 李玟", "李玟")]
    [InlineData("李玟", "Coco 李玟")]
    [InlineData("G.E.M. 邓紫棋", "邓紫棋")]
    [InlineData("邓紫棋(G.E.M.)", "邓紫棋")]
    [InlineData("邓紫棋(G.E.M.)", "G.E.M.")]
    [InlineData("MC HotDog", "HotDog")]
    public void Matches_treats_stage_names_as_same_artist(string left, string right) =>
        Assert.True(ArtistNameEquivalence.Matches(left, right));

    [Fact]
    public void MatchesArtist_finds_coco_coates_by_chinese_name() =>
        Assert.True(ArtistFieldParser.MatchesArtist("Coco 李玟", "李玟"));

    [Fact]
    public void MatchesArtist_does_not_match_unrelated_embedded_name() =>
        Assert.False(ArtistFieldParser.MatchesArtist("广东周杰伦", "周杰伦"));
}
