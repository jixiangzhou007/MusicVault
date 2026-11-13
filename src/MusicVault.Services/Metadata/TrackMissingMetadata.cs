using MusicVault.Core.Models;

namespace MusicVault.Services.Metadata;

public static class TrackMissingMetadata
{
    public static bool HasCoverSignal(Track t) => !string.IsNullOrWhiteSpace(t.CoverHash);
    public static bool HasLyricsSignal(Track t) => !string.IsNullOrWhiteSpace(t.LyricsPath);
    public static bool IsMissingCover(Track t) => !HasCoverSignal(t);
    public static bool IsMissingLyrics(Track t) => !HasLyricsSignal(t);
}
