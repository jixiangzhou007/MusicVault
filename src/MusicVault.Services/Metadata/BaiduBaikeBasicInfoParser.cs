using System.Net;
using System.Text.RegularExpressions;

namespace MusicVault.Services.Metadata;

/// <summary>解析百度百科基本信息栏（性别、出生、院校、代表作等）。</summary>
internal static class BaiduBaikeBasicInfoParser
{
    public static IReadOnlyDictionary<string, string> Parse(string html)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in Regex.Matches(
                     html,
                     @"<dt[^>]*class=""[^""]*basicInfo-item\s+name[^""]*""[^>]*>(?<k>[^<]+)</dt>\s*<dd[^>]*class=""[^""]*basicInfo-item\s+value[^""]*""[^>]*>(?<v>.*?)</dd>",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            Add(map, m.Groups["k"].Value, m.Groups["v"].Value);
        }

        foreach (Match m in Regex.Matches(
                     html,
                     @"""name""\s*:\s*""(?<k>[^""]+)""\s*,\s*""value""\s*:\s*""((?:\\.|[^""])*)""",
                     RegexOptions.IgnoreCase))
        {
            Add(map, m.Groups["k"].Value, UnescapeJson(m.Groups[2].Value));
        }

        foreach (Match m in Regex.Matches(
                     html,
                     @"""key""\s*:\s*""(?<k>[^""]+)""\s*,\s*""value""\s*:\s*""((?:\\.|[^""])*)""",
                     RegexOptions.IgnoreCase))
        {
            Add(map, m.Groups["k"].Value, UnescapeJson(m.Groups[2].Value));
        }

        foreach (Match m in Regex.Matches(
                     html,
                     @"""itemName""\s*:\s*""(?<k>[^""]+)""\s*,\s*""itemValue""\s*:\s*""((?:\\.|[^""])*)""",
                     RegexOptions.IgnoreCase))
        {
            Add(map, m.Groups["k"].Value, UnescapeJson(m.Groups[2].Value));
        }

        return map;
    }

    private static void Add(IDictionary<string, string> map, string key, string value)
    {
        key = CleanKey(key);
        value = CleanValue(value);
        if (key.Length == 0 || value.Length == 0)
            return;
        map[key] = value;
    }

    private static string CleanKey(string key)
    {
        key = WebUtility.HtmlDecode(StripTags(key)).Trim();
        return Regex.Replace(key, @"\s+", "");
    }

    private static string CleanValue(string value)
    {
        value = WebUtility.HtmlDecode(StripTags(value));
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = Regex.Replace(value, @"\[?\d+\]?", "").Trim();
        return value;
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", " ", RegexOptions.Singleline);

    private static string UnescapeJson(string s) =>
        s.Replace("\\n", " ").Replace("\\r", "").Replace("\\\"", "\"").Replace("\\/", "/");
}
