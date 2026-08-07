using MusicVault.Core.Browse;

namespace MusicVault.Tests;

public sealed class ArtistRegionClassifierTests
{
    [Theory]
    [InlineData("周杰伦", ArtistBrowseRegion.Mandarin)]
    [InlineData("宇多田ヒカル", ArtistBrowseRegion.Japanese)]
    [InlineData("아이유", ArtistBrowseRegion.Korean)]
    public void ClassifyByName_uses_script(string name, ArtistBrowseRegion expected) =>
        Assert.Equal(expected, ArtistRegionClassifier.ClassifyByName(name));

    [Theory]
    [InlineData("wands")]
    [InlineData("w-inds.")]
    [InlineData("BTS")]
    [InlineData("Taylor Swift")]
    public void ClassifyByName_latin_only_defaults_to_other(string name) =>
        Assert.Equal(ArtistBrowseRegion.Other, ArtistRegionClassifier.ClassifyByName(name));

    [Fact]
    public void Classify_uses_netease_area_for_latin_name()
    {
        var ctx = new ArtistRegionContext { NetEaseArea = 8 };
        Assert.Equal(ArtistBrowseRegion.Japanese, ArtistRegionClassifier.Classify("wands", ctx));
    }

    [Fact]
    public void Classify_uses_stored_region_for_latin_name()
    {
        var ctx = ArtistRegionContext.FromProfile(null, null, ArtistBrowseRegion.Japanese);
        Assert.Equal(ArtistBrowseRegion.Japanese, ArtistRegionClassifier.Classify("wands", ctx));
    }

    [Fact]
    public void Classify_uses_profile_text_for_japanese_artist()
    {
        var ctx = ArtistRegionContext.FromProfile("日本男歌手、演员。", null, null);
        Assert.Equal(ArtistBrowseRegion.Japanese, ArtistRegionClassifier.Classify("wands", ctx));
    }

    [Theory]
    [InlineData("wands", null, true)]
    [InlineData("wands", ArtistBrowseRegion.Western, true)]
    [InlineData("wands", ArtistBrowseRegion.Japanese, false)]
    [InlineData("周杰伦", ArtistBrowseRegion.Western, false)]
    public void NeedsPlatformRegionResolution_detects_pending_latin_names(
        string name, ArtistBrowseRegion? stored, bool expected) =>
        Assert.Equal(expected, ArtistRegionClassifier.NeedsPlatformRegionResolution(name, stored));
}
