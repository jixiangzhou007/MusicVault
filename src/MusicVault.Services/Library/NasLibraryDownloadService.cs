using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Download;
using MusicVault.Services.Organize;

namespace MusicVault.Services.Library;

/// <summary>将 NAS / WebDAV 曲目复制到本地音乐库（经传输队列）。</summary>
public sealed class NasLibraryDownloadService
{
    private readonly LibraryManagerService _libraryManager;
    private readonly TrackRepository _tracks;
    private readonly DownloadQueueService _downloads;
    private readonly WebDavLibraryService _webDavLibraries;
    private readonly FileOrganizerService _organizer = new();

    public NasLibraryDownloadService(
        LibraryManagerService libraryManager,
        TrackRepository tracks,
        DownloadQueueService downloads,
        WebDavLibraryService webDavLibraries)
    {
        _libraryManager = libraryManager;
        _tracks = tracks;
        _downloads = downloads;
        _webDavLibraries = webDavLibraries;
    }

    public async Task<(MusicLibrary Library, bool Enqueued)> EnqueueDownloadToLocalLibraryAsync(
        Track track,
        long? targetLibraryId = null,
        CancellationToken ct = default)
    {
        if (track.Id <= 0)
            throw new InvalidOperationException(ServiceMessageCodec.Key("Nas.Download.TrackNotIndexed"));

        if (!NasPathHelper.IsRemoteLibraryPath(track.FilePath))
            throw new InvalidOperationException(ServiceMessageCodec.Key("Nas.Download.RemotePathOnly"));

        EnsureRemoteSourceReachable(track);

        var targetLibrary = await ResolveTargetLibraryAsync(targetLibraryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Nas.Download.NoLocalTarget"));

        var enqueued = _downloads.EnqueueNasCopyToLibrary(track, targetLibrary, _organizer);
        return (targetLibrary, enqueued);
    }

    public async Task<NasLibraryDownloadBatchResult> EnqueueLibraryDownloadAsync(
        MusicLibrary remoteLibrary,
        long? targetLibraryId = null,
        CancellationToken ct = default)
    {
        if (remoteLibrary.SourceType is not (LibrarySourceType.NetworkNas or LibrarySourceType.NetworkWebDav))
            throw new InvalidOperationException(ServiceMessageCodec.Key("Nas.Download.RemoteLibraryOnly"));

        var targetLibrary = await ResolveTargetLibraryAsync(targetLibraryId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Nas.Download.NoLocalTarget"));

        var tracks = await _tracks
            .GetAllUnderLibraryScopeAsync(remoteLibrary.Id, remoteLibrary.RootPath, ct)
            .ConfigureAwait(false);

        var candidates = tracks.Where(t => NasPathHelper.IsRemoteLibraryPath(t.FilePath)).ToList();
        var remoteOnline = IsRemoteLibraryOnline(remoteLibrary);
        var enqueued = 0;
        var skipped = 0;
        var unreachable = 0;

        foreach (var track in candidates)
        {
            if (!remoteOnline || !IsRemoteTrackReachable(track))
            {
                unreachable++;
                continue;
            }

            if (_downloads.EnqueueNasCopyToLibrary(track, targetLibrary, _organizer))
                enqueued++;
            else
                skipped++;
        }

        return new NasLibraryDownloadBatchResult(
            targetLibrary,
            candidates.Count,
            enqueued,
            skipped,
            unreachable);
    }

    public async Task<IReadOnlyList<MusicLibrary>> GetLocalTargetLibrariesAsync(CancellationToken ct = default)
    {
        var libs = await _libraryManager.GetAllAsync(ct).ConfigureAwait(false);
        return libs
            .Where(l => l.IsEnabled && l.SourceType == LibrarySourceType.Local)
            .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private bool IsRemoteLibraryOnline(MusicLibrary library) =>
        library.SourceType switch
        {
            LibrarySourceType.NetworkNas => Directory.Exists(library.RootPath),
            LibrarySourceType.NetworkWebDav =>
                _webDavLibraries.GetStatus(library.Id)?.Reachability == NasReachability.Online,
            _ => false
        };

    private bool IsRemoteTrackReachable(Track track)
    {
        if (NasPathHelper.IsUncPath(track.FilePath))
            return File.Exists(track.FilePath);

        if (WebDavPathHelper.IsWebDavPath(track.FilePath) && track.LibraryId is > 0)
            return _webDavLibraries.GetStatus(track.LibraryId.Value)?.Reachability == NasReachability.Online;

        return false;
    }

    private void EnsureRemoteSourceReachable(Track track)
    {
        if (NasPathHelper.IsUncPath(track.FilePath))
        {
            if (!File.Exists(track.FilePath))
                throw new FileNotFoundException(ServiceMessageCodec.Key("Nas.Download.SourceUnreachable"), track.FilePath);
            return;
        }

        if (WebDavPathHelper.IsWebDavPath(track.FilePath)
            && track.LibraryId is > 0
            && _webDavLibraries.GetStatus(track.LibraryId.Value)?.Reachability != NasReachability.Online)
        {
            throw new FileNotFoundException(ServiceMessageCodec.Key("Nas.Download.WebDavSourceUnreachable"), track.FilePath);
        }
    }

    private async Task<MusicLibrary?> ResolveTargetLibraryAsync(long? targetLibraryId, CancellationToken ct)
    {
        var locals = await GetLocalTargetLibrariesAsync(ct).ConfigureAwait(false);
        if (locals.Count == 0)
            return null;

        if (targetLibraryId.HasValue)
            return locals.FirstOrDefault(l => l.Id == targetLibraryId.Value) ?? locals[0];

        return locals[0];
    }

    internal static AudioQuality InferQuality(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".flac", StringComparison.OrdinalIgnoreCase)
            ? AudioQuality.Flac
            : AudioQuality.High320;
    }
}

public sealed record NasLibraryDownloadBatchResult(
    MusicLibrary TargetLibrary,
    int TotalCandidates,
    int Enqueued,
    int SkippedDuplicates,
    int Unreachable);
