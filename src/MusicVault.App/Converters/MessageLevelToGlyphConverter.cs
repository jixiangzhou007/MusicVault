using System.Globalization;
using Avalonia.Data.Converters;
using MusicVault.App.Services;

namespace MusicVault.App.Converters;

public sealed class MessageLevelToGlyphConverter : IValueConverter
{
    public static readonly MessageLevelToGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MessageLevel level) return "\uE946";
        return level switch
        {
            MessageLevel.Success => "\uE73E",
            MessageLevel.Error => "\uE783",
            MessageLevel.Warning => "\uE7BA",
            _ => "\uE946"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
