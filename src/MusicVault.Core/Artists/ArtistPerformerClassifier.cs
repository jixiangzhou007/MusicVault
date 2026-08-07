namespace MusicVault.Core.Artists;

/// <summary>判断艺术家字段拆分结果是否像「演唱者」，而非厂牌/工作室/企划等组织。</summary>
public static class ArtistPerformerClassifier
{
    private static readonly string[] OrganizationKeywords =
    [
        "工作室", "文化公司", "文化传媒", "传媒", "娱乐公司", "唱片公司", "音乐公司",
        "厂牌", "企划", "音乐社", "音乐工作室", "Production", "Records", "Record Label",
        "Label", "Studio", "Official"
    ];

    private static readonly string[] PerformerKeywords =
    [
        "歌手", "音乐人", "说唱", "rapper", "Rapper", "乐队", "组合", "男歌手", "女歌手",
        "唱作人", "作词", "作曲", "演奏", "DJ", "偶像", "声优", "演员", "艺人"
    ];

    private static readonly string[] NonPerformerKeywords =
    [
        "厂牌", "唱片公司", "文化公司", "工作室", "传媒公司", "娱乐公司", "音乐厂牌",
        "音乐工作室", "虚拟歌手企划", "音乐企划", "发行公司", "经纪公司"
    ];

    /// <summary>基于名称启发式：过滤厂牌/工作室等（如「国风堂」）。</summary>
    public static bool IsLikelyPerformer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        name = name.Trim();
        if (name.Length is < 2 or > 40)
            return false;

        if (CollectiveArtistCatalog.KnownPrefixes.Any(p =>
                name.Equals(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (OrganizationKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;

        foreach (var suffix in CollectiveArtistCatalog.OrganizationSuffixes.OrderByDescending(s => s.Length))
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var stem = name[..^suffix.Length];
            if (stem.Length is 0 or > 8)
                continue;

            if (suffix is "堂" or "社" or "音乐社")
                return false;

            if (suffix is "工作室" or "唱片" or "传媒" or "娱乐")
                return false;
        }

        return true;
    }

    /// <summary>结合百度百科摘要与基本信息栏，判断词条是否描述歌手/音乐人。</summary>
    public static bool IsLikelyPerformerFromBaidu(
        string artistName,
        string? summary,
        IReadOnlyDictionary<string, string>? basicFields = null)
    {
        if (!IsLikelyPerformer(artistName))
            return false;

        basicFields ??= new Dictionary<string, string>();
        var job = GetField(basicFields, "职业", "身份", "主要职业", "类型");
        var company = GetField(basicFields, "公司名称", "所属公司", "总部", "性质", "类别");

        var blob = string.Join(' ', new[] { summary, job, company }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(blob))
            return true;

        if (NonPerformerKeywords.Any(k => blob.Contains(k, StringComparison.Ordinal)))
            return false;

        if (PerformerKeywords.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(job)
            && job.Contains("公司", StringComparison.Ordinal)
            && !job.Contains("歌手", StringComparison.Ordinal)
            && !job.Contains("音乐", StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(company)
            && company.Contains("公司", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(job))
            return false;

        return true;
    }

    private static string? GetField(IReadOnlyDictionary<string, string> fields, params string[] keys)
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
}
