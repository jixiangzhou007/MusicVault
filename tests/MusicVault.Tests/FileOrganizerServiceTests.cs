using MusicVault.Core.Models;
using MusicVault.Services.Organize;

namespace MusicVault.Tests;

public class FileOrganizerServiceTests
{
    private readonly FileOrganizerService _organizer = new();

    private static Track SampleTrack() => new()
    {
        Title = "Song*Title",
        Artist = "Artist/Name",
        Album = "Album",
        TrackNumber = 3,
        Year = 2024,
        FilePath = @"C:\music\old.mp3"
    };

    [Fact]
    public void ApplyTemplate_ReplacesPlaceholdersAndSanitizes()
    {
        var result = _organizer.ApplyTemplate("{Artist} - {Title}.{Extension}", SampleTrack(), ".mp3");
        Assert.DoesNotContain('*', result);
        Assert.DoesNotContain('/', result);
        Assert.Contains("Artist_Name", result);
        Assert.Contains("Song_Title", result);
    }

    [Fact]
    public void PreviewOrganize_BuildsTargetPath()
    {
        var preview = _organizer.PreviewOrganize(
            SampleTrack(),
            @"D:\Library",
            "{Artist}/{Album}",
            "{TrackNumber} - {Title}");

        Assert.Contains("Artist_Name", preview.TargetDirectory);
        Assert.EndsWith(".mp3", preview.TargetPath);
    }

    [Fact]
    public void PreviewOrganize_NoneTemplate_KeepsOriginalSegment()
    {
        var track = SampleTrack();
        var preview = _organizer.PreviewOrganize(track, @"D:\Library", folderTemplate: null, fileNameTemplate: null);

        Assert.Equal(track.FilePath, preview.TargetPath);
        Assert.Equal("old.mp3", preview.FileName);
    }

    [Fact]
    public void PreviewOrganize_FolderOnly_MovesDirectoryKeepsFileName()
    {
        var preview = _organizer.PreviewOrganize(
            SampleTrack(),
            @"D:\Library",
            "{Artist}/{Album}",
            fileNameTemplate: null);

        Assert.Contains("Artist_Name", preview.TargetDirectory);
        Assert.EndsWith("old.mp3", preview.TargetPath);
        Assert.Equal("old.mp3", preview.FileName);
    }
}
