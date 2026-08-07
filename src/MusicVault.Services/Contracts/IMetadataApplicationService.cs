using MusicVault.Core.Models;
using MusicVault.Services.Features;

namespace MusicVault.Services.Contracts;

/// <summary>元数据域应用层契约：以“写标签 + 入库”作为原子业务动作。</summary>
public interface IMetadataApplicationService
{
    Task<ApplicationResult<Track>> SaveTrackMetadataAsync(
        Track track,
        TrackMetadataEditorFields fields,
        CancellationToken ct = default);
}
