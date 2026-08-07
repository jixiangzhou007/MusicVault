using MusicVault.Core.Enums;

namespace MusicVault.App.Localization;

/// <summary>发现/整理/在线曲目等平台下拉与标签文案。</summary>
public static class MusicPlatformLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;

    public static IReadOnlyList<string> ComboLabels =>
    [
        G("Transfers.Platform.All"),
        G("Platform.NetEase"),
        G("Platform.QQMusic"),
        G("Platform.Kugou"),
        G("Platform.Kuwo"),
        G("Platform.Migu"),
        G("Platform.MyFreeMp3"),
        G("Platform.Jbsou"),
        G("Platform.Soda")
    ];

    public static IReadOnlyList<string> ScrapePlatformLabels => ComboLabels.Take(6).ToList();

    public static string GetLabel(MusicPlatform platform, string? unknownFallback = null) =>
        platform switch
        {
            MusicPlatform.All => G("Transfers.Platform.All"),
            MusicPlatform.NetEase => G("Platform.NetEase"),
            MusicPlatform.QQMusic => G("Platform.QQMusic"),
            MusicPlatform.Kugou => G("Platform.Kugou"),
            MusicPlatform.Kuwo => G("Platform.Kuwo"),
            MusicPlatform.Migu => G("Platform.Migu"),
            MusicPlatform.MyFreeMp3 => G("Platform.MyFreeMp3"),
            MusicPlatform.Jbsou => G("Platform.Jbsou"),
            MusicPlatform.Soda => G("Platform.Soda"),
            _ => unknownFallback ?? G("Transfers.Row.Status.Unknown")
        };
}
