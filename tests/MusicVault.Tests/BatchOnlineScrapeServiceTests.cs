using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class BatchOnlineScrapeServiceTests
{
    [Fact]
    public void ApplyOnlineTags_respects_options()
    {
        var track = new Track
        {
            Title = "旧歌名",
            Artist = "旧歌手",
            Album = "旧专辑",
            Year = 1999,
            Genre = "流行"
        };

        var online = new OnlineTrack
        {
            Title = "新歌名",
            Artist = "新歌手",
            Album = "新专辑",
            AlbumArtist = "新专辑艺术家",
            Year = 2024,
            Genre = "摇滚",
            TrackNumber = 3,
            Composer = "作曲家"
        };

        var options = new BatchScrapeOptions
        {
            UpdateTitle = true,
            UpdateArtist = false,
            UpdateAlbum = true,
            UpdateYear = true,
            UpdateGenre = false,
            UpdateTrackNumber = true,
            UpdateComposer = false
        };

        BatchOnlineScrapeService.ApplyOnlineTags(track, online, options);

        Assert.Equal("新歌名", track.Title);
        Assert.Equal("旧歌手", track.Artist);
        Assert.Equal("新专辑", track.Album);
        Assert.Equal(2024, track.Year);
        Assert.Equal("流行", track.Genre);
        Assert.Equal(3, track.TrackNumber);
    }

    [Fact]
    public void BuildSearchQuery_prefers_title_and_artist()
    {
        var q = BatchOnlineScrapeService.BuildSearchQuery("晴天", "周杰伦", @"D:\music\01.mp3");
        Assert.Equal("周杰伦 晴天", q);
    }
}
