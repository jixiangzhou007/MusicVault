using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistKnownAliasesTests
{
    [Fact]
    public void GetAliasesFor_coco_lee_includes_chinese_name()
    {
        var aliases = ArtistKnownAliases.GetAliasesFor("Coco Lee").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("李玟", aliases);
        Assert.Contains("Coco Lee", aliases);
    }

    [Fact]
    public void Enumerate_lookup_names_for_coco_lee_includes_chinese_name()
    {
        var names = ArtistNameEquivalence.EnumerateLookupNames("Coco Lee").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("李玟", names);
    }
}
