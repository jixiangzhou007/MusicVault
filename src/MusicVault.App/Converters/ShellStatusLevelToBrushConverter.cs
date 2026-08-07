using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MusicVault.App.Services;

namespace MusicVault.App.Converters;

public sealed class ShellStatusLevelToBrushConverter : IValueConverter
{
    public static readonly ShellStatusLevelToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShellStatusLevel level)
            return Brushes.Transparent;

        var key = level switch
        {
            ShellStatusLevel.Error => "DangerBrush",
            ShellStatusLevel.Warning => "WarningTextBrush",
            _ => "AccentBrush"
        };

        if (Application.Current?.TryGetResource(key, null, out var resource) == true && resource is IBrush brush)
            return brush;

        return Brushes.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
