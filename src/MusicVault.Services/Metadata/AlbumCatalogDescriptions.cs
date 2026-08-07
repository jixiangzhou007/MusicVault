using System.Text.RegularExpressions;

namespace MusicVault.Services.Metadata;

/// <summary>区分完整专辑文案与搜索/元数据占位（发行方、年份等）。</summary>
internal static class AlbumCatalogDescriptions
{
    public static string BaiduSource => ArtistSourceKeys.BaiduBaike;
    public static string NetEaseSource => ArtistSourceKeys.NetEaseMusic;

    public static bool IsFromBaidu(string? source) => ArtistSourceKeys.IsBaiduBaike(source);

    public static bool IsDisplayReady(string? text, string? source, string? albumName = null)
    {
        if (IsFromBaidu(source))
            return !string.IsNullOrWhiteSpace(text) && text.Trim().Length >= 24;

        return IsSubstantial(text, albumName) && !LooksLikeTrackListing(text, albumName);
    }

    public static bool NeedsBetterDescription(string? text, string? source, string? albumName = null) =>
        !IsDisplayReady(text, source, albumName);

    /// <summary>展示/落库前统一处理：百科保留原文，平台长文截取专辑导语。</summary>
    public static string? NormalizeForDisplay(string? text, string? source, string? albumName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        if (IsFromBaidu(source))
            return text.Length > 1200 ? text[..1200].TrimEnd() + "…" : text;

        if (IsDisplayReady(text, source, albumName))
            return text;

        return ExtractAlbumIntroOnly(text, albumName);
    }

    public static bool IsSubstantial(string? text, string? albumName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (LooksLikeTrackListing(text, albumName))
            return false;

        if (text.Length >= 48)
            return true;

        if (IsMetadataOnly(text))
            return false;

        return text.Length >= 20;
    }

    public static string? PickBest(
        string? primary,
        string? primarySource,
        string? secondary,
        string? secondarySource,
        string? albumName = null)
    {
        string? chosen = null;
        string? chosenSource = null;

        if (IsDisplayReady(primary, primarySource, albumName))
        {
            chosen = primary;
            chosenSource = primarySource;
        }
        else if (IsDisplayReady(secondary, secondarySource, albumName))
        {
            chosen = secondary;
            chosenSource = secondarySource;
        }
        else if (IsSubstantial(primary, albumName) && !LooksLikeTrackListing(primary, albumName))
        {
            chosen = primary;
            chosenSource = primarySource;
        }
        else if (IsSubstantial(secondary, albumName) && !LooksLikeTrackListing(secondary, albumName))
        {
            chosen = secondary;
            chosenSource = secondarySource;
        }
        else
            chosen = !string.IsNullOrWhiteSpace(primary) ? primary : secondary;

        chosenSource ??= !string.IsNullOrWhiteSpace(primary) ? primarySource : secondarySource;
        return NormalizeForDisplay(chosen, chosenSource, albumName);
    }

    public static string? PickSource(
        string? primary,
        string? primarySource,
        string? secondary,
        string? secondarySource,
        string? albumName = null)
    {
        var normalizedPrimary = NormalizeForDisplay(primary, primarySource, albumName);
        var normalizedSecondary = NormalizeForDisplay(secondary, secondarySource, albumName);
        var text = PickBest(primary, primarySource, secondary, secondarySource, albumName);
        if (text == null)
            return null;
        if (string.Equals(text, normalizedPrimary, StringComparison.Ordinal))
            return primarySource;
        if (string.Equals(text, normalizedSecondary, StringComparison.Ordinal))
            return secondarySource;
        return primarySource ?? secondarySource;
    }

    /// <summary>网易云专辑 description 常混入逐曲介绍，启发式过滤。</summary>
    public static bool LooksLikeTrackListing(string? text, string? albumName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        var titleIsCount = Regex.Matches(text, @"《[^》]{1,48}》是").Count;
        if (titleIsCount >= 2)
            return true;

        if (text.Length < 80)
            return false;

        var quoted = Regex.Matches(text, @"《([^》]{1,24})》")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            quoted = quoted
                .Where(t => !string.Equals(t, albumName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var guillemetCount = quoted.Count;
        var distinctTitles = quoted.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (distinctTitles >= 3 || guillemetCount >= 5)
            return true;

        var songLike = quoted.Count(t =>
            text.Contains($"《{t}》", StringComparison.Ordinal)
            && (text.Contains($"《{t}》是", StringComparison.Ordinal)
                || text.Contains($"《{t}》为", StringComparison.Ordinal)
                || text.Contains($"《{t}》由", StringComparison.Ordinal)
                || text.Contains($"《{t}》，", StringComparison.Ordinal)));
        if (songLike >= 2)
            return true;

        if (text.Length > 500 && guillemetCount >= 2 && songLike >= 1)
            return true;

        var numbered = Regex.Matches(text, @"\d{1,2}[\.、．]\s*[\u4e00-\u9fff《]").Count;
        return numbered >= 4;
    }

    /// <summary>截取专辑导语，去掉《歌名》是… 等逐曲段落。</summary>
    public static string? ExtractAlbumIntroOnly(string? text, string? albumName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Trim();
        if (text.Length <= 360 && !LooksLikeTrackListing(text, albumName))
            return text;

        var cutAt = FindTrackListingStartIndex(text, albumName);
        if (cutAt is int index && index > 20 && index < text.Length)
        {
            var intro = text[..index].TrimEnd('，', '、', '；', ' ', '\n', '\r');
            if (intro.Length >= 16)
                return intro.EndsWith('。') || intro.EndsWith('！') || intro.EndsWith('？')
                    ? intro
                    : intro + "。";
        }

        var firstSentence = text.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstSentence)
            && firstSentence.Length >= 12
            && !Regex.IsMatch(firstSentence, @"《[^》]{1,48}》是"))
            return firstSentence + "。";

        return CollapseToLead(text, albumName: albumName);
    }

    private static int? FindTrackListingStartIndex(string text, string? albumName)
    {
        var patterns = new[]
        {
            @"《[^》]{1,48}》是",
            @"《[^》]{1,48}》为",
            @"《[^》]{1,48}》由",
            @"《[^》]{1,48}》，[^。]{4,}是",
            @"\d{1,2}[\.、．]\s*《",
            @"\d{1,2}[\.、．]\s*[\u4e00-\u9fff]{2,8}[，,]",
        };

        int? earliest = null;
        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(text, pattern))
            {
                if (match.Index < 12)
                    continue;

                var title = TryReadGuillemetTitle(match.Value);
                if (!string.IsNullOrWhiteSpace(albumName)
                    && !string.IsNullOrWhiteSpace(title)
                    && string.Equals(title, albumName.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                earliest = earliest == null ? match.Index : Math.Min(earliest.Value, match.Index);
            }
        }

        return earliest;
    }

    private static string? TryReadGuillemetTitle(string matchedText)
    {
        var m = Regex.Match(matchedText, @"《([^》]{1,48})》");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>将过长平台文案收敛为前两句话，避免逐曲介绍占满 UI。</summary>
    public static string? CollapseToLead(string? text, int maxChars = 480, string? albumName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Trim();
        if (text.Length <= maxChars && !LooksLikeTrackListing(text, albumName))
            return text;

        var sentences = text.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sentences.Length == 0)
            return text.Length <= maxChars ? text : text[..maxChars];

        var lead = sentences[0] + "。";
        if (sentences.Length > 1 && lead.Length < maxChars / 2)
        {
            var next = sentences[1];
            var nextLooksLikeSong = Regex.IsMatch(next, @"《[^》]{1,48}》是");
            if (!nextLooksLikeSong)
                lead += next + "。";
        }

        return lead.Length > maxChars ? lead[..maxChars] : lead;
    }

    private static bool IsMetadataOnly(string text)
    {
        var segments = text.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            segments = [text];

        foreach (var segment in segments)
        {
            if (segment.StartsWith("发行：", StringComparison.Ordinal)
                || segment.StartsWith("发行年份：", StringComparison.Ordinal)
                || segment.StartsWith("风格：", StringComparison.Ordinal)
                || segment.StartsWith("Genre:", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("Released:", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("Release year:", StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return true;
    }
}
