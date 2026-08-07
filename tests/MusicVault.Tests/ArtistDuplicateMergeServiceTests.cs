using MusicVault.Core.Artists;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class ArtistDuplicateMergeServiceTests
{
    [Fact]
    public void ApplyCanonicalMap_ReplacesAliasWithCanonical()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ArtistNameNormalizer.NormalizeKey("周杰伦.")] = "周杰伦"
        };

        var result = ArtistDuplicateMergeService.ApplyCanonicalMap("周杰伦. / 蔡依林", map);
        Assert.Equal("周杰伦 / 蔡依林", result);
    }

    [Fact]
    public void ApplyCanonicalMap_LeavesUnrelatedArtists()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ArtistNameNormalizer.NormalizeKey("周杰伦-")] = "周杰伦"
        };

        var result = ArtistDuplicateMergeService.ApplyCanonicalMap("张学友", map);
        Assert.Equal("张学友", result);
    }
}
