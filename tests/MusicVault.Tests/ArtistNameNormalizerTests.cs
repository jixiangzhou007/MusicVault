using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistNameNormalizerTests
{
    [Theory]
    [InlineData("周杰伦", "周杰伦")]
    [InlineData("周杰伦.", "周杰伦")]
    [InlineData("周杰伦-", "周杰伦")]
    [InlineData("周杰伦_", "周杰伦")]
    [InlineData("  周杰伦  ", "周杰伦")]
    [InlineData("周　杰　伦", "周 杰 伦")]
    [InlineData("Ｊａｙ", "Jay")]
    public void Normalize_StripsTrailingJunkAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, ArtistNameNormalizer.Normalize(input));
    }

    [Fact]
    public void NormalizeKey_IsCaseInsensitive()
    {
        Assert.Equal(
            ArtistNameNormalizer.NormalizeKey("Taylor Swift"),
            ArtistNameNormalizer.NormalizeKey("taylor swift"));
    }

    [Fact]
    public void NormalizeField_SplitsFeatAndNormalizesParts()
    {
        var field = ArtistNameNormalizer.NormalizeField("周杰伦 feat. 蔡依林");
        Assert.Equal("周杰伦 / 蔡依林", field);
    }

    [Fact]
    public void NormalizeField_DeduplicatesAfterNormalization()
    {
        var field = ArtistNameNormalizer.NormalizeField("周杰伦 / 周杰伦.");
        Assert.Equal("周杰伦", field);
    }

    [Theory]
    [InlineData("周杰伦", "周杰伦.", true)]
    [InlineData("周杰伦.", "周杰伦", false)]
    [InlineData("周杰伦", "周杰伦-", true)]
    public void PreferDisplayName_PrefersCleanerName(string candidate, string current, bool expectPrefer)
    {
        Assert.Equal(expectPrefer, ArtistNameNormalizer.PreferDisplayName(candidate, current));
    }

    [Fact]
    public void Split_NormalizesTrailingPunctuationInSingleArtist()
    {
        var names = ArtistFieldParser.Split("周杰伦.");
        Assert.Single(names);
        Assert.Equal("周杰伦", names[0]);
    }
}
