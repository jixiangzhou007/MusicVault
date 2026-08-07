namespace MusicVault.Core.Models;

/// <summary>
/// 本地或在线曲目实体。
/// </summary>
/// <remarks>
/// <para><b>身份属性</b>（创建后不应修改，否则影响索引一致性）：
/// <see cref="FilePath"/>、<see cref="AddedAt"/>。</para>
/// <para><b>元数据属性</b>（扫描/导入阶段可更新，播放阶段只读）：
/// <see cref="Title"/>、<see cref="Artist"/>、<see cref="Album"/>、<see cref="AlbumArtist"/>、
/// <see cref="TrackNumber"/>、<see cref="Year"/>、<see cref="Genre"/>、
/// <see cref="DurationMs"/>、<see cref="FileSizeBytes"/>、
/// <see cref="AcoustId"/>、<see cref="CoverHash"/>。</para>
/// <para><b>可变统计</b>（随播放/编辑更新）：
/// <see cref="PlayCount"/>、<see cref="LastPlayedAt"/>、<see cref="LastPositionSeconds"/>、
/// <see cref="LyricsPath"/>、<see cref="LibraryId"/>。</para>
/// <para>需要修改元数据属性的场合，优先使用 <see cref="With"/> 创建新实例以明确变更意图。</para>
/// </remarks>
public sealed class Track
{
    // ── 身份属性 ──
    public long Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // ── 元数据属性 ──
    public long? LibraryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public int? TrackNumber { get; set; }
    public int? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public long FileSizeBytes { get; set; }
    public string? AcoustId { get; set; }
    public string? CoverHash { get; set; }

    // ── 可变统计 ──
    public string? LyricsPath { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public int PlayCount { get; set; }
    public double LastPositionSeconds { get; set; }

    /// <summary>
    /// 返回一个修改了指定字段的新实例（不可变更新模式）。
    /// 未指定的字段保留原值。可变统计字段（PlayCount 等）可通过此方法一并设置。
    /// </summary>
    public Track With(
        long? id = null,
        long? libraryId = null,
        string? filePath = null,
        string? title = null,
        string? artist = null,
        string? album = null,
        string? albumArtist = null,
        int? trackNumber = null,
        int? year = null,
        string? genre = null,
        int? durationMs = null,
        long? fileSizeBytes = null,
        string? acoustId = null,
        string? coverHash = null,
        DateTime? addedAt = null,
        string? lyricsPath = null,
        DateTime? lastPlayedAt = null,
        int? playCount = null,
        double? lastPositionSeconds = null)
    {
        return new Track
        {
            Id = id ?? Id,
            LibraryId = libraryId ?? LibraryId,
            FilePath = filePath ?? FilePath,
            Title = title ?? Title,
            Artist = artist ?? Artist,
            Album = album ?? Album,
            AlbumArtist = albumArtist ?? AlbumArtist,
            TrackNumber = trackNumber ?? TrackNumber,
            Year = year ?? Year,
            Genre = genre ?? Genre,
            DurationMs = durationMs ?? DurationMs,
            FileSizeBytes = fileSizeBytes ?? FileSizeBytes,
            AcoustId = acoustId ?? AcoustId,
            CoverHash = coverHash ?? CoverHash,
            AddedAt = addedAt ?? AddedAt,
            LyricsPath = lyricsPath ?? LyricsPath,
            LastPlayedAt = lastPlayedAt ?? LastPlayedAt,
            PlayCount = playCount ?? PlayCount,
            LastPositionSeconds = lastPositionSeconds ?? LastPositionSeconds,
        };
    }
}
