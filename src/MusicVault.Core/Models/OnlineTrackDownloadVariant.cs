using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>单曲额外下载选项（多通道直链、夸克分享等）。</summary>
public sealed class OnlineTrackDownloadVariant
{
    /// <summary>内部键，如 netease-128、quark-flac。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>发现页操作列 chip 文案。</summary>
    public string Label { get; set; } = string.Empty;

    public AudioQuality Quality { get; set; }

    /// <summary>已解析的 HTTP 直链；下载时优先使用。</summary>
    public string? DirectDownloadUrl { get; set; }

    /// <summary>夸克网盘分享页 URL（需有效 Cookie 解析）。</summary>
    public string? QuarkShareUrl { get; set; }

    /// <summary>网易曲目 ID（夸克合并到网易行时用于 128K 外链）。</summary>
    public string? NetEaseTrackId { get; set; }
}
