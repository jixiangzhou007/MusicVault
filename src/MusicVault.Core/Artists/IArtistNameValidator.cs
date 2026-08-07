namespace MusicVault.Core.Artists;

/// <summary>校验艺术家名是否被外部音乐平台（网易云/QQ 音乐）识别。</summary>
public interface IArtistNameValidator
{
    Task<ArtistNameValidationStatus> ValidateArtistNameAsync(string name, CancellationToken ct = default);

    Task<bool> IsRecognizedArtistAsync(string name, CancellationToken ct = default);

    Task<string> FilterArtistFieldAsync(string? artistField, CancellationToken ct = default);
}
