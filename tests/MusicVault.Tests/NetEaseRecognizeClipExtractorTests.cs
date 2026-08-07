using MusicVault.Services.Audio;
using NAudio.Wave;
using Xunit;

namespace MusicVault.Tests;

public sealed class NetEaseRecognizeClipExtractorTests
{
    [Theory]
    [InlineData(null, NetEaseRecognizeClipExtractor.DefaultDurationSeconds)]
    [InlineData(3, NetEaseRecognizeClipExtractor.MinDurationSeconds)]
    [InlineData(20, NetEaseRecognizeClipExtractor.MaxDurationSeconds)]
    public void ResolveClipSeconds_clamps_preferred_when_file_missing(int? preferred, int expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mv_missing_{Guid.NewGuid():N}.mp3");
        var actual = NetEaseRecognizeClipExtractor.ResolveClipSeconds(path, preferred);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Extract_preferTail_takes_samples_from_end_of_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mv_clip_{Guid.NewGuid():N}.wav");
        const int sampleRate = 44100;
        const int leadSeconds = 6;
        const int tailSeconds = 8;
        const int totalSeconds = leadSeconds + tailSeconds;

        try
        {
            WriteTestWave(path, sampleRate, totalSeconds, leadSeconds);

            var headClip = NetEaseRecognizeClipExtractor.Extract(path, tailSeconds, preferTail: false);
            var tailClip = NetEaseRecognizeClipExtractor.Extract(path, tailSeconds, preferTail: true);

            Assert.Equal(tailSeconds * NetEaseRecognizeClipExtractor.SampleRate, headClip.Length);
            Assert.Equal(tailSeconds * NetEaseRecognizeClipExtractor.SampleRate, tailClip.Length);
            Assert.True(AverageAbs(tailClip) > AverageAbs(headClip) * 4);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private static void WriteTestWave(string path, int sampleRate, int totalSeconds, int silentLeadSeconds)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        using var writer = new WaveFileWriter(path, format);
        var totalSamples = sampleRate * totalSeconds;
        var silentSamples = sampleRate * silentLeadSeconds;
        var buffer = new float[4096];
        var written = 0;

        while (written < totalSamples)
        {
            var chunk = Math.Min(buffer.Length, totalSamples - written);
            for (var i = 0; i < chunk; i++)
            {
                var index = written + i;
                buffer[i] = index < silentSamples
                    ? 0f
                    : (float)Math.Sin(2 * Math.PI * 440 * index / sampleRate) * 0.8f;
            }

            writer.WriteSamples(buffer, 0, chunk);
            written += chunk;
        }
    }

    private static double AverageAbs(IReadOnlyList<float> samples)
    {
        if (samples.Count == 0)
            return 0;

        double sum = 0;
        foreach (var sample in samples)
            sum += Math.Abs(sample);

        return sum / samples.Count;
    }
}
