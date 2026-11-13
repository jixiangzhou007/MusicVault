using System.Globalization;
using Avalonia.Data.Converters;
using MusicVault.App.Icons;
using MusicVault.App.Services;

namespace MusicVault.App.Converters;

public sealed class MessageLevelToGlyphConverter : IValueConverter
{
    public static readonly MessageLevelToGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MessageLevel level) return IconGlyphs.Info;
        return level switch
        {
            MessageLevel.Success => IconGlyphs.CheckMark,
            MessageLevel.Error => IconGlyphs.Error,
            MessageLevel.Warning => IconGlyphs.Warning,
            _ => IconGlyphs.Info
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
