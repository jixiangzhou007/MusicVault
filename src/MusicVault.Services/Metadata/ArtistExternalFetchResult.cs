namespace MusicVault.Services.Metadata;

/// <summary>外部抓取的艺术家资料（百科 / 音乐平台）。</summary>
public sealed record ArtistExternalFetchResult(
    string? Summary,
    string? Description,
    string? PhotoUrl,
    string? SourceUrl,
    string? LemmaId,
    string SourceName,
    int? NetEaseArea = null);
