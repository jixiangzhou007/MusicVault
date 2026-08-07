using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>整数大于 0 时返回 true</summary>
public sealed class IntToVisibilityConverter : IValueConverter
{
    public static readonly IntToVisibilityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
