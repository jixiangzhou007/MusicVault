using System.Text;
using System.Text.RegularExpressions;
using MusicVault.Core.Models;
using MusicVault.Core.Text;

namespace MusicVault.Services.Metadata;

/// <summary>LRC 歌词解析器，支持同步滚动</summary>
public static partial class LrcParser
{
    // [mm:ss.xx] / [mm:ss:xx] / [mm:ss]
    [GeneratedRegex(@"\[(\d{1,2}):(\d{2})(?:[\.:](\d{2,3}))?\]", RegexOptions.Compiled)]
    private static partial Regex TimeTagRegex();

    // [hh:mm:ss.xx]
    [GeneratedRegex(@"\[(\d{1,2}):(\d{2}):(\d{2})(?:[\.:](\d{2,3}))?\]", RegexOptions.Compiled)]
    private static partial Regex HmsTimeTagRegex();

    [GeneratedRegex(@"\[offset:\s*([+-]?\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OffsetTagRegex();

    /// <summary>输出缓冲导致听感略滞后于解码进度，高亮时略作补偿（秒）。</summary>
    public const double DefaultLyricLatencyCompensationSeconds = 0.18;

    /// <summary>解析 LRC 全局 offset 标签（毫秒，可为负）。</summary>
    public static double ParseOffsetSeconds(string lrcContent)
    {
        var m = OffsetTagRegex().Match(lrcContent);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var ms))
            return 0;
        return ms / 1000.0;
    }

    /// <summary>解析 LRC 文本为时间轴行列表</summary>
    public static IReadOnlyList<LrcLine> Parse(string lrcContent)
    {
        var offset = ParseOffsetSeconds(lrcContent);
        var lines = new List<LrcLine>();
        foreach (var raw in lrcContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var matches = CollectTimeTagMatches(line);
            if (matches.Count == 0)
            {
                if (!line.StartsWith('['))
                    lines.Add(new LrcLine { TimeSeconds = lines.Count > 0 ? lines[^1].TimeSeconds : 0, Text = line });
                continue;
            }

            var text = StripTimeTags(line).Trim();
            if (string.IsNullOrEmpty(text)) continue;

            foreach (var total in SelectDisplayTimestamps(matches, text))
                lines.Add(new LrcLine { TimeSeconds = total + offset, Text = text });
        }
        var ordered = lines.OrderBy(l => l.TimeSeconds).ToList();
        return CollapseDuplicateTextLines(NormalizeDisplayLines(ordered));
    }

    /// <summary>
    /// 一行内多个时间戳：间隔很近视为逐字/逐词标签只保留首个；间隔较大视为副歌重复则全部保留。
    /// </summary>
    private static IEnumerable<double> SelectDisplayTimestamps(IReadOnlyList<double> matches, string lineText)
    {
        if (matches.Count <= 1)
            return matches;

        var sorted = matches.OrderBy(t => t).ToList();
        var span = sorted[^1] - sorted[0];
        if (span < 5.0)
            return [sorted[0]];

        if (LooksLikeSpreadMetadataHeader(lineText))
            return [sorted[0]];

        return sorted;
    }

    private static bool LooksLikeSpreadMetadataHeader(string lineText)
    {
        if (string.IsNullOrWhiteSpace(lineText) || lineText.Length < 16)
            return false;

        if (!lineText.Contains('(') || !lineText.Contains('-'))
            return false;

        return lineText.Contains('/') || lineText.Contains('／') || lineText.Contains('、');
    }

    private static List<double> CollectTimeTagMatches(string line)
    {
        var result = new List<double>();
        foreach (Match m in HmsTimeTagRegex().Matches(line))
            result.Add(ParseHmsMatch(m));
        foreach (Match m in TimeTagRegex().Matches(line))
        {
            if (IsInsideHmsMatch(m, line))
                continue;
            result.Add(ParseMsMatch(m));
        }
        return result;
    }

    private static bool IsInsideHmsMatch(Match mmSsMatch, string line)
    {
        foreach (Match hms in HmsTimeTagRegex().Matches(line))
        {
            if (mmSsMatch.Index >= hms.Index && mmSsMatch.Index < hms.Index + hms.Length)
                return true;
        }
        return false;
    }

    private static string StripTimeTags(string line)
    {
        var stripped = HmsTimeTagRegex().Replace(line, "");
        stripped = TimeTagRegex().Replace(stripped, "");
        return stripped;
    }

    private static double ParseMsMatch(Match m)
    {
        var min = int.Parse(m.Groups[1].Value);
        var sec = int.Parse(m.Groups[2].Value);
        var ms = 0;
        if (m.Groups[3].Success)
        {
            ms = int.Parse(m.Groups[3].Value);
            if (m.Groups[3].Value.Length == 2) ms *= 10;
        }
        return min * 60 + sec + ms / 1000.0;
    }

    private static double ParseHmsMatch(Match m)
    {
        var hour = int.Parse(m.Groups[1].Value);
        var min = int.Parse(m.Groups[2].Value);
        var sec = int.Parse(m.Groups[3].Value);
        var ms = 0;
        if (m.Groups[4].Success)
        {
            ms = int.Parse(m.Groups[4].Value);
            if (m.Groups[4].Value.Length == 2) ms *= 10;
        }
        return hour * 3600 + min * 60 + sec + ms / 1000.0;
    }

    /// <summary>将原文与中文 LRC 按时间轴或行序合并为双语行。</summary>
    public static string MergeBilingualSources(string primaryLrc, string translationLrc)
    {
        var a = NormalizeDisplayLines(Parse(primaryLrc));
        var b = NormalizeDisplayLines(Parse(translationLrc));
        if (b.Count == 0) return primaryLrc;

        var sb = new StringBuilder();
        for (var i = 0; i < a.Count; i++)
        {
            var line = a[i];
            var zh = FindTranslationForLine(line, b, i);
            var text = line.Text;
            if (!string.IsNullOrWhiteSpace(zh))
                text = $"{text}\n{zh}";
            sb.AppendLine(FormatLrcLine(line.TimeSeconds, text));
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatLrcLine(double seconds, string text)
    {
        var min = (int)(seconds / 60);
        var sec = seconds % 60;
        return $"[{min:D2}:{sec:00.00}]{text}";
    }

    private static string? FindTranslationForLine(LrcLine primary, IReadOnlyList<LrcLine> zhLines, int index)
    {
        const double epsilon = 0.05;
        var match = zhLines.FirstOrDefault(z => Math.Abs(z.TimeSeconds - primary.TimeSeconds) < epsilon);
        if (match != null && MusicTextScript.IsMostlyChinese(match.Text))
            return match.Text.Trim();

        if (index < zhLines.Count && MusicTextScript.IsMostlyChinese(zhLines[index].Text))
            return zhLines[index].Text.Trim();

        return null;
    }

    /// <summary>合并相同时间戳的多行；区分原文与中文对照。</summary>
    public static IReadOnlyList<LrcLine> NormalizeDisplayLines(IReadOnlyList<LrcLine> lines)
    {
        if (lines.Count == 0) return lines;

        var groups = lines
            .GroupBy(l => Math.Round(l.TimeSeconds, 2))
            .OrderBy(g => g.Key);

        var result = new List<LrcLine>();
        foreach (var g in groups)
        {
            var parts = g.Select(x => x.Text.Trim()).Where(t => t.Length > 0).ToList();
            if (parts.Count == 0) continue;

            string primary;
            string? translation = null;

            if (parts.Count == 1)
            {
                var single = parts[0];
                if (single.Contains('\n'))
                {
                    var nl = single.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (nl.Length >= 2)
                    {
                        if (MusicTextScript.IsMostlyChinese(nl[0]) && !MusicTextScript.IsMostlyChinese(nl[^1]))
                            (primary, translation) = (string.Join("\n", nl.Skip(1)), string.Join("\n", nl.Take(1)));
                        else
                        {
                            primary = nl[0];
                            translation = string.Join("\n", nl.Skip(1));
                        }
                    }
                    else
                        primary = single;
                }
                else
                    primary = single;
            }
            else
            {
                var cjk = parts.Where(MusicTextScript.IsMostlyChinese).ToList();
                var nonCjk = parts.Where(p => !MusicTextScript.IsMostlyChinese(p)).ToList();
                if (cjk.Count > 0 && nonCjk.Count > 0)
                {
                    primary = string.Join("\n", nonCjk);
                    translation = string.Join("\n", cjk);
                }
                else
                {
                    primary = parts[0];
                    translation = string.Join("\n", parts.Skip(1));
                }
            }

            if (translation != null && MusicTextScript.IsMostlyChinese(primary))
                (primary, translation) = (translation, primary);

            result.Add(new LrcLine
            {
                TimeSeconds = g.First().TimeSeconds,
                Text = primary,
                TranslationText = translation
            });
        }

        return result;
    }

    public static bool HasSyncedTimestamps(IReadOnlyList<LrcLine> lines)
    {
        if (lines.Count <= 1) return false;

        var times = lines.Select(l => Math.Round(l.TimeSeconds, 2)).ToList();
        var distinct = times.Distinct().Count();
        if (distinct <= 1) return false;

        var span = times.Max() - times.Min();
        if (span >= 8.0) return true;
        if (lines.Count >= 4 && distinct >= Math.Max(3, lines.Count / 4) && span >= 3.0) return true;
        return false;
    }

    /// <summary>将播放进度转换为歌词匹配用时间（补偿输出延迟）。</summary>
    public static double AdjustPlaybackPositionForLyrics(
        double positionSeconds,
        double compensationSeconds = DefaultLyricLatencyCompensationSeconds) =>
        Math.Max(0, positionSeconds - compensationSeconds);

    /// <summary>根据当前播放位置获取活跃行索引；前奏尚未到达首句时返回 -1。</summary>
    public static int GetActiveLineIndex(IReadOnlyList<LrcLine> lines, double positionSeconds)
    {
        if (lines.Count == 0) return -1;

        if (positionSeconds < lines[0].TimeSeconds - 0.05)
            return -1;

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (positionSeconds >= lines[i].TimeSeconds - 0.05)
                return i;
        }

        return -1;
    }

    /// <summary>移除 LRC 开头的歌名/歌手占位行（常见于 [00:00.00] 元数据行）。</summary>
    public static IReadOnlyList<LrcLine> FilterMetadataLines(
        IReadOnlyList<LrcLine> lines,
        string? title = null,
        string? artist = null)
    {
        if (lines.Count == 0)
            return lines;

        var filtered = new List<LrcLine>(lines.Count);
        foreach (var line in lines)
        {
            if (LyricsQuality.IsTitleArtistPlaceholder(line.Text, title, artist)
                || LyricsQuality.IsExtendedMetadataLine(line.Text, title, artist))
                continue;

            if (!string.IsNullOrWhiteSpace(title)
                && string.Equals(
                    LyricsQuality.NormalizeForCompare(line.Text),
                    LyricsQuality.NormalizeForCompare(title),
                    StringComparison.Ordinal)
                && line.TimeSeconds < 1.0
                && lines.Any(l => l.TimeSeconds > line.TimeSeconds + 0.5))
                continue;

            filtered.Add(line);
        }

        return CollapseDuplicateTextLines(filtered);
    }

    /// <summary>合并连续重复文本；若全文仅同一行文案则只保留首条。</summary>
    public static IReadOnlyList<LrcLine> CollapseDuplicateTextLines(IReadOnlyList<LrcLine> lines)
    {
        if (lines.Count <= 1)
            return lines;

        var distinct = lines
            .Select(l => l.Text.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinct <= 1)
        {
            var text = lines[0].Text.Trim();
            if (lines.Count > 1)
            {
                if (LooksLikeSpreadMetadataHeader(text))
                    return [lines[0]];

                if (HasSyncedTimestamps(lines))
                {
                    if (lines.Count == 2)
                        return lines;

                    if (lines.All(l => string.Equals(l.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
                        return [lines[0]];
                }
            }

            return [lines[0]];
        }

        var result = new List<LrcLine>(lines.Count);
        string? previous = null;
        foreach (var line in lines)
        {
            var text = line.Text.Trim();
            if (text.Length == 0)
                continue;

            if (previous != null
                && string.Equals(text, previous, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(line);
            previous = text;
        }

        return result;
    }

    /// <summary>无时间轴的纯文本歌词按曲目时长均匀分配时间戳，便于滚动与高亮。</summary>
    public static IReadOnlyList<LrcLine> EnsureSyncedTimeline(IReadOnlyList<LrcLine> lines, double durationSeconds)
    {
        if (lines.Count <= 1) return lines;
        if (HasSyncedTimestamps(lines)) return lines;

        var duration = Math.Max(durationSeconds, lines.Count * 2.0);
        var result = new List<LrcLine>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines.Count == 1 ? 0.0 : duration * i / (lines.Count - 1);
            result.Add(new LrcLine { TimeSeconds = t, Text = lines[i].Text, TranslationText = lines[i].TranslationText });
        }
        return result;
    }
}
