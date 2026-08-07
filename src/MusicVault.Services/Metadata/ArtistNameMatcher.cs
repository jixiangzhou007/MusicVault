using MusicVault.Core.Artists;

namespace MusicVault.Services.Metadata;

internal static class ArtistNameMatcher
{
    /// <summary>专辑/歌单的 creator 字段：按拆分后的独立名匹配，避免误匹配。</summary>
    public static bool IsCreatorMatch(string query, string? creator) =>
        ArtistFieldParser.MatchesArtist(creator, query);

    public static bool IsMatch(string query, string? candidate) =>
        ArtistNameFuzzy.IsLooseMatch(query, candidate);

    public static int Similarity(string query, string? candidate) =>
        ArtistNameFuzzy.Similarity(query, candidate);
}
