using MusicVault.Core.Browse;

namespace MusicVault.App.Browse;

/// <summary>艺术家列表 A–Z 索引（中文按拼音首字母；其余非拉丁归入 #）。</summary>
public static class ArtistSortHelper
{
    public static readonly IReadOnlyList<string> IndexLetters =
        ["#", .. Enumerable.Range('A', 26).Select(c => ((char)c).ToString())];

    public static string GetIndexLetter(string name) => GetSectionKey(name);

    public static string GetSectionKey(string name) => ArtistPinyinHelper.GetFirstIndexLetter(name);

    public static int CompareArtistNames(string? a, string? b)
    {
        var left = a ?? string.Empty;
        var right = b ?? string.Empty;
        var section = CompareSectionKeys(GetSectionKey(left), GetSectionKey(right));
        return section != 0
            ? section
            : string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    public static int CompareSectionKeys(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return 0;

        return GetSectionRank(a).CompareTo(GetSectionRank(b));
    }

    private static int GetSectionRank(string key)
    {
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
            return key[0] - 'A';

        return 26;
    }
}
