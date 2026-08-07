namespace MusicVault.Core.Nas;

public enum NasReachability
{
    Unknown,
    Online,
    Offline
}

public sealed record NasLibraryStatus(
    long LibraryId,
    NasReachability Reachability,
    string Message,
    DateTime CheckedAtUtc);
