namespace MusicVault.Core.Ui;

/// <summary>界面字号档位（与 App 层 TypographyScale 共用）。</summary>
public static class UiFontScale
{
    public static double GetMultiplier(int scaleIndex) => scaleIndex switch
    {
        1 => 1.12,
        2 => 1.22,
        _ => 1.0
    };

    public static int ClampIndex(int index) => Math.Clamp(index, 0, 2);

    public static string GetLabel(int scaleIndex) => ClampIndex(scaleIndex) switch
    {
        1 => "较大",
        2 => "特大",
        _ => "标准"
    };
}
