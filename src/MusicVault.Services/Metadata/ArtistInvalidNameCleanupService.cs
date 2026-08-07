using MusicVault.Core.Artists;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Metadata;

/// <summary>清理库内无法被音乐平台识别的艺术家名（曲目字段 + 百科资料）。</summary>
public sealed class ArtistInvalidNameCleanupService
{
    private readonly TrackRepository _tracks;
    private readonly ArtistProfileRepository _profiles;
    private readonly IArtistNameValidator _validator;

    public ArtistInvalidNameCleanupService(
        TrackRepository tracks,
        string databasePath,
        IArtistNameValidator validator)
    {
        _tracks = tracks;
        _profiles = new ArtistProfileRepository(databasePath);
        _validator = validator;
    }

    public async Task<ArtistInvalidNameCleanupResult> CleanupInvalidArtistsAsync(
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);
        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        var trackFields = await _tracks.GetArtistFieldsAsync(ct).ConfigureAwait(false);
        var names = CollectPerformerNames(trackFields);
        if (names.Count == 0)
            return new ArtistInvalidNameCleanupResult();

        var invalid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = names.Count;
        var done = 0;
        using var gate = new SemaphoreSlim(4);

        var tasks = names.Select(async name =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var status = await _validator.ValidateArtistNameAsync(name, ct).ConfigureAwait(false);
                if (status == ArtistNameValidationStatus.NotRecognized)
                    lock (invalid) { invalid.Add(name); }
            }
            finally
            {
                gate.Release();
                var current = Interlocked.Increment(ref done);
                progress?.Report((current, total));
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (invalid.Count == 0)
            return new ArtistInvalidNameCleanupResult();

        var invalidSet = invalid.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tracksUpdated = await RewriteTrackArtistFieldsAsync(trackFields, invalidSet, ct).ConfigureAwait(false);

        var profilesDeleted = 0;
        foreach (var name in invalid)
        {
            await _profiles.DeleteAsync(name, ct).ConfigureAwait(false);
            profilesDeleted++;
        }

        return new ArtistInvalidNameCleanupResult
        {
            InvalidNamesRemoved = invalid.Count,
            TracksUpdated = tracksUpdated,
            ProfilesDeleted = profilesDeleted
        };
    }

    private static HashSet<string> CollectPerformerNames(IReadOnlyList<TrackArtistFields> tracks)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in tracks)
        {
            foreach (var field in new[] { track.Artist, track.AlbumArtist })
            {
                foreach (var name in ArtistFieldParser.Split(field))
                {
                    if (!ArtistPerformerClassifier.IsLikelyPerformer(name))
                        continue;
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private async Task<int> RewriteTrackArtistFieldsAsync(
        IReadOnlyList<TrackArtistFields> tracks,
        IReadOnlySet<string> invalidNames,
        CancellationToken ct)
    {
        var updated = 0;
        foreach (var track in tracks)
        {
            var artist = ArtistFieldEditor.RemoveNames(track.Artist, invalidNames);
            var albumArtist = ArtistFieldEditor.RemoveNames(track.AlbumArtist, invalidNames);
            if (artist == track.Artist && albumArtist == track.AlbumArtist)
                continue;

            await _tracks.UpdateArtistFieldsAsync(track.Id, artist, albumArtist, ct).ConfigureAwait(false);
            updated++;
        }

        return updated;
    }
}
