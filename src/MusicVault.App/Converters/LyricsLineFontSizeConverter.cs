using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicVault.App.Converters;

/// <summary>歌词行字号：普通=基础字号，高亮=基础+增量</summary>
public sealed class LyricsLineFontSizeConverter : IMultiValueConverter
{
    public static readonly LyricsLineFontSizeConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is true;
        var baseSize = values.Count > 1 && values[1] is double d ? d : 22.0;
        var activeDelta = values.Count > 2 && values[2] is double deltaFromBinding
            ? deltaFromBinding
            : parameter switch
            {
                double delta => delta,
                string s when double.TryParse(s, NumberStyles.Any, culture, out var parsed) => parsed,
                _ => 5.0
            };
        return isActive ? baseSize + activeDelta : baseSize;
    }
}
