using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>将 0–1 音量映射为横向滑条已填充宽度</summary>
public sealed class VolumeToSliderWidthConverter : IValueConverter
{
    public static readonly VolumeToSliderWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var trackWidth = parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var w)
            ? w
            : 120d;

        var volume = value switch
        {
            float f => f,
            double d => d,
            _ => 0d
        };

        return Math.Clamp(volume, 0, 1) * trackWidth;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
