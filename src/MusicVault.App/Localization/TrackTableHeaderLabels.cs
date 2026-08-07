namespace MusicVault.App.Localization;

/// <summary>曲库/传输/队列等表格列头文案。</summary>
public static class TrackTableHeaderLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;

    public static string ColIndex => "#";

    public static string ColSelect => G("Organize.Column.Select");

    public static string ColSelectFull => G("Table.Col.SelectFull");

    public static string ColTitle => G("Discover.Column.Song");

    public static string ColArtistLibrary => G("Home.History.Column.Artist");

    public static string ColArtistDiscover => G("Discover.Column.Artist");

    public static string ColSource => G("Discover.Column.Source");

    public static string ColCache => G("Discover.Column.Cache");

    public static string ColAlbum => G("Home.History.Column.Album");

    public static string ColYear => G("Organize.Column.Year");

    public static string ColDuration => G("Discover.Column.Duration");

    public static string ColFavorite => G("Discover.Column.Favorite");

    public static string ColPlaylist => G("Discover.Column.Playlist");

    public static string ColActions => G("Discover.Column.Actions");

    public static string ColStatus => G("Table.Col.Status");

    public static string ColFormat => G("Table.Col.Format");

    public static string ColSize => G("Table.Col.Size");
}
