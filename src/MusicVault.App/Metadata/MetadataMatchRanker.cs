using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Search;

namespace MusicVault.App.Metadata;

/// <summary>组合标签搜索：在线结果去重、打分与排序。</summary>
public static class MetadataMatchRanker
{
    public static List<(OnlineTrack Track, int Score)> BuildRankedMatches(
        IReadOnlyList<OnlineTrack> tracks,
        string title,
        string artist,
        MusicPlatform platform,
        string? album = null,
        int maxResults = 100,
        int localDurationMs = 0) =>
        OnlineMetadataMatcher.BuildRankedMatches(
            tracks, title, artist, platform, album, maxResults, localDurationMs);

    public static int ScoreMatch(
        OnlineTrack track,
        string titleKey,
        string artistKey,
        string albumKey,
        MusicPlatform preferred,
        int localDurationMs = 0) =>
        OnlineMetadataMatcher.ScoreMatch(track, titleKey, artistKey, albumKey, preferred, localDurationMs);

    public static string NormalizeMatchKey(string? value) =>
        OnlineMetadataMatcher.NormalizeMatchKey(value);
}
