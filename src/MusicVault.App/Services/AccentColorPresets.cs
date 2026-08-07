namespace MusicVault.App.Services;

/// <summary>设置页可选的强调色预设（无需记忆色值）。</summary>
public static class AccentColorPresets
{
    public sealed record Option(string Name, string Hex);

    public static IReadOnlyList<Option> All { get; } =
    [
        new("蓝", AccentPalette.DefaultAccentHex),
        new("橙", "#FF9500"),
        new("红", "#FF3B30"),
        new("绿", "#34C759"),
        new("紫", "#AF52DE"),
        new("青", "#00C7BE"),
        new("粉", "#FF375F"),
    ];

    public static string Normalize(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return AccentPalette.DefaultAccentHex;

        var trimmed = hex.Trim();
        if (string.Equals(trimmed, "#2D7DFF", StringComparison.OrdinalIgnoreCase))
            return AccentPalette.DefaultAccentHex;

        foreach (var option in All)
        {
            if (string.Equals(option.Hex, trimmed, StringComparison.OrdinalIgnoreCase))
                return option.Hex;
        }

        return AccentPalette.DefaultAccentHex;
    }
}
