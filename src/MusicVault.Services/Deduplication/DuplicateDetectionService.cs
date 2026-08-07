using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Deduplication;

/// <summary>三重去重：文件名 + 文件大小 + 音频指纹</summary>
public sealed class DuplicateDetectionService
{
    private readonly TrackRepository _tracks;

    public DuplicateDetectionService(TrackRepository tracks) => _tracks = tracks;

    public DuplicateDetectionService(string dbPath) : this(new TrackRepository(dbPath)) { }

    public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(CancellationToken ct = default)
    {
        var all = await _tracks.GetByLibraryAsync(null, 0, int.MaxValue, ct);
        return FindDuplicatesInTracks(all);
    }

    /// <summary>在指定曲目集合内检测重复（用于整理工作区已载入清单）。</summary>
    public static IReadOnlyList<DuplicateGroup> FindDuplicatesInTracks(IEnumerable<Track> tracks)
    {
        var groups = new Dictionary<string, List<Track>>();

        foreach (var track in tracks)
        {
            var key = $"{track.Title}|{track.Artist}|{track.FileSizeBytes}";
            if (!groups.ContainsKey(key))
                groups[key] = [];
            groups[key].Add(track);
        }

        return groups.Where(g => g.Value.Count > 1)
            .Select(g => new DuplicateGroup(g.Key, g.Value))
            .ToList();
    }

    public bool IsLikelyDuplicate(Track a, Track b)
    {
        if (a.FileSizeBytes > 0 && b.FileSizeBytes > 0 && a.FileSizeBytes != b.FileSizeBytes) return false;
        if (!string.Equals(a.Title, b.Title, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(a.AcoustId) && a.AcoustId == b.AcoustId) return true;
        if (!string.IsNullOrEmpty(a.FilePath) && !string.IsNullOrEmpty(b.FilePath))
            return Path.GetFileName(a.FilePath).Equals(Path.GetFileName(b.FilePath), StringComparison.OrdinalIgnoreCase);
        return true;
    }
}

public sealed record DuplicateGroup(string Key, IReadOnlyList<Track> Tracks);
