using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>枚举值是否等于 ConverterParameter</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        if (value is Enum left && parameter is Enum right)
            return left.Equals(right);

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
            return Avalonia.Data.BindingOperations.DoNothing;

        if (parameter is Enum enumParameter)
            return enumParameter;

        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
