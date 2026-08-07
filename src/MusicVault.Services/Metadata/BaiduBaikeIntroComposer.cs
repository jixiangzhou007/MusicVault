using System.Text;
using System.Text.RegularExpressions;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>将百科字段裁剪合成为约 200 字的艺术家介绍。</summary>
internal static class BaiduBaikeIntroComposer
{
    private const int TargetMax = 200;

    public static string Compose(string artistName, IReadOnlyDictionary<string, string> fields, string? fallbackText)
    {
        var culture = ServiceCulture.Current;
        var parts = new List<string>();

        var gender = Get(fields, "性别");
        var birth = Get(fields, "出生日期", "出生", "出生地", "出生时间");
        var school = Get(fields, "毕业院校", "毕业学校", "学校");
        var works = Get(fields, "代表作品", "代表作", "主要作品", "知名作品");
        var job = Get(fields, "职业", "身份", "主要成就");
        var nation = Get(fields, "国籍", "民族");
        var realName = Get(fields, "本名", "原名", "中文名");

        var lead = artistName.Trim();
        if (!string.IsNullOrWhiteSpace(realName) && !realName.Equals(lead, StringComparison.OrdinalIgnoreCase))
        {
            lead = FormatMessage(
                culture,
                "Artist.Baike.RealNameLead",
                lead,
                TruncateToken(realName, 12));
        }

        var clauseSep = ServiceMessages.Get("Artist.Baike.ClauseSeparator", culture);
        var head = new StringBuilder(lead);
        if (!string.IsNullOrWhiteSpace(gender))
            head.Append(clauseSep).Append(gender);
        if (!string.IsNullOrWhiteSpace(birth))
            head.Append(clauseSep).Append(TruncateToken(birth, 28));
        else if (!string.IsNullOrWhiteSpace(nation))
            head.Append(clauseSep).Append(TruncateToken(nation, 12));

        if (!string.IsNullOrWhiteSpace(job))
            head.Append(clauseSep).Append(TruncateToken(job, 24));

        parts.Add(head.ToString());

        if (!string.IsNullOrWhiteSpace(school))
        {
            parts.Add(FormatMessage(
                culture,
                "Artist.Baike.GraduatedFrom",
                TruncateToken(school, 22)));
        }

        if (!string.IsNullOrWhiteSpace(works))
        {
            parts.Add(FormatMessage(
                culture,
                "Artist.Baike.NotableWorks",
                FormatWorks(works, culture)));
        }

        var partSep = ServiceMessages.Get("Artist.Baike.PartSeparator", culture);
        var composed = string.Join(partSep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        composed = TrimToLength(composed, TargetMax);
        composed = EnsureTrailingPeriod(composed, culture);

        if (composed.Length >= 40 && fields.Count >= 2)
            return composed;

        var fallback = TrimIntroFallback(fallbackText, artistName);
        return string.IsNullOrWhiteSpace(fallback) ? composed : fallback;
    }

    private static string FormatMessage(string culture, string key, params object[] args)
    {
        var template = ServiceMessages.Get(key, culture);
        return args.Length == 0 ? template : string.Format(template, args);
    }

    private static string EnsureTrailingPeriod(string text, string culture)
    {
        if (text.EndsWith("…", StringComparison.Ordinal))
            return text;

        var period = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "." : "。";
        return text.EndsWith(period, StringComparison.Ordinal) ? text : text + period;
    }

    private static string? Get(IReadOnlyDictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        foreach (var kv in fields)
        {
            foreach (var key in keys)
            {
                if (kv.Key.Contains(key, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }
        }

        return null;
    }

    private static string FormatWorks(string works, string culture)
    {
        works = works.Replace('、', '，').Replace(';', '，');
        var tokens = works.Split(['，', ',', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Take(4)
            .Select(t => TruncateToken(t, 14))
            .ToList();
        if (tokens.Count == 0)
            return TruncateToken(works, 36);

        var separator = ServiceMessages.Get("Artist.Baike.WorksListSeparator", culture);
        var suffix = ServiceMessages.Get("Artist.Baike.WorksMoreSuffix", culture);
        return string.Join(separator, tokens) + (works.Contains('等') ? "" : suffix);
    }

    private static string TrimIntroFallback(string? text, string artistName)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("播报", " ").Replace("编辑", " ").Replace("锁定", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > TargetMax || text.Contains("百科", StringComparison.Ordinal) && text.Length > 120)
        {
            var idx = text.IndexOf(artistName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < 60)
                text = text[idx..];
        }

        return TrimToLength(text, TargetMax);
    }

    private static string TruncateToken(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text;
        return text[..(max - 1)] + "…";
    }

    private static string TrimToLength(string text, int max)
    {
        text = text.Trim().TrimEnd('。', '，', '；', '.', ',', ';');
        if (text.Length <= max) return text;
        var cut = text[..(max - 1)];
        var last = cut.LastIndexOfAny(['，', '、', '；', ' ', ',', ';']);
        if (last > max * 2 / 3)
            cut = cut[..last];
        return cut + "…";
    }
}
