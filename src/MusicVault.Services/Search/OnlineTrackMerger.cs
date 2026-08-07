using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Search;

/// <summary>合并搜索摘要与曲目详情，优先保留更完整的字段。</summary>
public static class OnlineTrackMerger
{
    public static OnlineTrack Merge(OnlineTrack primary, OnlineTrack? detail)
    {
        if (detail == null)
            return primary;

        return new OnlineTrack
        {
            Id = Prefer(primary.Id, detail.Id),
            Platform = primary.Platform != MusicPlatform.All ? primary.Platform : detail.Platform,
            Title = Prefer(primary.Title, detail.Title),
            Artist = Prefer(primary.Artist, detail.Artist),
            AlbumArtist = Prefer(primary.AlbumArtist, detail.AlbumArtist),
            Album = Prefer(primary.Album, detail.Album),
            AlbumId = Prefer(primary.AlbumId, detail.AlbumId),
            Composer = Prefer(primary.Composer, detail.Composer),
            Year = primary.Year ?? detail.Year,
            Genre = Prefer(primary.Genre, detail.Genre),
            TrackNumber = primary.TrackNumber ?? detail.TrackNumber,
            DiscNumber = primary.DiscNumber ?? detail.DiscNumber,
            DurationMs = primary.DurationMs > 0 ? primary.DurationMs : detail.DurationMs,
            SizeBytes = primary.SizeBytes > 0 ? primary.SizeBytes : detail.SizeBytes,
            BitrateKbps = primary.BitrateKbps > 0 ? primary.BitrateKbps : detail.BitrateKbps,
            CoverUrl = PreferNullable(primary.CoverUrl, detail.CoverUrl),
            PreviewUrl = PreferNullable(primary.PreviewUrl, detail.PreviewUrl),
            DirectDownloadUrl = PreferNullable(primary.DirectDownloadUrl, detail.DirectDownloadUrl),
            HasFlac = primary.HasFlac || detail.HasFlac,
            SupportsMp3128 = primary.SupportsMp3128 || detail.SupportsMp3128,
            SupportsMp3320 = primary.SupportsMp3320 || detail.SupportsMp3320
        };
    }

    private static string Prefer(string primary, string detail) =>
        !string.IsNullOrWhiteSpace(detail) ? detail.Trim()
        : !string.IsNullOrWhiteSpace(primary) ? primary.Trim()
        : string.Empty;

    private static string? PreferNullable(string? primary, string? detail) =>
        !string.IsNullOrWhiteSpace(detail) ? detail
        : !string.IsNullOrWhiteSpace(primary) ? primary
        : null;
}
