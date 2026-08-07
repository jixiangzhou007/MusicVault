using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>将 -20~+20 dB 均衡增益映射为垂直滑条填充高度（自底部向上）</summary>
public sealed class EqGainToBarHeightConverter : IValueConverter
{
    public static readonly EqGainToBarHeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var trackHeight = parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var h)
            ? h
            : 88d;

        var gain = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0d
        };

        var normalized = (gain + 20d) / 40d;
        return Math.Clamp(normalized, 0, 1) * trackHeight;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
