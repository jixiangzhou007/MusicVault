namespace MusicVault.Core.Models;

/// <summary>专辑分组（用于专辑视图）</summary>
public sealed class AlbumGroup
{
    public string Album { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public int TrackCount { get; init; }
    public int Year { get; init; }
}
