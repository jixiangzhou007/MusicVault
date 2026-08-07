#if WINDOWS
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicVault.Services.Audio;

/// <summary>对采样流应用多段 Peaking EQ</summary>
public sealed class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _sampleRate;
    private readonly float[] _frequencies;
    private readonly float[] _gainsDb;
    private BiQuadFilter[][] _filters = [];

    public EqualizerSampleProvider(ISampleProvider source, float[] centerFrequencies, float[] gainsDb)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        _sampleRate = WaveFormat.SampleRate;
        _frequencies = centerFrequencies;
        _gainsDb = gainsDb;
        RebuildFilters();
    }

    public WaveFormat WaveFormat { get; }

    public void SetBandGain(int bandIndex, float gainDb)
    {
        if (bandIndex < 0 || bandIndex >= _gainsDb.Length) return;
        _gainsDb[bandIndex] = gainDb;
        RebuildFilters();
    }

    public void SetGains(float[] gainsDb)
    {
        var count = Math.Min(gainsDb.Length, _gainsDb.Length);
        for (var i = 0; i < count; i++)
            _gainsDb[i] = gainsDb[i];
        RebuildFilters();
    }

    private void RebuildFilters()
    {
        var channels = WaveFormat.Channels;
        _filters = new BiQuadFilter[_frequencies.Length][];
        for (var b = 0; b < _frequencies.Length; b++)
        {
            _filters[b] = new BiQuadFilter[channels];
            for (var c = 0; c < channels; c++)
                _filters[b][c] = BiQuadFilter.PeakingEQ(_sampleRate, _frequencies[b], 1.0f, _gainsDb[b]);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (_filters.Length == 0) return read;

        var channels = WaveFormat.Channels;
        for (var i = 0; i < read; i += channels)
        {
            for (var c = 0; c < channels; c++)
            {
                var idx = offset + i + c;
                var sample = buffer[idx];
                for (var b = 0; b < _filters.Length; b++)
                    sample = _filters[b][c].Transform(sample);
                buffer[idx] = sample;
            }
        }

        return read;
    }
}
#endif
