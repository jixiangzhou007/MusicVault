using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Contracts;
using MusicVault.Services.Features;

namespace MusicVault.Services.Application;

/// <summary>元数据域契约适配：将文件标签写入与入库保存封装为统一结果。</summary>
public sealed class MetadataApplicationService : IMetadataApplicationService
{
    private readonly TrackMetadataPersistenceService _metadataPersistence;

    public MetadataApplicationService(TrackMetadataPersistenceService metadataPersistence) =>
        _metadataPersistence = metadataPersistence;

    public async Task<ApplicationResult<Track>> SaveTrackMetadataAsync(
        Track track,
        TrackMetadataEditorFields fields,
        CancellationToken ct = default)
    {
        if (track == null)
            return ApplicationResult<Track>.Failure(ApplicationErrorCode.ValidationFailed, ServiceMessageCodec.Key("Metadata.TrackRequired"));

        if (fields == null)
            return ApplicationResult<Track>.Failure(ApplicationErrorCode.ValidationFailed, ServiceMessageCodec.Key("Metadata.FieldsRequired"));

        try
        {
            var saved = await _metadataPersistence.SaveFullAsync(track, fields, ct).ConfigureAwait(false);
            return ApplicationResult<Track>.Success(saved);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ApplicationResult<Track>.Failure(
                ApplicationErrorCode.InfrastructureFailure,
                ServiceMessageCodec.Format("General.ExternalDetail", ex.Message));
        }
    }
}
