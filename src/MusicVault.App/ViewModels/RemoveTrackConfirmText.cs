using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels;

/// <summary>曲目移除 Popconfirm 文案（歌曲页 / 艺术家页共用）。</summary>
public static class RemoveTrackConfirmText
{
    public static string Title(ILocalizationService l10n, TrackItemViewModel item) =>
        string.Format(l10n.Get("Library.RemoveTrack.ConfirmTitle"), DisplayName(item));

    public static string Description(ILocalizationService l10n, bool canDeleteFile) =>
        canDeleteFile
            ? l10n.Get("Library.RemoveTrack.DescWithFile")
            : l10n.Get("Library.RemoveTrack.DescNoFile");

    public static string IndexOnlyConfirmText(ILocalizationService l10n, bool canDeleteFile) =>
        canDeleteFile ? l10n.Get("Library.RemoveTrack.IndexOnly") : l10n.Get("Library.RemoveTrack.Remove");

    public static bool CanDeleteFile(TrackItemViewModel item)
    {
        var path = item.Track.FilePath;
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    private static string DisplayName(TrackItemViewModel item) =>
        string.IsNullOrWhiteSpace(item.Title) ? item.Track.FilePath : item.Title;
}
