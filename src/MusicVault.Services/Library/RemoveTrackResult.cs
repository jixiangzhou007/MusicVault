namespace MusicVault.Services.Library;

public sealed record RemoveTrackResult(bool IndexRemoved, bool FileDeleted, bool FileDeleteFailed);
