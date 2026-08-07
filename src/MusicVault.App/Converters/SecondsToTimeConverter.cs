using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>秒数转 mm:ss 时间文本</summary>
public sealed class SecondsToTimeConverter : IValueConverter
{
    public static readonly SecondsToTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var seconds = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0
        };
        if (seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
