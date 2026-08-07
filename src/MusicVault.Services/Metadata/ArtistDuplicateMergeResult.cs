namespace MusicVault.Services.Metadata;

/// <summary>合并重复艺术家结果。</summary>
public sealed class ArtistDuplicateMergeResult
{
    public int GroupsMerged { get; init; }
    public int DuplicateRecordsMerged { get; init; }
    public int TracksUpdated { get; init; }

    public bool AnyChanges => GroupsMerged > 0 || TracksUpdated > 0;
}
