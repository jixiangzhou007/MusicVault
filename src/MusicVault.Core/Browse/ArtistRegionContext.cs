namespace MusicVault.Core.Browse;

/// <summary>艺术家分区推断的附加线索（平台 area、百科简介等）。</summary>
public sealed class ArtistRegionContext
{
    public ArtistBrowseRegion? StoredRegion { get; init; }
    public string? ProfileText { get; init; }
    public int? NetEaseArea { get; init; }

    public static ArtistRegionContext FromProfile(string? summary, string? description, ArtistBrowseRegion? stored, int? netEaseArea = null)
    {
        var text = string.Join(' ',
            new[] { summary, description }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ArtistRegionContext
        {
            StoredRegion = stored,
            ProfileText = string.IsNullOrWhiteSpace(text) ? null : text,
            NetEaseArea = netEaseArea
        };
    }
}
