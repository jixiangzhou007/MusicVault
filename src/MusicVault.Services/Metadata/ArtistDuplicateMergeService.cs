using MusicVault.Core.Artists;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Metadata;

/// <summary>按规范化艺术家名合并库内重复条目（曲目字段 + 百科资料）。</summary>
public sealed class ArtistDuplicateMergeService
{
    private readonly TrackRepository _tracks;
    private readonly ArtistProfileRepository _profiles;

    public ArtistDuplicateMergeService(TrackRepository tracks, string databasePath)
    {
        _tracks = tracks;
        _profiles = new ArtistProfileRepository(databasePath);
    }

    public async Task<ArtistDuplicateMergeResult> MergeDuplicateArtistsAsync(CancellationToken ct = default)
    {
        var trackFields = await _tracks.GetArtistFieldsAsync(ct);
        var nameCounts = CountPerformerNames(trackFields);
        var duplicateGroups = BuildDuplicateGroups(nameCounts);
        if (duplicateGroups.Count == 0)
            return new ArtistDuplicateMergeResult();

        var allNames = duplicateGroups.SelectMany(g => g.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var profiles = await _profiles.GetManyAsync(allNames, ct);

        var keyToCanonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groupsMerged = 0;
        var duplicateRecords = 0;

        foreach (var group in duplicateGroups)
        {
            var canonical = PickCanonical(group.Names, profiles, nameCounts);
            keyToCanonical[group.Key] = canonical;
            groupsMerged++;
            duplicateRecords += group.Names.Count - 1;

            foreach (var alias in group.Names)
            {
                if (alias.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                    continue;
                renameMap[alias] = canonical;
            }
        }

        var tracksUpdated = await RewriteTrackArtistFieldsAsync(trackFields, keyToCanonical, ct);
        await MergeProfilesAsync(renameMap, profiles, ct);

        return new ArtistDuplicateMergeResult
        {
            GroupsMerged = groupsMerged,
            DuplicateRecordsMerged = duplicateRecords,
            TracksUpdated = tracksUpdated
        };
    }

    private static Dictionary<string, int> CountPerformerNames(IReadOnlyList<TrackArtistFields> tracks)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in tracks)
        {
            foreach (var field in new[] { track.Artist, track.AlbumArtist })
            {
                foreach (var name in ArtistFieldParser.Split(field))
                {
                    if (!ArtistPerformerClassifier.IsLikelyPerformer(name))
                        continue;
                    counts.TryGetValue(name, out var n);
                    counts[name] = n + 1;
                }
            }
        }

        return counts;
    }

    private static List<DuplicateArtistGroup> BuildDuplicateGroups(Dictionary<string, int> nameCounts)
    {
        var keyToNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in nameCounts.Keys)
        {
            var key = ArtistNameNormalizer.NormalizeKey(name);
            if (key.Length == 0)
                continue;

            if (!keyToNames.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                keyToNames[key] = set;
            }

            set.Add(name);
        }

        return keyToNames
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new DuplicateArtistGroup(kv.Key, kv.Value.ToList()))
            .ToList();
    }

    private static string PickCanonical(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ArtistProfile> profiles,
        IReadOnlyDictionary<string, int> trackCounts)
    {
        return names
            .OrderByDescending(ScoreProfile)
            .ThenByDescending(n => trackCounts.GetValueOrDefault(n))
            .ThenBy(n => ArtistNameNormalizer.EndsWithJunk(n))
            .ThenByDescending(n => n.Length)
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
            .First();

        int ScoreProfile(string name)
        {
            if (!profiles.TryGetValue(name, out var profile))
                return 0;

            var score = 0;
            if (!string.IsNullOrWhiteSpace(profile.PhotoFileName))
                score += 8;
            if (profile.FetchStatus == ArtistProfileFetchStatus.Ok)
                score += 4;
            if (!string.IsNullOrWhiteSpace(profile.Summary))
                score += 2;
            if (!string.IsNullOrWhiteSpace(profile.Description))
                score += 1;
            return score;
        }
    }

    private async Task<int> RewriteTrackArtistFieldsAsync(
        IReadOnlyList<TrackArtistFields> tracks,
        IReadOnlyDictionary<string, string> keyToCanonical,
        CancellationToken ct)
    {
        var updated = 0;
        foreach (var track in tracks)
        {
            var artist = ApplyCanonicalMap(track.Artist, keyToCanonical);
            var albumArtist = ApplyCanonicalMap(track.AlbumArtist, keyToCanonical);
            if (artist == track.Artist && albumArtist == track.AlbumArtist)
                continue;

            await _tracks.UpdateArtistFieldsAsync(track.Id, artist, albumArtist, ct);
            updated++;
        }

        return updated;
    }

    public static string ApplyCanonicalMap(string? field, IReadOnlyDictionary<string, string> keyToCanonical)
    {
        var parts = ArtistFieldParser.Split(field);
        if (parts.Count == 0)
            return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            var key = ArtistNameNormalizer.NormalizeKey(part);
            var name = key.Length > 0 && keyToCanonical.TryGetValue(key, out var canonical)
                ? canonical
                : ArtistNameNormalizer.Normalize(part);
            if (name.Length == 0 || !seen.Add(name))
                continue;
            list.Add(name);
        }

        if (list.Count == 0)
            return string.Empty;
        if (list.Count == 1)
            return list[0];
        return string.Join(" / ", list);
    }

    private async Task MergeProfilesAsync(
        IReadOnlyDictionary<string, string> renameMap,
        IReadOnlyDictionary<string, ArtistProfile> profiles,
        CancellationToken ct)
    {
        foreach (var (from, to) in renameMap)
        {
            profiles.TryGetValue(from, out var aliasProfile);
            profiles.TryGetValue(to, out var canonicalProfile);

            if (aliasProfile == null)
            {
                await _profiles.DeleteAsync(from, ct);
                continue;
            }

            if (ShouldReplaceCanonical(canonicalProfile, aliasProfile))
            {
                await _profiles.UpsertAsync(new ArtistProfile
                {
                    Name = to,
                    Summary = aliasProfile.Summary,
                    Description = aliasProfile.Description,
                    PhotoFileName = aliasProfile.PhotoFileName,
                    SourceUrl = aliasProfile.SourceUrl,
                    SourceName = aliasProfile.SourceName,
                    LemmaId = aliasProfile.LemmaId,
                    FetchStatus = aliasProfile.FetchStatus,
                    ErrorMessage = aliasProfile.ErrorMessage,
                    FetchedAt = aliasProfile.FetchedAt,
                    AlbumsJson = aliasProfile.AlbumsJson,
                    AlbumsFetchedAt = aliasProfile.AlbumsFetchedAt,
                    BrowseRegion = aliasProfile.BrowseRegion
                }, ct);
            }

            await _profiles.DeleteAsync(from, ct);
        }
    }

    private static bool ShouldReplaceCanonical(ArtistProfile? canonical, ArtistProfile alias)
    {
        if (canonical == null)
            return true;

        var canonScore = ProfileScore(canonical);
        var aliasScore = ProfileScore(alias);
        return aliasScore > canonScore;
    }

    private static int ProfileScore(ArtistProfile profile)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(profile.PhotoFileName))
            score += 8;
        if (profile.FetchStatus == ArtistProfileFetchStatus.Ok)
            score += 4;
        if (!string.IsNullOrWhiteSpace(profile.Summary))
            score += 2;
        if (!string.IsNullOrWhiteSpace(profile.Description))
            score += 1;
        return score;
    }

    private sealed record DuplicateArtistGroup(string Key, IReadOnlyList<string> Names);
}
