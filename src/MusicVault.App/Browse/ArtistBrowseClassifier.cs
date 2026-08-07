using MusicVault.Core.Browse;
using MusicVault.Core.Models;

namespace MusicVault.App.Browse;

/// <summary>根据艺术家名称与已缓存资料推断展示分区。</summary>
public static class ArtistBrowseClassifier
{
    public static ArtistRegionFilter ClassifyRegion(string name, ArtistProfile? profile = null) =>
        Map(ArtistRegionClassifier.Classify(name, BuildContext(profile)));

    public static int CountByRegion(
        IEnumerable<string> artistNames,
        ArtistRegionFilter filter,
        IReadOnlyDictionary<string, ArtistProfile>? profiles = null)
    {
        if (filter == ArtistRegionFilter.All)
            return artistNames.Count();

        return artistNames.Count(name =>
        {
            ArtistProfile? profile = null;
            if (profiles != null)
                profiles.TryGetValue(name, out profile);
            return ClassifyRegion(name, profile) == filter;
        });
    }

    public static int CountByRegion(
        IEnumerable<(string Name, ArtistProfile? Profile)> artists,
        ArtistRegionFilter filter)
    {
        if (filter == ArtistRegionFilter.All)
            return artists.Count();

        return artists.Count(item => ClassifyRegion(item.Name, item.Profile) == filter);
    }

    private static ArtistRegionContext? BuildContext(ArtistProfile? profile)
    {
        if (profile == null)
            return null;

        return ArtistRegionContext.FromProfile(
            profile.Summary,
            profile.Description,
            profile.BrowseRegion);
    }

    private static ArtistRegionFilter Map(ArtistBrowseRegion region) => region switch
    {
        ArtistBrowseRegion.Mandarin => ArtistRegionFilter.Mandarin,
        ArtistBrowseRegion.Japanese => ArtistRegionFilter.Japanese,
        ArtistBrowseRegion.Korean => ArtistRegionFilter.Korean,
        ArtistBrowseRegion.Western => ArtistRegionFilter.Western,
        _ => ArtistRegionFilter.Other
    };
}
