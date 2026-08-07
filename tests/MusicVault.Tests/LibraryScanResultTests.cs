using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class LibraryScanResultTests
{
    [Fact]
    public void Summary_resolves_scan_counts_in_english()
    {
        var result = new LibraryScanResult
        {
            RootPath = @"D:\Music",
            DiskFileCount = 10,
            ImportedCount = 2,
            FailedCount = 1,
            DbTrackCount = 42
        };

        Assert.Equal(
            "On disk 10 · imported 2 · failed 1 · indexed 42",
            ServiceMessageCodec.Resolve(result.Summary, "en-US"));
    }

    [Fact]
    public void Summary_resolves_webdav_offline_with_cache()
    {
        var result = new LibraryScanResult
        {
            RootPath = @"\\webdav\music",
            RootMissing = true,
            IsNetworkWebDav = true,
            DbTrackCount = 5
        };

        Assert.Equal(
            "WebDAV unreachable: \\\\webdav\\music · 5 cached tracks kept in library index",
            ServiceMessageCodec.Resolve(result.Summary, "en-US"));
    }
}

public sealed class ArtistProfileResultBuilderTests
{
    [Fact]
    public void FromTracks_builds_encoded_album_summary()
    {
        var tracks = new List<OnlineTrack>
        {
            new()
            {
                Id = "1",
                Title = "Song A",
                Artist = "Jay Chou",
                Album = "Fantasy",
                Platform = MusicPlatform.NetEase
            },
            new()
            {
                Id = "2",
                Title = "Song B",
                Artist = "Jay Chou",
                Album = "Still Fantasy",
                Platform = MusicPlatform.NetEase
            }
        };

        var result = ArtistProfileResultBuilder.FromTracks(
            tracks,
            "Jay Chou",
            ServiceMessageCodec.Key("Artist.Source.NetEaseMusic"));

        Assert.NotNull(result);
        Assert.StartsWith(ServiceMessageCodec.Prefix, result!.Summary);
        Assert.Equal(
            "Notable albums include: Fantasy, Still Fantasy",
            ServiceMessageCodec.Resolve(result.Summary, "en-US"));
    }
}
