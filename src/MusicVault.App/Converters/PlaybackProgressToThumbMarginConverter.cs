using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>播放进度 → 进度条旋钮左边距。</summary>
public sealed class PlaybackProgressToThumbMarginConverter : IMultiValueConverter
{
    public const double ThumbSize = 10d;
    public static readonly PlaybackProgressToThumbMarginConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3)
            return new Thickness(0);

        var position = values[0] switch { double d => d, float f => f, _ => 0d };
        var duration = values[1] switch { double d => d, float f => f, _ => 0d };
        var trackWidth = values[2] switch { double d => d, float f => f, _ => 0d };

        if (trackWidth <= 0)
            return new Thickness(0);

        var ratio = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
        var left = ratio * Math.Max(0, trackWidth - ThumbSize);
        // 旋钮中心对齐 1px 顶部分隔/轨道线
        const double top = -4.5;
        return new Thickness(left, top, 0, 0);
    }
}
