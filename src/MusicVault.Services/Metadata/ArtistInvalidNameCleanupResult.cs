namespace MusicVault.Services.Metadata;

/// <summary>清理无效艺术家名结果。</summary>
public sealed class ArtistInvalidNameCleanupResult
{
    public int InvalidNamesRemoved { get; init; }
    public int TracksUpdated { get; init; }
    public int ProfilesDeleted { get; init; }

    public bool AnyChanges => InvalidNamesRemoved > 0 || TracksUpdated > 0 || ProfilesDeleted > 0;
}
