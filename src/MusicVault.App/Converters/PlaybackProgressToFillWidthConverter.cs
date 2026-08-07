using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>播放进度 → 进度条填充宽度（Position / Duration × 轨道宽）</summary>
public sealed class PlaybackProgressToFillWidthConverter : IMultiValueConverter
{
    public static readonly PlaybackProgressToFillWidthConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3) return 0d;

        var position = values[0] switch { double d => d, float f => f, _ => 0d };
        var duration = values[1] switch { double d => d, float f => f, _ => 0d };
        var trackWidth = values[2] switch { double d => d, float f => f, _ => 0d };

        if (trackWidth <= 0) return 0d;
        if (duration <= 0) return position > 0 ? trackWidth : 0d;
        return Math.Clamp(position / duration, 0, 1) * trackWidth;
    }
}
