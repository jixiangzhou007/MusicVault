using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>在线搜索结果曲目</summary>
public sealed class OnlineTrack
{
    public string Id { get; set; } = string.Empty;
    public MusicPlatform Platform { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    /// <summary>平台歌手 ID（如网易云 ar.id）。</summary>
    public string ArtistId { get; set; } = string.Empty;
    /// <summary>专辑艺术家（部分平台与 <see cref="Artist"/> 相同）</summary>
    public string AlbumArtist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    /// <summary>平台专辑 ID，用于补全年份/封面等</summary>
    public string AlbumId { get; set; } = string.Empty;
    /// <summary>作曲/作词等（统一 API extra 或平台详情）</summary>
    public string Composer { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationMs { get; set; }
    /// <summary>搜索/API 返回的参考文件大小（字节），用于列表展示与音质推断。</summary>
    public long SizeBytes { get; set; }
    /// <summary>参考码率（kbps），可由 API 或 size/duration 推算。</summary>
    public int BitrateKbps { get; set; }
    public string? CoverUrl { get; set; }
    public string? PreviewUrl { get; set; }
    /// <summary>搜索阶段已解析到的直链（聚合站），下载时优先使用</summary>
    public string? DirectDownloadUrl { get; set; }
    public bool HasFlac { get; set; }
    /// <summary>该曲目是否可能有 MP3 128K 下载源。</summary>
    public bool SupportsMp3128 { get; set; }
    /// <summary>该曲目是否可能有 MP3 320K 下载源。</summary>
    public bool SupportsMp3320 { get; set; }

    /// <summary>MyFreeMP3 夸克分享页（下载时解析；需在偏好配置夸克 Cookie）。</summary>
    public string? QuarkShareUrl { get; set; }

    /// <summary>额外下载选项（多通道/多直链），与标准 128/320/FLAC chip 合并展示。</summary>
    public List<OnlineTrackDownloadVariant> DownloadVariants { get; set; } = [];
}
