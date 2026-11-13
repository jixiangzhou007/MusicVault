namespace MusicVault.App.Services;

/// <summary>刮削底栏：默认展示基础字段；扩展字段折叠。</summary>
public static class OrganizeScrapeFieldGroups
{
    public static IReadOnlyList<string> Basic { get; } =
        ["title", "artist", "album", "year", "cover", "lyrics"];

    public static IReadOnlyList<string> Extended { get; } =
        ["albumArtist", "genre", "trackNumber", "composer"];
}
