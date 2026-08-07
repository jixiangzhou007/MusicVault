namespace MusicVault.Core.Browse;

/// <summary>网易云歌手 area 字段 → 浏览分区（见 artist/list 文档）。</summary>
public static class NetEaseArtistAreaMapper
{
    public static ArtistBrowseRegion? Map(int? area) =>
        area switch
        {
            7 => ArtistBrowseRegion.Mandarin,
            8 => ArtistBrowseRegion.Japanese,
            16 => ArtistBrowseRegion.Korean,
            96 => ArtistBrowseRegion.Western,
            0 => ArtistBrowseRegion.Other,
            _ => null
        };
}
