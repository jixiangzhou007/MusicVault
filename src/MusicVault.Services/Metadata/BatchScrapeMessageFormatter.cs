using MusicVault.Services;



namespace MusicVault.Services.Metadata;



/// <summary>批量刮削面向用户的提示文案（编码为 svc: 供 UI 解析）。</summary>

public static class BatchScrapeMessageFormatter

{

    private const char DimensionSeparator = '|';



    public static string FormatTrackResult(string? raw)

    {

        if (string.IsNullOrWhiteSpace(raw))

            return ServiceMessageCodec.Key("Scrape.Incomplete");



        if (ServiceMessageCodec.IsEncoded(raw))

            return raw;



        var text = raw.Trim();

        if (text.StartsWith("部分失败：", StringComparison.Ordinal))

            text = text["部分失败：".Length..].Trim();

        else if (text.StartsWith("Partial failure:", StringComparison.OrdinalIgnoreCase))

            text = text["Partial failure:".Length..].Trim();



        if (text.EndsWith("未获取", StringComparison.Ordinal))
        {
            var stem = text[..^3].TrimEnd('：', '、', ' ');
            return string.IsNullOrEmpty(stem)
                ? ServiceMessageCodec.Key("Scrape.DimensionNotWritten")
                : ServiceMessageCodec.Format("Scrape.DimensionMissedStem", stem);
        }

        if (text.StartsWith("匹配度不足", StringComparison.Ordinal)
            || text.StartsWith("Match score too low", StringComparison.OrdinalIgnoreCase))
            return MapLowMatchScore(text);



        return text switch

        {

            "刮削未完成" or "Scrape incomplete" => ServiceMessageCodec.Key("Scrape.Incomplete"),

            "未找到在线匹配" or "No online match found" => ServiceMessageCodec.Key("Scrape.NoOnlineMatch"),

            "未找到完整元数据匹配" => ServiceMessageCodec.Key("Scrape.IncompleteMetadata"),

            "缺少歌名或歌手，无法搜索" => ServiceMessageCodec.Key("Scrape.MissingTitleArtist"),

            "文件不存在" => ServiceMessageCodec.Key("Scrape.FileNotFound"),

            "未找到可用的在线歌曲匹配" or "No usable online song match found" =>

                ServiceMessageCodec.Key("Scrape.NoOnlineMatch"),

            "在线结果信息不完整，可更换数据源后重试" or "Online result incomplete; try another source" =>

                ServiceMessageCodec.Key("Scrape.IncompleteMetadata"),

            "缺少歌名或歌手，无法在线搜索" or "Missing title or artist; cannot search online" =>

                ServiceMessageCodec.Key("Scrape.MissingTitleArtist"),

            "音频文件不存在或已被移动" or "Audio file not found or moved" =>

                ServiceMessageCodec.Key("Scrape.FileNotFound"),

            "在线匹配度偏低，请核对歌名、歌手或尝试「全部平台」数据源"

                or "Match score too low; verify title/artist or try All platforms" =>

                ServiceMessageCodec.Key("Scrape.LowMatchScore"),

            _ when text.StartsWith("匹配度不足（", StringComparison.Ordinal) => MapLowMatchScore(text),

            _ when text.StartsWith("Match score too low (", StringComparison.OrdinalIgnoreCase) =>

                MapLowMatchScore(text),

            _ => raw

        };

    }



    public static string FormatPartialFailure(

        BatchScrapeOptions options,

        bool tagsWritten,

        bool coverEmbedded,

        bool lyricsEmbedded)

    {

        var wrote = new List<string>();

        var missed = new List<string>();



        if (options.UpdatesAnyTag)

            AddDimension(tagsWritten ? wrote : missed, "Scrape.Dimension.Tags");

        if (options.EmbedCover)

            AddDimension(coverEmbedded ? wrote : missed, "Scrape.Dimension.Cover");

        if (options.EmbedLyrics)

            AddDimension(lyricsEmbedded ? wrote : missed, "Scrape.Dimension.Lyrics");



        if (wrote.Count > 0 && missed.Count > 0)

            return ServiceMessageCodec.Format("Scrape.PartialFailure", JoinDimensions(wrote), JoinDimensions(missed));



        if (missed.Count > 0)

            return ServiceMessageCodec.Format("Scrape.PartialMissedOnly", JoinDimensions(missed));



        return ServiceMessageCodec.Key("Scrape.Incomplete");

    }



    public static string FormatSuccessSummary(bool tagsWritten, bool coverEmbedded, bool lyricsEmbedded)

    {

        var parts = new List<string> { ServiceMessageCodec.Key("Scrape.Success") };

        if (tagsWritten) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Tags"));

        if (coverEmbedded) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Cover"));

        if (lyricsEmbedded) parts.Add(ServiceMessageCodec.Key("Scrape.Dimension.Lyrics"));

        return string.Join(" · ", parts);

    }



    private static void AddDimension(List<string> target, string key) =>

        target.Add(ServiceMessageCodec.Key(key));



    private static string JoinDimensions(IEnumerable<string> encodedKeys) =>

        string.Join(DimensionSeparator, encodedKeys);



    private static string MapLowMatchScore(string text)

    {

        if (text.StartsWith("匹配度不足（", StringComparison.Ordinal)
            && (text.EndsWith(')') || text.EndsWith('）')))
        {
            var score = text["匹配度不足（".Length..^1];
            return ServiceMessageCodec.Format("Scrape.LowMatchScoreDetail", score);
        }

        if (text.StartsWith("Match score too low (", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
        {
            var score = text["Match score too low (".Length..^1];
            return ServiceMessageCodec.Format("Scrape.LowMatchScoreDetail", score);
        }

        return ServiceMessageCodec.Key("Scrape.LowMatchScore");
    }
}

