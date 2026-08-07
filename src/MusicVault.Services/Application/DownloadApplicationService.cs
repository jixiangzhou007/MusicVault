using MusicVault.Core.Enums;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;

namespace MusicVault.Services.Application;

/// <summary>下载域契约适配：统一错误语义，文案走 <see cref="ServiceMessages"/> 集中管理。</summary>
public sealed class DownloadApplicationService : IDownloadApplicationService
{
    private readonly DownloadQueueService _downloads;

    public DownloadApplicationService(DownloadQueueService downloads) => _downloads = downloads;

    public ApplicationResult<DownloadEnqueueResponse> Enqueue(DownloadEnqueueRequest request)
    {
        if (request.Track == null)
            return ApplicationResult<DownloadEnqueueResponse>.Failure(
                ApplicationErrorCode.ValidationFailed,
                ServiceMessageCodec.Key("Metadata.TrackRequired"));

        var accepted = _downloads.Enqueue(
            request.Track,
            request.Quality,
            request.LibraryId,
            request.TargetDirectory);
        return ApplicationResult<DownloadEnqueueResponse>.Success(
            new DownloadEnqueueResponse(
                accepted,
                accepted
                    ? ServiceMessageCodec.Key("Download.Completed")
                    : ServiceMessageCodec.Key("Download.AlreadyEnqueued")));
    }

    public ApplicationResult<bool> Pause(Guid downloadId)
    {
        var exists = _downloads.Items.Any(i => i.Id == downloadId);
        if (!exists)
            return ApplicationResult<bool>.Failure(
                ApplicationErrorCode.NotFound,
                ServiceMessageCodec.Key("Download.TaskNotFound"));

        _downloads.Pause(downloadId);
        return ApplicationResult<bool>.Success(true);
    }

    public ApplicationResult<bool> Resume(Guid downloadId)
    {
        var exists = _downloads.Items.Any(i => i.Id == downloadId);
        if (!exists)
            return ApplicationResult<bool>.Failure(
                ApplicationErrorCode.NotFound,
                ServiceMessageCodec.Key("Download.TaskNotFound"));

        _downloads.Resume(downloadId);
        return ApplicationResult<bool>.Success(true);
    }

    public ApplicationResult<bool> Cancel(Guid downloadId)
    {
        var exists = _downloads.Items.Any(i => i.Id == downloadId);
        if (!exists)
            return ApplicationResult<bool>.Failure(
                ApplicationErrorCode.NotFound,
                ServiceMessageCodec.Key("Download.TaskNotFound"));

        _downloads.Cancel(downloadId);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<bool>> ClearAllHistoryAsync(CancellationToken ct = default)
    {
        try
        {
            await _downloads.ClearAllHistoryAsync(ct);
            return ApplicationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ApplicationResult<bool>.Failure(ApplicationErrorCode.InfrastructureFailure, ex.Message);
        }
    }

    public IReadOnlyList<MusicVault.Core.Models.DownloadItem> GetQueueSnapshot() => _downloads.Items;

    public Task<IReadOnlyList<MusicVault.Core.Models.DownloadItem>> GetHistoryAsync(CancellationToken ct = default) =>
        _downloads.GetAllHistoryItemsAsync(ct);
}
