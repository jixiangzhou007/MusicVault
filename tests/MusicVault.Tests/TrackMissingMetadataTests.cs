using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class TrackMissingMetadataTests
{
    [Theory]
    [InlineData("embedded")]
    [InlineData("abc123")]
    public void HasCoverSignal_true_when_CoverHash_nonempty(string coverHash)
    {
        var track = new Track { CoverHash = coverHash };

        Assert.True(TrackMissingMetadata.HasCoverSignal(track));
        Assert.False(TrackMissingMetadata.IsMissingCover(track));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasCoverSignal_false_when_CoverHash_blank(string? coverHash)
    {
        var track = new Track { CoverHash = coverHash };

        Assert.False(TrackMissingMetadata.HasCoverSignal(track));
        Assert.True(TrackMissingMetadata.IsMissingCover(track));
    }

    [Fact]
    public void HasLyricsSignal_true_when_LyricsPath_set()
    {
        var track = new Track { LyricsPath = @"D:\music\song.lrc" };

        Assert.True(TrackMissingMetadata.HasLyricsSignal(track));
        Assert.False(TrackMissingMetadata.IsMissingLyrics(track));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void HasLyricsSignal_false_when_LyricsPath_blank(string? lyricsPath)
    {
        var track = new Track { LyricsPath = lyricsPath };

        Assert.False(TrackMissingMetadata.HasLyricsSignal(track));
        Assert.True(TrackMissingMetadata.IsMissingLyrics(track));
    }

    [Fact]
    public void Cover_and_lyrics_signals_are_independent()
    {
        var onlyCover = new Track { CoverHash = "embedded", LyricsPath = null };
        var onlyLyrics = new Track { CoverHash = null, LyricsPath = @"D:\a.lrc" };
        var neither = new Track();

        Assert.True(TrackMissingMetadata.HasCoverSignal(onlyCover));
        Assert.True(TrackMissingMetadata.IsMissingLyrics(onlyCover));

        Assert.True(TrackMissingMetadata.HasLyricsSignal(onlyLyrics));
        Assert.True(TrackMissingMetadata.IsMissingCover(onlyLyrics));

        Assert.True(TrackMissingMetadata.IsMissingCover(neither));
        Assert.True(TrackMissingMetadata.IsMissingLyrics(neither));
    }
}
