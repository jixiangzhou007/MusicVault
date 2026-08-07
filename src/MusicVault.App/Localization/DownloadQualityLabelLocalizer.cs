using MusicVault.Core.Enums;
using MusicVault.Services.Download;

namespace MusicVault.App.Localization;

/// <summary>将 <see cref="DownloadQualityLabels"/> 编码键解析为当前 UI 语言。</summary>
public static class DownloadQualityLabelLocalizer
{
    public static string Chip(ILocalizationService l10n, AudioQuality quality, MusicPlatform? platform = null) =>
        ServiceMessageLocalizer.Resolve(l10n, DownloadQualityLabels.Chip(quality, platform));

    public static string Describe(
        ILocalizationService l10n,
        AudioQuality quality,
        string? targetPath = null,
        MusicPlatform? platform = null) =>
        ServiceMessageLocalizer.Resolve(l10n, DownloadQualityLabels.Describe(quality, targetPath, platform));
}
