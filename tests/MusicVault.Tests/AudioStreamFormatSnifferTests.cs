using MusicVault.Core.Audio;
using Xunit;

namespace MusicVault.Tests;

public sealed class AudioStreamFormatSnifferTests
{
    [Fact]
    public void Detect_recognizes_flac_and_mp3_headers()
    {
        ReadOnlySpan<byte> flac = "fLaC"u8;
        ReadOnlySpan<byte> id3 = "ID3"u8;
        ReadOnlySpan<byte> mp3Frame = [0xFF, 0xFB];

        Assert.Equal(DetectedAudioFormat.Flac, AudioStreamFormatSniffer.Detect(flac));
        Assert.Equal(DetectedAudioFormat.Mp3, AudioStreamFormatSniffer.Detect(id3));
        Assert.Equal(DetectedAudioFormat.Mp3, AudioStreamFormatSniffer.Detect(mp3Frame));
    }

    [Fact]
    public void ProbeFile_detects_flac_after_id3_prefix()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mv_id3_flac_{Guid.NewGuid():N}.mp3");
        try
        {
            using (var fs = File.Create(path))
            {
                fs.Write("ID3"u8);
                fs.WriteByte(3);
                fs.WriteByte(0);
                fs.WriteByte(0);
                var sizeBytes = new byte[] { 0, 0, 0, 0x0E };
                fs.Write(sizeBytes);
                fs.Write(new byte[14]);
                fs.Write("fLaC"u8);
                fs.Write(new byte[32]);
            }

            var probe = AudioStreamFormatSniffer.ProbeFile(path);
            Assert.Equal(DetectedAudioFormat.Flac, probe.Format);
            Assert.True(probe.ContentStartOffset > 0);
            Assert.True(AudioFormatMislabel.IsId3WrappedFlac(path));
            Assert.False(AudioFormatMislabel.IsMp3ContentWithFlacExtension(path));
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ProbeFile_does_not_treat_id3_wrapped_flac_as_surface_mp3()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mv_id3_flac2_{Guid.NewGuid():N}.flac");
        try
        {
            using (var fs = File.Create(path))
            {
                fs.Write("ID3"u8);
                fs.WriteByte(4);
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.Write(new byte[] { 0, 0, 2, 0x0E });
                fs.Write(new byte[270]);
                fs.Write("fLaC"u8);
                fs.Write(new byte[16]);
            }

            Assert.True(AudioFormatMislabel.IsId3WrappedFlac(path));
            Assert.False(AudioFormatMislabel.IsMp3ContentWithFlacExtension(path));
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
