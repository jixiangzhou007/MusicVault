using MusicVault.Core.Models;

namespace MusicVault.Core.Interfaces;

public interface IMetadataService
{
    Task<Track> ReadTagsAsync(string filePath, CancellationToken ct = default);
    Task WriteTagsAsync(Track track, CancellationToken ct = default);
    Task EmbedCoverAsync(string filePath, byte[] imageData, CancellationToken ct = default);
    Task EmbedLyricsAsync(string filePath, string lrcContent, CancellationToken ct = default);
    Task ClearLyricsAsync(string filePath, CancellationToken ct = default);
    Task<string?> LookupByFingerprintAsync(string filePath, CancellationToken ct = default);
    Task<byte[]?> DownloadCoverAsync(string artist, string album, int size = 600, CancellationToken ct = default);
    Task<string?> DownloadLyricsAsync(string artist, string title, CancellationToken ct = default);
    Task<byte[]?> ReadEmbeddedCoverAsync(string filePath, CancellationToken ct = default);
    Task<(byte[] Data, string MimeType, string PictureTypeLabel)?> ReadEmbeddedCoverDetailsAsync(
        string filePath, CancellationToken ct = default);
    Task ClearCoverAsync(string filePath, CancellationToken ct = default);
    Task<(string Composer, string Comment, string DiscNumberText, string Lyricist)> ReadExtendedTagsAsync(
        string filePath, CancellationToken ct = default);
    Task WriteExtendedTagsAsync(string filePath, string composer, string comment, string? discNumberText,
        string lyricist, CancellationToken ct = default);
    Task<string> ReadLyricsAsync(string filePath, CancellationToken ct = default);
}
