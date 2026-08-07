using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistLookupNameVariantsTests
{
    [Theory]
    [InlineData("康姆士乐团", "康姆士乐团", "康姆士")]
    [InlineData("逃跑计划乐队", "逃跑计划乐队", "逃跑计划")]
    [InlineData("某某乐队乐团", "某某乐队乐团", "某某乐队", "某某")]
    public void Enumerate_strips_trailing_band_suffixes(string input, params string[] expected)
    {
        var actual = ArtistLookupNameVariants.Enumerate(input).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Enumerate_includes_chinese_and_latin_parts_for_bilingual_name()
    {
        var names = ArtistLookupNameVariants.Enumerate("Coco 李玟").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Coco 李玟", names);
        Assert.Contains("李玟", names);
        Assert.Contains("Coco", names);
    }

    [Fact]
    public void Enumerate_for_zhou_jielun_includes_known_english_aliases()
    {
        var names = ArtistLookupNameVariants.Enumerate("周杰伦").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("周杰伦", names);
        Assert.Contains("Jay Chou", names);
    }

    [Fact]
    public void Enumerate_includes_parenthetical_alias()
    {
        var names = ArtistLookupNameVariants.Enumerate("邓紫棋(G.E.M.)").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("邓紫棋", names);
        Assert.Contains(names, n => n.Contains("G.E.M", StringComparison.OrdinalIgnoreCase));
    }
}
