using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>整型是否等于 ConverterParameter（用于 Segmented 选中态）。</summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public static readonly IntEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int current)
            return false;

        return parameter switch
        {
            int p => current == p,
            string s when int.TryParse(s, NumberStyles.Integer, culture, out var p) => current == p,
            _ => int.TryParse(parameter?.ToString(), NumberStyles.Integer, culture, out var parsed) && current == parsed
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
