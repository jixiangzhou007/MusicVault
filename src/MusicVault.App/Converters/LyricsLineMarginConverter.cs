using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>歌词行边距：可指定固定行距，或随字号缩放。</summary>
public sealed class LyricsLineMarginConverter : IMultiValueConverter
{
    public static readonly LyricsLineMarginConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var font = values.Count > 0 && values[0] is double fd ? fd : 22.0;
        if (values.Count > 1 && values[1] is double lineV && lineV > 0)
            return new Thickness(0, lineV);
        return FromFont(font);
    }

    private static Thickness FromFont(double font)
    {
        var v = Math.Max(6, font * 0.45);
        var h = Math.Max(8, font * 0.55);
        return new Thickness(h, v);
    }
}
