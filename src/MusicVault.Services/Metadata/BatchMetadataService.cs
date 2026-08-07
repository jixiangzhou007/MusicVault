using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Metadata;

/// <summary>批量元数据编辑与查找替换</summary>
public sealed class BatchMetadataService
{
    private readonly IMetadataService _metadata;
    private readonly TrackRepository _tracks;

    public BatchMetadataService(IMetadataService metadata, TrackRepository tracks)
    {
        _metadata = metadata;
        _tracks = tracks;
    }

    /// <summary>批量写入标签到多个文件</summary>
    public async Task<int> ApplyBatchEditAsync(
        IReadOnlyList<Track> tracks,
        Action<Track> editAction,
        CancellationToken ct = default)
    {
        var count = 0;
        foreach (var track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            editAction(track);
            await _metadata.WriteTagsAsync(track, ct);
            await _tracks.UpsertAsync(track, ct);
            count++;
        }
        return count;
    }

    /// <summary>查找替换：在指定字段中替换文本</summary>
    public async Task<int> FindReplaceAsync(
        IReadOnlyList<Track> tracks,
        string fieldName,
        string find,
        string replace,
        bool ignoreCase = true,
        CancellationToken ct = default)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return await ApplyBatchEditAsync(tracks, track =>
        {
            switch (fieldName.ToLowerInvariant())
            {
                case "title":
                    if (track.Title.Contains(find, comparison))
                        track.Title = track.Title.Replace(find, replace, comparison);
                    break;
                case "artist":
                    if (track.Artist.Contains(find, comparison))
                        track.Artist = track.Artist.Replace(find, replace, comparison);
                    break;
                case "album":
                    if (track.Album.Contains(find, comparison))
                        track.Album = track.Album.Replace(find, replace, comparison);
                    break;
                case "genre":
                    if (track.Genre.Contains(find, comparison))
                        track.Genre = track.Genre.Replace(find, replace, comparison);
                    break;
            }
        }, ct);
    }

    /// <summary>批量刮削：为选中曲目下载封面与歌词</summary>
    public async Task<BatchScrapeResult> BatchScrapeAsync(
        IReadOnlyList<Track> tracks,
        bool embedCover = true,
        bool embedLyrics = true,
        int coverSize = 600,
        CancellationToken ct = default)
    {
        var result = new BatchScrapeResult();
        foreach (var track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (embedCover)
                {
                    var cover = await _metadata.DownloadCoverAsync(track.Artist, track.Album, coverSize, ct);
                    if (cover != null)
                    {
                        await _metadata.EmbedCoverAsync(track.FilePath, cover, ct);
                        result.CoversEmbedded++;
                    }
                }
                if (embedLyrics)
                {
                    var lrc = await _metadata.DownloadLyricsAsync(track.Artist, track.Title, ct);
                    if (lrc != null)
                    {
                        await _metadata.EmbedLyricsAsync(track.FilePath, lrc, ct);
                        track.LyricsPath = Path.ChangeExtension(track.FilePath, ".lrc");
                        result.LyricsEmbedded++;
                    }
                }
                await _tracks.UpsertAsync(track, ct);
                result.Success++;
            }
            catch
            {
                result.Failed++;
            }
        }
        return result;
    }
}

/// <summary>批量刮削结果</summary>
public sealed class BatchScrapeResult
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int CoversEmbedded { get; set; }
    public int LyricsEmbedded { get; set; }
}
