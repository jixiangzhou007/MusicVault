using MusicVault.App.Services;
using MusicVault.Core.Models;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class LibraryFolderNavigatorTests
{
    [Fact]
    public void Split_lists_immediate_child_folders_and_direct_tracks_only()
    {
        var root = Normalize(@"D:\Music");
        var tracks = new[]
        {
            T(root, @"流行\a.mp3"),
            T(root, @"流行\周杰伦\b.mp3"),
            T(root, @"古典\c.mp3"),
            T(root, @"root-song.mp3"),
        };

        var split = LibraryFolderNavigator.Split(root, currentFolderAbsolute: root, tracks);

        Assert.Equal(new[] { "流行", "古典" }.OrderBy(x => x), split.ChildFolders.OrderBy(x => x));
        Assert.Single(split.DirectTracks);
        Assert.EndsWith("root-song.mp3", split.DirectTracks[0].FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Split_inside_child_is_non_recursive()
    {
        var root = Normalize(@"D:\Music");
        var pop = LibraryFolderNavigator.Combine(root, "流行");
        var tracks = new[]
        {
            T(root, @"流行\a.mp3"),
            T(root, @"流行\周杰伦\b.mp3"),
        };

        var split = LibraryFolderNavigator.Split(root, pop, tracks);

        Assert.Equal(new[] { "周杰伦" }, split.ChildFolders);
        Assert.Single(split.DirectTracks);
        Assert.EndsWith("a.mp3", split.DirectTracks[0].FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Split_returns_empty_when_current_outside_root()
    {
        var root = Normalize(@"D:\Music");
        var outside = Normalize(@"D:\Other");
        var tracks = new[] { T(root, @"流行\a.mp3") };

        var split = LibraryFolderNavigator.Split(root, outside, tracks);

        Assert.Empty(split.ChildFolders);
        Assert.Empty(split.DirectTracks);
    }

    [Fact]
    public void Breadcrumb_stops_at_root_when_current_outside_library()
    {
        var root = Normalize(@"D:\Music");
        var outside = Normalize(@"D:\Other");
        var crumbs = LibraryFolderNavigator.Breadcrumb(root, outside, libraryDisplayName: "Music");

        Assert.Single(crumbs);
        Assert.Equal("Music", crumbs[0].Label);
        Assert.Equal(root, crumbs[0].FolderAbsolutePath);
    }

    [Fact]
    public void Breadcrumb_segments_from_root()
    {
        var root = Normalize(@"D:\Music");
        var deep = LibraryFolderNavigator.Combine(root, "流行", "周杰伦");
        var crumbs = LibraryFolderNavigator.Breadcrumb(root, deep, libraryDisplayName: "Music");
        Assert.Equal(3, crumbs.Count);
        Assert.Equal("Music", crumbs[0].Label);
        Assert.Equal("流行", crumbs[1].Label);
        Assert.Equal("周杰伦", crumbs[2].Label);
        Assert.Equal(root, crumbs[0].FolderAbsolutePath);
    }

    private static string Normalize(string p) =>
        Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static Track T(string root, string relative) =>
        new() { FilePath = Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar)) };
}
