using Avalonia.Media.Imaging;
using MusicVault.App.Localization;

namespace MusicVault.App.Metadata;

public static class CoverInfoHelper
{
    public static string BuildInfoText(
        byte[]? imageData,
        string? mimeType,
        string? pictureTypeLabel,
        ILocalizationService? l10n = null)
    {
        if (imageData is not { Length: > 0 })
            return string.Empty;

        var mime = string.IsNullOrWhiteSpace(mimeType) ? GuessMime(imageData) : mimeType.Trim();
        var sizeText = FormatFileSize(imageData.Length, l10n);
        var dimensions = TryGetDimensions(imageData);
        var typeLabel = string.IsNullOrWhiteSpace(pictureTypeLabel) ? string.Empty : pictureTypeLabel.Trim();
        var typeSuffix = string.IsNullOrEmpty(typeLabel) ? string.Empty : $", {typeLabel}";

        return dimensions is { Width: > 0, Height: > 0 }
            ? $"{mime}, {dimensions.Value.Width}×{dimensions.Value.Height}, {sizeText}{typeSuffix}"
            : $"{mime}, {sizeText}{typeSuffix}";
    }

    public static string GuessMime(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8)
            return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
            return "image/webp";
        return "image/jpeg";
    }

    public static string GuessExtension(byte[] data) =>
        GuessMime(data) switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

    private static (int Width, int Height)? TryGetDimensions(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var bitmap = new Bitmap(ms);
            return (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatFileSize(long bytes, ILocalizationService? l10n)
    {
        if (bytes < 1024)
            return FormatSize(l10n, "Common.FileSize.Bytes", bytes);

        var kb = bytes / 1024.0;
        return kb < 1024
            ? FormatSize(l10n, "Common.FileSize.Kilobytes", kb, "0.##")
            : FormatSize(l10n, "Common.FileSize.Megabytes", kb / 1024.0, "0.##");
    }

    private static string FormatSize(
        ILocalizationService? l10n,
        string key,
        double value,
        string? numberFormat = null)
    {
        var formatted = numberFormat != null ? value.ToString(numberFormat) : value.ToString();
        var template = l10n?.Get(key);
        if (string.IsNullOrEmpty(template) || string.Equals(template, key, StringComparison.Ordinal))
            return numberFormat != null ? $"{formatted}{FallbackUnit(key)}" : $"{formatted}B";

        return string.Format(template, formatted);
    }

    private static string FallbackUnit(string key) => key switch
    {
        "Common.FileSize.Kilobytes" => "KB",
        "Common.FileSize.Megabytes" => "MB",
        _ => "B"
    };
}
