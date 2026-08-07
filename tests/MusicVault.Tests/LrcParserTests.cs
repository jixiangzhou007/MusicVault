using MusicVault.Core.Models;

using MusicVault.Services.Metadata;



namespace MusicVault.Tests;



public class LrcParserTests

{

    private const string Sample = """

        [00:12.00]第一句歌词

        [00:15.50][00:30.00]重复时间轴

        [ar:Artist]

        无时间轴行

        """;



    [Fact]

    public void Parse_ExtractsTimedLines()

    {

        var lines = LrcParser.Parse(Sample);

        Assert.True(lines.Count >= 3);

        Assert.Contains(lines, l => l.Text == "第一句歌词");

        Assert.Equal(12.0, lines.First(l => l.Text == "第一句歌词").TimeSeconds, 2);

    }



    [Fact]

    public void Parse_AppliesOffsetTag()

    {

        var lines = LrcParser.Parse("[offset:+500]\n[00:10.00]Line");

        Assert.Equal(10.5, lines[0].TimeSeconds, 2);

    }



    [Fact]

    public void Parse_SupportsHourFormat()

    {

        var lines = LrcParser.Parse("[01:02:03.00]Long track line");

        Assert.Equal(3723.0, lines[0].TimeSeconds, 2);

    }



    [Fact]

    public void GetActiveLineIndex_ReturnsCorrectLine()

    {

        var lines = LrcParser.Parse("[00:10.00]A\n[00:20.00]B\n[00:30.00]C");

        Assert.Equal(-1, LrcParser.GetActiveLineIndex(lines, 5));

        Assert.Equal(0, LrcParser.GetActiveLineIndex(lines, 15));

        Assert.Equal(1, LrcParser.GetActiveLineIndex(lines, 25));

        Assert.Equal(2, LrcParser.GetActiveLineIndex(lines, 35));

    }



    [Fact]

    public void HasSyncedTimestamps_RejectsFakeSpread()

    {

        var fake = new[]

        {

            new LrcLine { TimeSeconds = 0, Text = "A" },

            new LrcLine { TimeSeconds = 0, Text = "B" },

            new LrcLine { TimeSeconds = 0.01, Text = "C" }

        };

        Assert.False(LrcParser.HasSyncedTimestamps(fake));

    }



    [Fact]
    public void Parse_CollapsesWordLevelTagsOnSameLine()
    {
        var lines = LrcParser.Parse("[00:15.50][00:15.80][00:16.00]同一句");
        Assert.Single(lines);
        Assert.Equal("同一句", lines[0].Text);
        Assert.Equal(15.5, lines[0].TimeSeconds, 2);
    }

    [Fact]
    public void Parse_KeepsChorusRepeatTimestampsOnSameLine()
    {
        var lines = LrcParser.Parse("[00:15.50][00:30.00]重复时间轴");
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("重复时间轴", l.Text));
    }

    [Fact]
    public void FilterMetadataLines_removes_title_artist_header()
    {
        var lines = new[]
        {
            new LrcLine { TimeSeconds = 0, Text = "我想要占据你 - 告五人" },
            new LrcLine { TimeSeconds = 12, Text = "占据你的美" },
            new LrcLine { TimeSeconds = 18, Text = "占据你的全世界" },
        };

        var filtered = LrcParser.FilterMetadataLines(lines, "我想要占据你", "告五人");
        Assert.Equal(2, filtered.Count);
        Assert.Equal("占据你的美", filtered[0].Text);
    }

    [Fact]
    public void FilterMetadataLines_returns_empty_when_only_placeholders()
    {
        var lines = new[]
        {
            new LrcLine { TimeSeconds = 0, Text = "我想要占据你 - 告五人" },
            new LrcLine { TimeSeconds = 5, Text = "告五人 - 我想要占据你" },
        };

        Assert.Empty(LrcParser.FilterMetadataLines(lines, "我想要占据你", "告五人"));
    }

    [Fact]
    public void GetActiveLineIndex_returns_negative_before_first_line()
    {
        var lines = new[]
        {
            new LrcLine { TimeSeconds = 15, Text = "第一句" },
            new LrcLine { TimeSeconds = 25, Text = "第二句" },
        };

        Assert.Equal(-1, LrcParser.GetActiveLineIndex(lines, 5));
        Assert.Equal(-1, LrcParser.GetActiveLineIndex(lines, 14.9));
        Assert.Equal(0, LrcParser.GetActiveLineIndex(lines, 15));
    }

    [Fact]
    public void Parse_CollapsesMetadataLineWithManyTimestamps()
    {
        var lrc = "[00:00.00][00:05.00][00:10.00][00:15.00]知我 (《梦华录》同人曲) - 国风堂/哦漏";
        var lines = LrcParser.Parse(lrc);
        Assert.Single(lines);
    }

    [Fact]
    public void CollapseDuplicateTextLines_keeps_single_when_all_same()
    {
        var lines = new[]
        {
            new LrcLine { TimeSeconds = 0, Text = "同一句" },
            new LrcLine { TimeSeconds = 5, Text = "同一句" },
            new LrcLine { TimeSeconds = 10, Text = "同一句" },
        };

        var collapsed = LrcParser.CollapseDuplicateTextLines(lines);
        Assert.Single(collapsed);
        Assert.Equal("同一句", collapsed[0].Text);
    }

    [Fact]
    public void EnsureSyncedTimeline_DistributesPlainLines()
    {
        var plain = new[]
        {
            new LrcLine { TimeSeconds = 0, Text = "A" },
            new LrcLine { TimeSeconds = 0, Text = "B" },
            new LrcLine { TimeSeconds = 0, Text = "C" },
        };

        var synced = LrcParser.EnsureSyncedTimeline(plain, 90);

        Assert.Equal(0, synced[0].TimeSeconds);
        Assert.Equal(45, synced[1].TimeSeconds, 1);
        Assert.Equal(90, synced[2].TimeSeconds, 1);
    }
}


