namespace MusicVault.Services.Metadata;

/// <summary>判断歌词文本是否可用于展示（过滤内嵌占位符等无效内容）。</summary>
public static class LyricsQuality
{
    /// <summary>歌词是否足够真实、可用于 UI 展示。</summary>
    public static bool IsUsableContent(string? raw, string? title = null, string? artist = null)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Length < 8)
            return false;

        var lines = LrcParser.Parse(raw);
        var texts = lines
            .Select(l => l.Text.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        if (texts.Count == 0)
            return false;

        var distinct = texts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (distinct.Count <= 1 && texts.Count > 1
            && (IsTitleArtistPlaceholder(distinct[0], title, artist)
                || IsExtendedMetadataLine(distinct[0], title, artist)))
            return false;

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
        {
            var placeholderCount = texts.Count(t =>
                IsTitleArtistPlaceholder(t, title, artist)
                || IsExtendedMetadataLine(t, title, artist));
            if (placeholderCount > 0 && placeholderCount >= texts.Count)
                return false;
            if (placeholderCount >= Math.Max(2, (int)Math.Ceiling(texts.Count * 0.6)))
                return false;
        }

        if (distinct.Count <= 1 && texts.Count >= 3)
            return false;

        if (distinct.Count <= 1 && texts.All(t => t.Length < 48) && !IsTitleArtistPlaceholder(distinct[0], title, artist))
            return texts.Count <= 1;

        return true;
    }

    /// <summary>是否为「歌名 - 歌手」类占位文本（下载工具常见内嵌占位，不含单独歌名行）。</summary>
    public static bool IsTitleArtistPlaceholder(string line, string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            return false;

        var normalized = Normalize(line);
        var titleNorm = Normalize(title);
        var artistNorm = Normalize(artist);
        return normalized == $"{titleNorm} - {artistNorm}"
               || normalized == $"{artistNorm} - {titleNorm}"
               || normalized == $"{titleNorm}-{artistNorm}"
               || normalized == $"{artistNorm}-{titleNorm}";
    }

    /// <summary>扩展元数据行，如「歌名 (作品名) - 歌手A/歌手B」。</summary>
    public static bool IsExtendedMetadataLine(string line, string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(title))
            return false;

        var normalized = Normalize(line);
        var titleNorm = Normalize(title);
        if (!normalized.Contains(titleNorm, StringComparison.Ordinal))
            return false;

        if (normalized.Length <= titleNorm.Length + 6)
            return normalized == titleNorm;

        return line.Contains('(')
               && line.Contains('-')
               && (line.Contains('/') || line.Contains('／') || line.Contains('、')
                   || (!string.IsNullOrWhiteSpace(artist)
                       && normalized.Contains(Normalize(artist), StringComparison.Ordinal)));
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    internal static string NormalizeForCompare(string value) => Normalize(value);
}
