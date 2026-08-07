using MusicVault.Services.Audio;

namespace MusicVault.Tests.Audio;

public sealed class PlaybackEngineSnapshotTests
{
    [Fact]
    public void WriteAndRead_RoundTripsStructuredFields()
    {
        var dir = CreateTempDir();
        try
        {
            var written = new PlaybackEngineSnapshot
            {
                Utc = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
                Engine = "LibVLC",
                PreferLibVlc = true,
                NativeRoot = @"D:\MusicVault\publish\win-x64-vol\libvlc\win-x64",
                Note = null
            };
            PlaybackEngineSnapshot.Write(dir, written);

            var read = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(read);
            Assert.Equal("LibVLC", read!.Engine);
            Assert.True(read.PreferLibVlc);
            Assert.Contains("libvlc", read.NativeRoot, StringComparison.OrdinalIgnoreCase);
            Assert.Null(read.Note);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_LegacyTabLine_ParsesEngineAndNote()
    {
        var dir = CreateTempDir();
        try
        {
            var path = PlaybackEngineSnapshot.GetPath(dir);
            File.WriteAllText(path, "2026-07-15T04:00:00.0000000Z\tNAudio\tLibVLC requested but unavailable");

            var read = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(read);
            Assert.Equal("NAudio", read!.Engine);
            Assert.True(read.PreferLibVlc);
            Assert.Equal("LibVLC requested but unavailable", read.Note);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FormatDetail_PrefersNoteOverNativeRoot()
    {
        var snapshot = new PlaybackEngineSnapshot
        {
            Engine = "NAudio",
            PreferLibVlc = true,
            NativeRoot = @"C:\libvlc\win-x64",
            Note = "LibVLC init failed"
        };
        Assert.Equal("LibVLC init failed", snapshot.FormatDetail());
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-engine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
