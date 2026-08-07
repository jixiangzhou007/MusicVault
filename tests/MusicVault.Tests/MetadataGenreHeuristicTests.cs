using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class MetadataGenreHeuristicTests
{
    [Fact]
    public void Rejects_lyric_like_sentence_as_genre()
    {
        const string lyricLine = "我被困在了这片混沌";
        Assert.False(MetadataGenreHeuristic.IsPlausible(lyricLine));
        Assert.Equal(string.Empty, MetadataGenreHeuristic.Sanitize(lyricLine));
    }

    [Fact]
    public void Accepts_known_genre()
    {
        Assert.True(MetadataGenreHeuristic.IsPlausible("流行"));
        Assert.Equal("流行", MetadataGenreHeuristic.Sanitize("流行"));
    }
}
