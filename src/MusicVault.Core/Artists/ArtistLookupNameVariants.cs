using System.Text;
using System.Text.RegularExpressions;

namespace MusicVault.Core.Artists;

/// <summary>生成艺术家检索/匹配用名称变体（别名、中英分段、前缀、括号内外文名等）。</summary>
public static class ArtistLookupNameVariants
{
    private static readonly string[] BandSuffixes = ["乐团", "乐队"];

    private static readonly Regex ParentheticalRegex = new(
        @"[\(（]([^）\)]+)[\)）]",
        RegexOptions.Compiled);

    private static readonly Regex StagePrefixRegex = new(
        @"^(?:MC|M\.C\.|DJ|VJ)\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>按优先级返回检索名：原名、括号内外、脚本分段、去前缀/乐队后缀等。</summary>
    public static IEnumerable<string> Enumerate(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(artistName.Trim());

        foreach (var alias in ArtistKnownAliases.GetAliasesFor(artistName))
            EnqueueIfNew(queue, seen, alias);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            var normalized = ArtistNameNormalizer.Normalize(name);
            if (normalized.Length < 1 || !seen.Add(normalized))
                continue;

            yield return normalized;

            EnqueueIfNew(queue, seen, StripTrailingBandSuffix(normalized));

            foreach (var paren in ExpandParenthetical(normalized))
                EnqueueIfNew(queue, seen, paren);

            foreach (var segment in ExtractScriptSegments(normalized))
                EnqueueIfNew(queue, seen, segment);

            if (TryStripStagePrefix(normalized, out var withoutPrefix))
                EnqueueIfNew(queue, seen, withoutPrefix);
        }
    }

    internal static string? StripTrailingBandSuffix(string name)
    {
        var n = name.TrimEnd();
        foreach (var suffix in BandSuffixes)
        {
            if (n.EndsWith(suffix, StringComparison.Ordinal) && n.Length > suffix.Length + 1)
                return n[..^suffix.Length].TrimEnd();
        }

        return null;
    }

    private static void EnqueueIfNew(Queue<string> queue, HashSet<string> seen, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        var n = ArtistNameNormalizer.Normalize(candidate);
        if (n.Length >= 1 && !seen.Contains(n))
            queue.Enqueue(n);
    }

    private static IEnumerable<string> ExpandParenthetical(string name)
    {
        var match = ParentheticalRegex.Match(name);
        if (!match.Success)
            yield break;

        var inner = ArtistNameNormalizer.Normalize(match.Groups[1].Value);
        if (inner.Length >= 1)
            yield return inner;

        var outer = ArtistNameNormalizer.Normalize(ParentheticalRegex.Replace(name, string.Empty));
        if (outer.Length >= 1)
            yield return outer;
    }

    /// <summary>拆分中英混排名（如「Coco 李玟」→「李玟」「Coco」）。</summary>
    internal static IEnumerable<string> ExtractScriptSegments(string name)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool? inCjk = null;

        void Flush()
        {
            if (current.Length == 0)
                return;

            var piece = ArtistNameNormalizer.Normalize(current.ToString());
            current.Clear();
            if (piece.Length >= 1)
                segments.Add(piece);
        }

        foreach (var c in name)
        {
            if (char.IsWhiteSpace(c))
            {
                Flush();
                inCjk = null;
                continue;
            }

            var cjk = IsCjk(c);
            if (inCjk.HasValue && inCjk.Value != cjk)
                Flush();

            inCjk = cjk;
            current.Append(c);
        }

        Flush();

        foreach (var segment in segments)
            yield return segment;

        var cjkOnly = new StringBuilder();
        foreach (var c in name)
        {
            if (IsCjk(c))
                cjkOnly.Append(c);
        }

        var cjkJoined = ArtistNameNormalizer.Normalize(cjkOnly.ToString());
        if (cjkJoined.Length >= 2)
            yield return cjkJoined;

        var latinOnly = new StringBuilder();
        foreach (var c in name)
        {
            if (!IsCjk(c) && !char.IsWhiteSpace(c))
                latinOnly.Append(c);
        }

        var latinJoined = ArtistNameNormalizer.Normalize(latinOnly.ToString());
        if (latinJoined.Length >= 2)
            yield return latinJoined;
    }

    private static bool TryStripStagePrefix(string name, out string stripped)
    {
        stripped = string.Empty;
        var match = StagePrefixRegex.Match(name);
        if (!match.Success)
            return false;

        stripped = ArtistNameNormalizer.Normalize(match.Groups[1].Value);
        return stripped.Length >= 2;
    }

    private static bool IsCjk(char c) =>
        c is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF';
}
