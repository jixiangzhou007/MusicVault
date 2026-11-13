using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiSourceRotationPriorityTests
{
    [Fact]
    public void PrioritizeOriginalTrack_puts_original_before_login_alternates()
    {
        var original = new OnlineTrack
        {
            Id = "3448738711",
            Title = "无意",
            Artist = "加木",
            Platform = MusicPlatform.NetEase
        };
        var qqMatch = new OnlineTrack
        {
            Id = "003QQSong",
            Title = "无意",
            Artist = "加木",
            Platform = MusicPlatform.QQMusic
        };

        var ordered = GoMusicApiClient.PrioritizeOriginalTrack(original, [qqMatch]);

        Assert.Equal(2, ordered.Count);
        Assert.Same(original, ordered[0]);
        Assert.Same(qqMatch, ordered[1]);
    }

    [Fact]
    public void PrioritizeOriginalTrack_dedupes_same_platform_id()
    {
        var original = new OnlineTrack
        {
            Id = "1",
            Title = "A",
            Artist = "B",
            Platform = MusicPlatform.NetEase
        };
        var duplicate = new OnlineTrack
        {
            Id = "1",
            Title = "A",
            Artist = "B",
            Platform = MusicPlatform.NetEase
        };

        var ordered = GoMusicApiClient.PrioritizeOriginalTrack(original, [duplicate]);

        Assert.Single(ordered);
        Assert.Same(original, ordered[0]);
    }
}
