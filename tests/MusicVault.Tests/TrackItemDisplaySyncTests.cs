using MusicVault.App.ViewModels;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class TrackItemDisplaySyncTests
{
    [Fact]
    public void ApplyMetadataFrom_copies_fields_when_track_instances_differ()
    {
        var listTrack = new Track
        {
            Id = 42,
            FilePath = @"D:\music\song.flac",
            Title = "Old Title",
            Artist = "Old Artist",
            Album = "Old Album",
            Year = 1999,
            Genre = "Pop"
        };
        var editorTrack = new Track
        {
            Id = 42,
            FilePath = @"D:\music\song.flac",
            Title = "New Title",
            Artist = "New Artist",
            Album = "New Album",
            Year = 2020,
            Genre = "Rock"
        };

        var item = new TrackItemViewModel(listTrack);
        item.ApplyMetadataFrom(editorTrack);

        Assert.Equal("New Title", item.Title);
        Assert.Equal("New Artist", item.Artist);
        Assert.Equal("New Album", item.Album);
        Assert.Equal("2020", item.YearText);
        Assert.Equal("Rock", item.Genre);
        Assert.Same(listTrack, item.Track);
        Assert.Equal("New Artist", listTrack.Artist);
    }

    [Fact]
    public void ApplyMetadataFrom_ignores_unrelated_tracks()
    {
        var listTrack = new Track { Id = 1, FilePath = @"D:\a.flac", Artist = "A" };
        var other = new Track { Id = 2, FilePath = @"D:\b.flac", Artist = "B" };

        var item = new TrackItemViewModel(listTrack);
        item.ApplyMetadataFrom(other);

        Assert.Equal("A", item.Artist);
    }

    [Fact]
    public void ResolveForTrack_reuses_view_model_and_updates_from_database()
    {
        var previousTrack = new Track
        {
            Id = 7,
            FilePath = @"D:\reuse.flac",
            Artist = "Before"
        };
        var previous = new List<TrackItemViewModel> { new(previousTrack) };

        var fromDb = new Track
        {
            Id = 7,
            FilePath = @"D:\reuse.flac",
            Artist = "After",
            Title = "Song",
            Album = "Album",
            Year = 2001,
            Genre = "Jazz"
        };

        var resolved = TrackItemViewModel.ResolveForTrack(previous, fromDb);

        Assert.Same(previous[0], resolved);
        Assert.Equal("After", resolved.Artist);
        Assert.Equal("Song", resolved.Title);
        Assert.Same(previousTrack, resolved.Track);
        Assert.Equal("After", previousTrack.Artist);
    }

    [Fact]
    public void SyncDisplay_marks_missing_cover_and_lyrics_from_track_signals()
    {
        var missing = new TrackItemViewModel(new Track
        {
            Id = 3,
            FilePath = @"D:\music\bare.flac",
            Title = "Bare",
            CoverHash = "  ",
            LyricsPath = null
        });
        Assert.True(missing.ShowMissingCover);
        Assert.True(missing.ShowMissingLyrics);

        var complete = new Track
        {
            Id = 3,
            FilePath = @"D:\music\bare.flac",
            Title = "Bare",
            CoverHash = "embedded",
            LyricsPath = @"D:\music\bare.lrc"
        };
        missing.ApplyMetadataFrom(complete);

        Assert.False(missing.ShowMissingCover);
        Assert.False(missing.ShowMissingLyrics);
        Assert.Equal("embedded", missing.Track.CoverHash);
        Assert.Equal(@"D:\music\bare.lrc", missing.Track.LyricsPath);
    }

    [Fact]
    public void IsSameTrack_matches_by_id_and_path()
    {
        var a = new Track { Id = 5, FilePath = @"D:\x.mp3", Artist = "One" };
        var b = new Track { Id = 5, FilePath = @"D:\x.mp3", Artist = "Two" };

        Assert.True(TrackFieldSync.IsSameTrack(a, b));
    }
}
