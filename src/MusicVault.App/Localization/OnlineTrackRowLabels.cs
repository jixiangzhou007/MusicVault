namespace MusicVault.App.Localization;

/// <summary>发现在线曲目行文案。</summary>
public static class OnlineTrackRowLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;

    public static string FavoriteAdd => G("Discover.OnlineFavorite.Add");

    public static string FavoriteRemove => G("Discover.OnlineFavorite.Remove");

    public static string PreviewCachedStatus => G("Discover.PreviewCache.Cached");

    public static string PreviewCachedToolTip => G("Discover.PreviewCache.CachedToolTip");

    public static string PreviewNotCachedToolTip => G("Discover.PreviewCache.NotCachedToolTip");

    public static string AlbumTrackInLibrary => G("Discover.AlbumTrack.InLibrary");

    public static string AlbumTrackNotInLibrary => G("Discover.AlbumTrack.NotInLibrary");

    public static string FormatNo128 => G("Discover.Format.No128");

    public static string FormatNo320 => G("Discover.Format.No320");

    public static string FormatNoFlac => G("Discover.Format.NoFlac");

    public static string SizeUnknown => G("Discover.Online.SizeUnknown");

    public static string Playing(string title) =>
        string.Format(G("Discover.Online.Playing"), title);

    public static string PreviewCancelled => G("Discover.Online.PreviewCancelled");

    public static string PlayFailed(string title) =>
        string.Format(G("Discover.Online.PlayFailed"), title);

    public static string PreviewTrying(string qualityLabel) =>
        string.Format(G("Discover.Online.PreviewTrying"), qualityLabel);

    public static string PreviewBuffering => G("Discover.Online.PreviewBuffering");

    public static string PlayFailedTraceSuffix(string trace) =>
        string.Format(G("Discover.Online.PlayFailedTrace"), trace);

    public static string PlayFailedQqHint => G("Discover.Online.PlayFailedQqHint");

    public static string FavoriteAdded(string title) =>
        string.Format(G("Discover.Online.FavoriteAdded"), title);

    public static string FavoriteRemoved(string title) =>
        string.Format(G("Discover.Online.FavoriteRemoved"), title);

    public static string EnqueueSkipped(string title, string label) =>
        string.Format(G("Discover.Online.EnqueueSkipped"), title, label);

    public static string EnqueueAdded(string title, string label) =>
        string.Format(G("Discover.Online.EnqueueAdded"), title, label);

    public static string EnqueueAddedToDir(string title, string label, string directory) =>
        string.Format(G("Discover.Online.EnqueueAddedToDir"), title, label, directory);

    public static string EnqueueDuplicate(string title) =>
        string.Format(G("Discover.Online.EnqueueDuplicate"), title);

    public static string EnqueueSingle(string title, string qualityLabel) =>
        string.Format(G("Discover.Online.EnqueueSingle"), title, qualityLabel);

    public static string NoDownloadQuality(string title) =>
        string.Format(G("Discover.Online.NoDownloadQuality"), title);

    public static string ActionFailed(string title, string label, string detail) =>
        string.Format(
            G("Discover.Online.ActionFailed"),
            title,
            label,
            ServiceMessageLocalizer.Resolve(_l10n!, detail));

    public static string StatusError(string title, string detail) =>
        string.Format(
            G("Discover.Online.StatusError"),
            title,
            ServiceMessageLocalizer.Resolve(_l10n!, detail));

    public static string PreviewFailureAuth => G("Discover.PreviewFailure.Auth");

    public static string PreviewFailureForbidden => G("Discover.PreviewFailure.Forbidden");

    public static string PreviewFailureTimeout => G("Discover.PreviewFailure.Timeout");

    public static string PreviewFailureNoLink => G("Discover.PreviewFailure.NoLink");

    public static string PreviewFailureGeneric => G("Discover.PreviewFailure.Generic");
}
