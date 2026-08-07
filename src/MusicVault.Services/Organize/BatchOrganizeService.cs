using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Organize;

/// <summary>批量文件整理：预览 + 执行</summary>
public sealed class BatchOrganizeService
{
    private readonly FileOrganizerService _organizer = new();
    private readonly TrackRepository _tracks;

    public BatchOrganizeService(TrackRepository tracks) => _tracks = tracks;

    public OrganizePreview PreviewTrack(
        Track track,
        string rootPath,
        string? folderTemplate,
        string? fileNameTemplate) =>
        _organizer.PreviewOrganize(track, rootPath, folderTemplate, fileNameTemplate);

    /// <summary>为曲目列表生成整理预览</summary>
    public IReadOnlyList<OrganizePreview> PreviewBatch(
        IReadOnlyList<Track> tracks,
        string rootPath,
        string? folderTemplate,
        string? fileNameTemplate)
    {
        return tracks
            .Select(t => _organizer.PreviewOrganize(t, rootPath, folderTemplate, fileNameTemplate))
            .ToList();
    }

    /// <summary>执行批量整理并更新数据库路径</summary>
    public async Task<OrganizeResult> ExecuteBatchAsync(
        IReadOnlyList<OrganizePreview> previews,
        bool move,
        CancellationToken ct = default)
    {
        var result = new OrganizeResult();
        foreach (var preview in previews)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (string.Equals(preview.SourcePath, preview.TargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    result.Skipped++;
                    continue;
                }
                await _organizer.ExecuteOrganizeAsync(preview, move, ct);
                var track = await _tracks.GetByPathAsync(preview.SourcePath, ct);
                if (track != null)
                {
                    track.FilePath = preview.TargetPath;
                    await _tracks.UpsertAsync(track, ct);
                }
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

public sealed class OrganizeResult
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}
