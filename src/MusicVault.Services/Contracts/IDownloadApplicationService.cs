using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Contracts;

public sealed record DownloadEnqueueRequest(
    OnlineTrack Track,
    AudioQuality Quality,
    long? LibraryId = null,
    string? TargetDirectory = null);

public sealed record DownloadEnqueueResponse(bool Accepted, string Message);

/// <summary>下载域应用层契约：队列生命周期与历史台账。</summary>
public interface IDownloadApplicationService
{
    ApplicationResult<DownloadEnqueueResponse> Enqueue(DownloadEnqueueRequest request);
    ApplicationResult<bool> Pause(Guid downloadId);
    ApplicationResult<bool> Resume(Guid downloadId);
    ApplicationResult<bool> Cancel(Guid downloadId);
    Task<ApplicationResult<bool>> ClearAllHistoryAsync(CancellationToken ct = default);
    IReadOnlyList<DownloadItem> GetQueueSnapshot();
    Task<IReadOnlyList<DownloadItem>> GetHistoryAsync(CancellationToken ct = default);
}
