namespace MusicVault.App.Localization;

/// <summary>共享列表行操作按钮文案（ToolTip / 无障碍 Name）。</summary>
public static class TrackRowActionLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;

    public static string Favorite => G("Discover.Column.Favorite");
    public static string AddToPlaylist => G("Discover.ToolTip.AddToPlaylist");
    public static string Download => G("Discover.ToolTip.Download");
    public static string Play => G("Common.Play");
    public static string DeleteFromLibrary => G("Library.Tracks.DeleteFromLibrary.ToolTip");
    public static string AddToOtherPlaylist => G("Playlists.Tracks.AddToOtherPlaylist.ToolTip");
    public static string RemoveFromPlaylist => G("Playlists.Tracks.RemoveFromPlaylist.ToolTip");
    public static string PauseDownload => G("Transfers.Row.Pause");
    public static string ResumeDownload => G("Transfers.Row.Resume");
    public static string CancelDownload => G("Transfers.Row.Cancel");
    public static string RetryDownload => G("Transfers.Row.Retry");
    public static string DeleteRecord => G("Transfers.Row.DeleteRecord");
    public static string OpenFolderFormat => G("Transfers.Row.OpenFolder");

    public static string RowCheckboxName(string title) =>
        string.Format(G("A11y.RowCheckbox.Name"), title);

    public static string RowCheckboxHelp(string title, string subtitle) =>
        string.Format(G("A11y.RowCheckbox.Help"), title, subtitle);

    public static string PlaylistCheckboxName(string playlistName) =>
        string.Format(G("A11y.PlaylistCheckbox.Name"), playlistName);
}
