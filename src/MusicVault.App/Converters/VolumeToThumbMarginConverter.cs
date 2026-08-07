using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>将 0–1 音量映射为横向滑块旋钮的左边距</summary>
public sealed class VolumeToThumbMarginConverter : IValueConverter
{
    public static readonly VolumeToThumbMarginConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        const double thumbSize = 11d;
        var trackWidth = parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var w)
            ? w
            : 108d;

        var volume = value switch
        {
            float f => f,
            double d => d,
            _ => 0d
        };

        volume = Math.Clamp(volume, 0, 1);
        var left = volume * Math.Max(0, trackWidth - thumbSize);
        return new Thickness(left, 0, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
