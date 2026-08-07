using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class ServiceCultureTests
{
    [Fact]
    public void RefreshFromDataDirectory_reads_culture_from_settings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MusicVault-culture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), """{"Culture":"en-US"}""");

        try
        {
            ServiceCulture.RefreshFromDataDirectory(dir);
            Assert.Equal(ServiceCulture.EnUs, ServiceCulture.Current);
        }
        finally
        {
            ServiceCulture.SetCurrent(ServiceCulture.ZhCn);
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Normalize_maps_unknown_cultures_to_zh_cn()
    {
        Assert.Equal(ServiceCulture.ZhCn, ServiceCulture.Normalize("fr-FR"));
        Assert.Equal(ServiceCulture.EnUs, ServiceCulture.Normalize("en-GB"));
    }
}

public sealed class BaiduBaikeIntroComposerTests
{
    [Fact]
    public void Compose_uses_english_glue_when_culture_is_en_us()
    {
        ServiceCulture.SetCurrent(ServiceCulture.EnUs);
        try
        {
            var fields = new Dictionary<string, string>
            {
                ["性别"] = "男",
                ["毕业院校"] = "淡江大学",
                ["代表作品"] = "晴天, 七里香"
            };

            var intro = BaiduBaikeIntroComposer.Compose("Jay Chou", fields, null);

            Assert.Contains("Graduated from", intro, StringComparison.Ordinal);
            Assert.Contains("Notable works:", intro, StringComparison.Ordinal);
            Assert.Contains("晴天, 七里香", intro, StringComparison.Ordinal);
        }
        finally
        {
            ServiceCulture.SetCurrent(ServiceCulture.ZhCn);
        }
    }

    [Fact]
    public void Compose_uses_chinese_glue_by_default()
    {
        ServiceCulture.SetCurrent(ServiceCulture.ZhCn);
        var fields = new Dictionary<string, string>
        {
            ["毕业院校"] = "淡江大学"
        };

        var intro = BaiduBaikeIntroComposer.Compose("周杰伦", fields, null);

        Assert.Contains("毕业于淡江大学", intro, StringComparison.Ordinal);
    }
}
