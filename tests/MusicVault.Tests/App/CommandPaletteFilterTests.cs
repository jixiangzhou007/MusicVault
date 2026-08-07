using MusicVault.App.ViewModels;

namespace MusicVault.Tests.App;

public sealed class CommandPaletteFilterTests
{
    [Fact]
    public void MatchesQuery_EmptyQuery_MatchesAll()
    {
        var entry = new CommandPaletteEntry("首页", null, "跳转", ["home"], () => { });
        Assert.True(CommandPaletteViewModel.MatchesQuery("", entry));
        Assert.True(CommandPaletteViewModel.MatchesQuery("   ", entry));
    }

    [Fact]
    public void MatchesQuery_MultiToken_RequiresAllParts()
    {
        var entry = new CommandPaletteEntry(
            "播放与音频",
            "快捷键",
            "设置",
            ["playback", "hotkey"],
            () => { });

        Assert.True(CommandPaletteViewModel.MatchesQuery("播放 快捷键", entry));
        Assert.False(CommandPaletteViewModel.MatchesQuery("播放 下载", entry));
    }

    [Fact]
    public void MatchesQuery_IsCaseInsensitive()
    {
        var entry = new CommandPaletteEntry("Discover", null, "Go to", ["search"], () => { });
        Assert.True(CommandPaletteViewModel.MatchesQuery("disc", entry));
    }

    [Fact]
    public void TryParseFavoriteSearch_KeywordOnly_ClearsQuery()
    {
        Assert.True(CommandPaletteViewModel.TryParseFavoriteSearch("喜欢", out var query));
        Assert.Equal(string.Empty, query);

        Assert.True(CommandPaletteViewModel.TryParseFavoriteSearch("liked", out query));
        Assert.Equal(string.Empty, query);
    }

    [Fact]
    public void TryParseFavoriteSearch_Prefix_StripsKeyword()
    {
        Assert.True(CommandPaletteViewModel.TryParseFavoriteSearch("喜欢 晴天", out var query));
        Assert.Equal("晴天", query);

        Assert.True(CommandPaletteViewModel.TryParseFavoriteSearch("favorite rain", out query));
        Assert.Equal("rain", query);
    }

    [Fact]
    public void TryParseFavoriteSearch_RegularQuery_ReturnsFalse()
    {
        Assert.False(CommandPaletteViewModel.TryParseFavoriteSearch("周杰伦", out var query));
        Assert.Equal("周杰伦", query);
    }

    [Fact]
    public void DedupeTrackEntries_RemovesDuplicatesFromSecondary()
    {
        var favorite = new CommandPaletteEntry("晴天", "周杰伦", "我喜欢", [], () => { });
        var track = new CommandPaletteEntry("晴天", "周杰伦", "曲目", [], () => { });
        var unique = new CommandPaletteEntry("稻香", "周杰伦", "曲目", [], () => { });

        var deduped = CommandPaletteViewModel.DedupeTrackEntries(
            [favorite],
            [track, unique]);

        Assert.Single(deduped);
        Assert.Equal("稻香", deduped[0].Title);
    }
}
