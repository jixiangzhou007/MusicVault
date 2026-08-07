using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>将 0–1 音量映射为垂直音量条填充高度</summary>
public sealed class VolumeToBarHeightConverter : IValueConverter
{
    public static readonly VolumeToBarHeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var trackHeight = parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var h)
            ? h
            : 128d;

        var volume = value switch
        {
            float f => f,
            double d => d,
            _ => 0d
        };

        return Math.Max(0, volume) * trackHeight;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
