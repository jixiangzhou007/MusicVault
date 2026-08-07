using MusicVault.App.Localization;
using MusicVault.Core.Enums;

namespace MusicVault.App.Constants;

/// <summary>各页平台下拉共用：标签顺序与 ComboBox SelectedIndex 一致。</summary>
public static class MusicPlatformCatalog
{
    public static IReadOnlyList<string> Labels => MusicPlatformLabels.ComboLabels;

    public static MusicPlatform FromIndex(int index) => index switch
    {
        1 => MusicPlatform.NetEase,
        2 => MusicPlatform.QQMusic,
        3 => MusicPlatform.Kugou,
        4 => MusicPlatform.Kuwo,
        5 => MusicPlatform.Migu,
        6 => MusicPlatform.MyFreeMp3,
        7 => MusicPlatform.Jbsou,
        8 => MusicPlatform.Soda,
        _ => MusicPlatform.All
    };

    public static string GetLabel(MusicPlatform platform, string unknownFallback = "") =>
        MusicPlatformLabels.GetLabel(
            platform,
            string.IsNullOrEmpty(unknownFallback) ? null : unknownFallback);

    private static readonly HashSet<MusicPlatform> ExperimentalPlatforms =
    [
        MusicPlatform.QQMusic,
        MusicPlatform.Kugou,
        MusicPlatform.Kuwo,
        MusicPlatform.Migu,
        MusicPlatform.Soda
    ];

    public static bool IsExperimental(MusicPlatform platform) => ExperimentalPlatforms.Contains(platform);
}
