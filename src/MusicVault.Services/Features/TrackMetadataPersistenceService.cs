using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services.Metadata;

namespace MusicVault.Services.Features;

/// <summary>元数据保存：写音频文件标签后 upsert 曲库（列表以 DB 为准）。</summary>
public sealed class TrackMetadataPersistenceService
{
    private readonly IMetadataService _metadata;
    private readonly TrackRepository _tracks;

    public TrackMetadataPersistenceService(IMetadataService metadata, TrackRepository tracks)
    {
        _metadata = metadata;
        _tracks = tracks;
    }

    public async Task<Track> SaveFullAsync(
        Track track,
        TrackMetadataEditorFields fields,
        CancellationToken ct = default)
    {
        fields.ApplyTo(track);
        await _metadata.WriteTagsAsync(track, ct).ConfigureAwait(false);
        await _metadata.WriteExtendedTagsAsync(
            track.FilePath,
            fields.Composer,
            fields.Comment,
            fields.DiscNumberText,
            fields.Lyricist,
            ct).ConfigureAwait(false);

        if (fields.CoverImage is { Length: > 0 })
            await _metadata.EmbedCoverAsync(track.FilePath, fields.CoverImage, ct).ConfigureAwait(false);
        else
            await _metadata.ClearCoverAsync(track.FilePath, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(fields.LyricsText))
        {
            await _metadata.ClearLyricsAsync(track.FilePath, ct).ConfigureAwait(false);
            track.LyricsPath = null;
        }
        else
        {
            await _metadata.EmbedLyricsAsync(track.FilePath, fields.LyricsText, ct).ConfigureAwait(false);
            track.LyricsPath = Path.ChangeExtension(track.FilePath, ".lrc");
        }

        TrackMetadataHelper.EnsureDuration(track);
        track.Id = await _tracks.UpsertAsync(track, ct, validateArtistNames: false).ConfigureAwait(false);
        return track;
    }
}
