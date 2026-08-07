#if WINDOWS

using MusicVault.Services;

using NAudio.Wave;

using NAudio.Wave.SampleProviders;



namespace MusicVault.Services.Audio;



/// <summary>为网易云听歌识曲提取 8kHz 单声道 float 片段（与官方 Demo 一致）。</summary>

public static class NetEaseRecognizeClipExtractor

{

    public const int SampleRate = 8000;

    public const int MinDurationSeconds = 5;

    public const int MaxDurationSeconds = 15;

    public const int DefaultDurationSeconds = 8;

    public const int MaxCaptureSeconds = 20;



    public static int ResolveClipSeconds(string filePath, int? preferredSeconds = null)

    {

        var target = preferredSeconds ?? DefaultDurationSeconds;

        target = Math.Clamp(target, MinDurationSeconds, MaxDurationSeconds);



        try

        {

            using var stream = AudioDecoderFactory.Open(filePath);

            var total = stream.TotalTime.TotalSeconds;

            if (total > 0.5)

            {

                var available = (int)Math.Floor(total);

                if (available < MinDurationSeconds)

                    return Math.Max(1, available);



                return Math.Clamp(Math.Min(available, target), MinDurationSeconds, MaxDurationSeconds);

            }

        }

        catch

        {

            // fall back to target

        }



        return target;

    }



    public static float[] Extract(string filePath, int durationSeconds = DefaultDurationSeconds, bool preferTail = true)

    {

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))

            throw new FileNotFoundException(ServiceMessageCodec.Key("Recognition.AudioNotFound"), filePath);



        durationSeconds = Math.Clamp(durationSeconds, 1, MaxDurationSeconds);

        var clipSamples = durationSeconds * SampleRate;

        var maxReadSamples = MaxCaptureSeconds * SampleRate;



        using var stream = AudioDecoderFactory.Open(filePath);

        ISampleProvider samples = stream.ToSampleProvider();

        if (samples.WaveFormat.Channels > 1)

            samples = new StereoToMonoSampleProvider(samples);



        var resampled = new WdlResamplingSampleProvider(samples, SampleRate);

        var buffer = new float[4096];

        var collected = new List<float>(Math.Min(maxReadSamples, clipSamples * 2));



        while (collected.Count < maxReadSamples)

        {

            var toRead = Math.Min(buffer.Length, maxReadSamples - collected.Count);

            var read = resampled.Read(buffer, 0, toRead);

            if (read <= 0)

                break;



            for (var i = 0; i < read; i++)

                collected.Add(buffer[i]);

        }



        if (collected.Count < SampleRate / 2)

            throw new InvalidOperationException(ServiceMessageCodec.Key("Recognition.ClipTooShort"));



        if (preferTail && collected.Count > clipSamples)

            return collected.Skip(collected.Count - clipSamples).ToArray();



        if (collected.Count > clipSamples)

            return collected.Take(clipSamples).ToArray();



        return collected.ToArray();

    }



    public static async Task<string> WriteTempPcmAsync(float[] samples, CancellationToken ct = default)

    {

        var dir = Path.Combine(Path.GetTempPath(), "MusicVault", "recognize");

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"clip_{Guid.NewGuid():N}.f32le");

        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

        var bytes = new byte[samples.Length * sizeof(float)];

        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

        await fs.WriteAsync(bytes, ct).ConfigureAwait(false);

        return path;

    }

}

#endif

