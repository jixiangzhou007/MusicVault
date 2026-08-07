using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>布尔值转侧边栏宽度（展开 360，收起 0）</summary>
public sealed class BoolToWidthConverter : IValueConverter
{
    public static readonly BoolToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var open = value is true;
        var width = parameter is string s && double.TryParse(s, out var w) ? w : 360;
        return open ? width : 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d && d > 0;
}
