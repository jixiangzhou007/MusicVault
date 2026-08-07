namespace MusicVault.App.ViewModels.Pages;

/// <summary>艺术家列表 A–Z / 首字分区标题行。</summary>
public sealed class ArtistSectionHeaderViewModel
{
    public required string Letter { get; init; }
    public required int ArtistCount { get; init; }
}
