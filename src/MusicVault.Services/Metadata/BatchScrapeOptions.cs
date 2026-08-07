namespace MusicVault.Services.Metadata;

/// <summary>批量在线刮削选项：标签字段与媒体嵌入。</summary>
public sealed class BatchScrapeOptions
{
    public bool UpdateTitle { get; set; } = true;
    public bool UpdateArtist { get; set; } = true;
    public bool UpdateAlbum { get; set; } = true;
    public bool UpdateAlbumArtist { get; set; } = true;
    public bool UpdateYear { get; set; } = true;
    public bool UpdateGenre { get; set; } = true;
    public bool UpdateTrackNumber { get; set; } = true;
    public bool UpdateComposer { get; set; }
    public bool EmbedCover { get; set; } = true;
    public bool EmbedLyrics { get; set; } = true;

    public bool UpdatesAnyTag =>
        UpdateTitle || UpdateArtist || UpdateAlbum || UpdateAlbumArtist
        || UpdateYear || UpdateGenre || UpdateTrackNumber || UpdateComposer;
}

/// <summary>单曲刮削结果。</summary>
public sealed class BatchScrapeTrackResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool TagsWritten { get; init; }
    public bool CoverEmbedded { get; init; }
    public bool LyricsEmbedded { get; init; }
}
