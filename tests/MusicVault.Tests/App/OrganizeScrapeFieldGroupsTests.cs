using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class OrganizeScrapeFieldGroupsTests
{
    [Fact]
    public void Basic_fields_are_title_artist_album_year_cover_lyrics()
    {
        var basic = OrganizeScrapeFieldGroups.Basic;
        Assert.Equal(
            new[] { "title", "artist", "album", "year", "cover", "lyrics" },
            basic);
    }

    [Fact]
    public void Extended_fields_are_albumArtist_genre_track_composer()
    {
        Assert.Equal(
            new[] { "albumArtist", "genre", "trackNumber", "composer" },
            OrganizeScrapeFieldGroups.Extended);
    }

    [Fact]
    public void Basic_and_extended_are_disjoint_and_cover_all()
    {
        var all = OrganizeScrapeFieldGroups.Basic
            .Concat(OrganizeScrapeFieldGroups.Extended)
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(all.Distinct().Count(), all.Length);
        Assert.Equal(10, all.Length);
    }
}
