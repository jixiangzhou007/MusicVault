using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;

namespace MusicVault.Tests;

public sealed class TrackRepositoryMissingMetadataFilterTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;
    private readonly TrackRepository _tracks;
    private readonly PlaylistRepository _playlists;
    private readonly long _libraryId;

    public TrackRepositoryMissingMetadataFilterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mv-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "musicvault.db");
        DatabaseInitializer.InitializeAsync(_dbPath).GetAwaiter().GetResult();
        _tracks = new TrackRepository(_dbPath);
        _playlists = new PlaylistRepository(_dbPath);
        _libraryId = new LibraryRepository(_dbPath).AddAsync(new MusicLibrary
        {
            Name = "Missing",
            RootPath = _root,
            IsEnabled = true
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Defaults_keep_tracks_that_have_cover_or_lyrics()
    {
        await SeedAsync();

        var all = await _tracks.GetByLibraryAsync(_libraryId, 0, 50);
        var counted = await _tracks.CountAsync(_libraryId);

        Assert.Equal(6, all.Count);
        Assert.Equal(6, counted);
        Assert.Contains(all, t => t.Title == "complete");
    }

    [Fact]
    public async Task MissingCover_treats_null_empty_and_whitespace_as_missing_before_page()
    {
        await SeedAsync();

        var listed = await _tracks.GetByLibraryAsync(_libraryId, 0, 50, missingCover: true);
        var counted = await _tracks.CountAsync(_libraryId, missingCover: true);
        var allLibraries = await _tracks.CountAsync(libraryId: null, missingCover: true);
        var page = await _tracks.GetByLibraryScopeAsync(
            _libraryId, _root, skip: 1, take: 1, missingCover: true);
        var scopeCount = await _tracks.CountByLibraryScopeAsync(_libraryId, _root, missingCover: true);

        Assert.Equal(["blank-cover", "neither", "empty-cover"], listed.Select(t => t.Title).ToArray());
        Assert.Equal(3, counted);
        Assert.Equal(3, allLibraries);
        Assert.Equal(3, scopeCount);
        Assert.Equal("neither", Assert.Single(page).Title);
    }

    [Fact]
    public async Task MissingLyrics_treats_null_empty_and_whitespace_as_missing()
    {
        await SeedAsync();

        var listed = await _tracks.GetByLibraryAsync(_libraryId, 0, 50, missingLyrics: true);
        var counted = await _tracks.CountAsync(_libraryId, missingLyrics: true);

        Assert.Equal(["neither", "tab-lyrics", "empty-lyrics"], listed.Select(t => t.Title).ToArray());
        Assert.Equal(3, counted);
    }

    [Fact]
    public async Task Both_flags_require_cover_and_lyrics_to_be_missing()
    {
        await SeedAsync();

        var listed = await _tracks.GetByLibraryAsync(
            _libraryId, 0, 50, missingCover: true, missingLyrics: true);
        var counted = await _tracks.CountAsync(_libraryId, missingCover: true, missingLyrics: true);
        var scope = await _tracks.GetByLibraryScopeAsync(
            _libraryId, _root, 0, 50, missingCover: true, missingLyrics: true);

        Assert.Equal(["neither"], listed.Select(t => t.Title).ToArray());
        Assert.Equal(1, counted);
        Assert.Equal(["neither"], scope.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task Search_and_playlist_paths_apply_the_same_flags()
    {
        var ids = await SeedAsync();
        var playlistId = await _playlists.CreateAsync("Missing filters");
        for (var i = 0; i < ids.Count; i++)
            await _playlists.AddTrackAsync(playlistId, ids[i], i);

        var search = await _tracks.SearchScopedAsync(
            "Tune", libraryId: _libraryId, missingLyrics: true);
        var searchCount = await _tracks.CountSearchScopedAsync(
            "Tune", libraryId: _libraryId, missingLyrics: true);
        var playlistSearch = await _tracks.SearchScopedAsync(
            "Tune", playlistId: playlistId, missingCover: true);
        var playlistSearchCount = await _tracks.CountSearchScopedAsync(
            "Tune", playlistId: playlistId, missingCover: true);
        var playlist = await _tracks.GetByPlaylistAsync(playlistId, 0, 50, missingCover: true);
        var playlistCount = await _tracks.CountByPlaylistAsync(playlistId, missingCover: true);
        var unfilteredPlaylistCount = await _tracks.CountByPlaylistAsync(playlistId);

        Assert.Equal(["neither", "tab-lyrics", "empty-lyrics"], search.Select(t => t.Title).ToArray());
        Assert.Equal(3, searchCount);
        Assert.Equal(["blank-cover", "neither", "empty-cover"], playlistSearch.Select(t => t.Title).ToArray());
        Assert.Equal(3, playlistSearchCount);
        Assert.Equal(["blank-cover", "neither", "empty-cover"], playlist.Select(t => t.Title).ToArray());
        Assert.Equal(3, playlistCount);
        Assert.Equal(6, unfilteredPlaylistCount);
    }

    private async Task<IReadOnlyList<long>> SeedAsync()
    {
        var ids = new List<long>
        {
            await InsertAsync("A", "blank-cover", "   ", "a.lrc"),
            await InsertAsync("B", "complete", "embedded", "b.lrc"),
            await InsertAsync("C", "neither", null, null),
            await InsertAsync("D", "empty-cover", "", "d.lrc"),
            await InsertAsync("E", "tab-lyrics", "hash", "\t"),
            await InsertAsync("F", "empty-lyrics", "embedded", "")
        };
        return ids;
    }

    private async Task<long> InsertAsync(string artist, string title, string? coverHash, string? lyricsPath)
    {
        return await _tracks.UpsertAsync(new Track
        {
            LibraryId = _libraryId,
            FilePath = Path.Combine(_root, artist + "-" + title + ".mp3"),
            Title = title,
            Artist = artist,
            Album = "Tune",
            CoverHash = coverHash,
            LyricsPath = lyricsPath
        });
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // ignore locked sqlite files
        }
    }
}
