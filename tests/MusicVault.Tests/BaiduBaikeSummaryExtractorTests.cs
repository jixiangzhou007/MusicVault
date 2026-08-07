using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class BaiduBaikeSummaryExtractorTests
{
    [Fact]
    public void ExtractPortraitUrl_PrefersLemmaPicOverOgImage()
    {
        const string html = """
            <meta property="og:image" content="https://example.com/logo.png"/>
            <script>"lemmaPic":"https://bkimg.cdn.bcebos.com/pic/周杰伦_portrait.jpg"</script>
            """;

        var url = BaiduBaikeSummaryExtractor.ExtractPortraitUrl(html);

        Assert.NotNull(url);
        Assert.Contains("bkimg.cdn.bcebos.com", url, StringComparison.Ordinal);
        Assert.Contains("portrait", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractPortraitUrl_ReadsSharePicAndDataLemmaImage()
    {
        const string html = """
            <div data-lemmaimage="https://bkimg.cdn.bcebos.com/pic/share.jpg"></div>
            "sharePic":"https://bkimg.cdn.bcebos.com/pic/share2.jpg"
            """;

        var url = BaiduBaikeSummaryExtractor.ExtractPortraitUrl(html);

        Assert.NotNull(url);
        Assert.Contains("bkimg.cdn.bcebos.com", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UpgradeBaiduPortraitUrl_adds_square_crop_for_bcebos()
    {
        var url = BaiduBaikeSummaryExtractor.UpgradeBaiduPortraitUrl(
            "https://bkimg.cdn.bcebos.com/pic/sample.jpg");

        Assert.NotNull(url);
        Assert.Contains("x-bce-process=image/resize", url, StringComparison.Ordinal);
        Assert.Contains("w_800", url, StringComparison.Ordinal);
    }
}
