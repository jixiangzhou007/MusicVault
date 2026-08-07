using MusicVault.Core.Audio;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Features;

namespace MusicVault.Tests;

public sealed class TrackExtensionCorrectionServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly TrackRepository _tracks;
    private readonly TrackExtensionCorrectionService _service;

    public TrackExtensionCorrectionServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mv_ext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        DatabaseInitializer.InitializeAsync(_dbPath).GetAwaiter().GetResult();
        _tracks = new TrackRepository(_dbPath);
        _service = new TrackExtensionCorrectionService(_tracks);
    }

    [Fact]
    public void Mislabel_detects_mp3_id3_with_flac_extension_only_when_body_is_mp3()
    {
        var wrappedFlac = Path.Combine(_dir, "wrapped.flac");
        using (var fs = File.Create(wrappedFlac))
        {
            fs.Write("ID3"u8);
            fs.WriteByte(3);
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.Write(new byte[] { 0, 0, 0, 0x0E });
            fs.Write(new byte[14]);
            fs.Write("fLaC"u8);
            fs.Write(new byte[8]);
        }

        Assert.False(AudioFormatMislabel.IsMp3ContentWithFlacExtension(wrappedFlac));

        var mp3Body = Path.Combine(_dir, "real_mp3.flac");
        using (var fs = File.Create(mp3Body))
        {
            fs.Write("ID3"u8);
            fs.WriteByte(3);
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.Write(new byte[] { 0, 0, 0, 0 });
            fs.WriteByte(0xFF);
            fs.WriteByte(0xFB);
            fs.Write(new byte[32]);
        }

        Assert.True(AudioFormatMislabel.IsMp3ContentWithFlacExtension(mp3Body));
    }

    [Fact]
    public async Task RepairId3WrappedFlac_strips_prefix_and_updates_database()
    {
        var oldPath = Path.Combine(_dir, "wrapped.mp3");
        await using (var fs = File.Create(oldPath))
        {
            fs.Write("ID3"u8);
            fs.WriteByte(3);
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.Write(new byte[] { 0, 0, 0, 0x0E });
            fs.Write(new byte[14]);
            fs.Write("fLaC"u8);
            fs.Write(new byte[64]);
        }

        var track = new Track
        {
            FilePath = oldPath,
            Title = "Song",
            Artist = "Artist",
            FileSizeBytes = new FileInfo(oldPath).Length
        };
        track.Id = await _tracks.UpsertAsync(track);

        var result = await _service.RepairId3WrappedFlacAsync(track);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_dir, "wrapped.flac"), result.NewPath);
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(result.NewPath!));
        var head = new byte[4];
        await using (var s = File.OpenRead(result.NewPath!))
            _ = await s.ReadAsync(head).ConfigureAwait(true);
        Assert.Equal("fLaC"u8, head);

        var fromDb = await _tracks.GetByIdAsync(track.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(result.NewPath, fromDb!.FilePath, ignoreCase: true);
    }

    [Fact]
    public async Task CorrectFlacToMp3_renames_file_and_updates_database()
    {
        var oldPath = Path.Combine(_dir, "song.flac");
        using (var fs = File.Create(oldPath))
        {
            fs.WriteByte(0xFF);
            fs.WriteByte(0xFB);
            fs.Write(new byte[64]);
        }

        var track = new Track
        {
            FilePath = oldPath,
            Title = "Song",
            Artist = "Artist",
            FileSizeBytes = 10
        };
        track.Id = await _tracks.UpsertAsync(track);

        var result = await _service.CorrectFlacToMp3Async(track);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_dir, "song.mp3"), result.NewPath);
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(result.NewPath!));
        Assert.Equal(result.NewPath, track.FilePath);

        var fromDb = await _tracks.GetByIdAsync(track.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(result.NewPath, fromDb!.FilePath, ignoreCase: true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch
        {
            // ignore
        }
    }
}
