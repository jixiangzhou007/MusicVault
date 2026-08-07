using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Search;

/// <summary>在线元数据匹配：去重、打分与排序（供批量刮削与组合标签搜索共用）。</summary>
public static class OnlineMetadataMatcher
{
    public static List<(OnlineTrack Track, int Score)> BuildRankedMatches(
        IReadOnlyList<OnlineTrack> tracks,
        string title,
        string artist,
        MusicPlatform platform,
        string? album = null,
        int maxResults = 100,
        int localDurationMs = 0)
    {
        var titleKey = NormalizeMatchKey(title);
        var artistKey = NormalizeMatchKey(artist);
        var albumKey = NormalizeMatchKey(album);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(OnlineTrack, int)>();

        foreach (var track in tracks)
        {
            if (platform != MusicPlatform.All && track.Platform != platform)
                continue;

            var key = $"{track.Platform}:{track.Id}";
            if (!seen.Add(key))
                continue;

            if (!OnlineTrackQuality.IsViableMetadataCandidate(track))
                continue;

            var matchScore = ScoreMatch(track, titleKey, artistKey, albumKey, platform, localDurationMs);
            list.Add((track, matchScore));
        }

        return list
            .OrderByDescending(x => x.Item2)
            .ThenByDescending(x => OnlineTrackQuality.CompletenessScore(x.Item1))
            .ThenBy(x => x.Item1.Platform)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>将纯匹配分映射为 1–100 的展示百分比（不含元数据完整度加权）。</summary>
    public static int ToDisplayPercent(int scoreMatch)
    {
        if (scoreMatch <= 0)
            return 0;

        // 歌名+歌手精确 + 时长接近 ≈ 300+ 分
        return Math.Clamp((int)Math.Round(scoreMatch * 100.0 / 300.0), 1, 100);
    }

    public static int ScoreMatch(
        OnlineTrack track,
        string titleKey,
        string artistKey,
        string albumKey,
        MusicPlatform preferred,
        int localDurationMs = 0)
    {
        var score = 0;
        var trackTitle = NormalizeMatchKey(track.Title);
        var trackArtist = NormalizeMatchKey(track.Artist);
        var trackAlbum = NormalizeMatchKey(track.Album);

        if (!string.IsNullOrEmpty(titleKey))
        {
            if (trackTitle == titleKey)
                score += 120;
            else if (trackTitle.Contains(titleKey, StringComparison.Ordinal) ||
                     titleKey.Contains(trackTitle, StringComparison.Ordinal))
                score += 60;
            else if (ContainsAllWords(trackTitle, titleKey))
                score += 35;
        }

        if (!string.IsNullOrEmpty(artistKey))
        {
            if (trackArtist == artistKey)
                score += 120;
            else if (ArtistContainsExactToken(trackArtist, artistKey))
                score += 90;
            else if (trackArtist.Contains(artistKey, StringComparison.Ordinal) ||
                     artistKey.Contains(trackArtist, StringComparison.Ordinal))
                score += 45;
            else if (LooksLikeWrongArtistCover(track, artistKey))
                score -= 80;
        }

        if (!string.IsNullOrEmpty(albumKey))
        {
            if (trackAlbum == albumKey)
                score += 70;
            else if (trackAlbum.Contains(albumKey, StringComparison.Ordinal)
                     || albumKey.Contains(trackAlbum, StringComparison.Ordinal))
                score += 35;
        }

        if (preferred != MusicPlatform.All && track.Platform == preferred)
            score += 25;

        if (localDurationMs > 0 && track.DurationMs > 0)
        {
            var diffMs = Math.Abs(localDurationMs - track.DurationMs);
            if (diffMs <= 2_000)
                score += 100;
            else if (diffMs <= 5_000)
                score += 65;
            else if (diffMs <= 12_000)
                score += 30;
            else if (diffMs >= 45_000)
                score -= 55;
        }
        else if (track.DurationMs > 0)
        {
            score += 5;
        }

        return score;
    }

    public static string NormalizeMatchKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private static bool ContainsAllWords(string haystack, string needle)
    {
        foreach (var part in needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!haystack.Contains(part, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool ArtistContainsExactToken(string trackArtist, string artistKey)
    {
        foreach (var token in trackArtist.Split(['、', '/', ',', ';', '，', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(NormalizeMatchKey(token), artistKey, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool LooksLikeWrongArtistCover(OnlineTrack track, string artistKey)
    {
        var title = NormalizeMatchKey(track.Title);
        var artist = NormalizeMatchKey(track.Artist);
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
            return false;

        if (ArtistContainsExactToken(artist, artistKey))
            return false;

        return title.Contains("cover", StringComparison.Ordinal)
               || title.Contains("翻唱", StringComparison.OrdinalIgnoreCase)
               || title.Contains("live", StringComparison.Ordinal)
               || artist.Contains("dj", StringComparison.Ordinal);
    }
}
