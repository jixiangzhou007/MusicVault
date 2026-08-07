using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

public partial class CompactSixColumnTrackTableHeader : UserControl
{
    public static readonly StyledProperty<string> LeadingColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(LeadingColumnHeader), "#");

    public static readonly StyledProperty<string> TitleColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(TitleColumnHeader), string.Empty);

    public static readonly StyledProperty<string> ArtistColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(ArtistColumnHeader), string.Empty);

    public static readonly StyledProperty<string> AlbumColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(AlbumColumnHeader), string.Empty);

    public static readonly StyledProperty<string> DurationColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(DurationColumnHeader), string.Empty);

    public static readonly StyledProperty<string> FavoriteColumnHeaderProperty =
        AvaloniaProperty.Register<CompactSixColumnTrackTableHeader, string>(nameof(FavoriteColumnHeader), string.Empty);

    public string LeadingColumnHeader
    {
        get => GetValue(LeadingColumnHeaderProperty);
        set => SetValue(LeadingColumnHeaderProperty, value);
    }

    public string TitleColumnHeader
    {
        get => GetValue(TitleColumnHeaderProperty);
        set => SetValue(TitleColumnHeaderProperty, value);
    }

    public string ArtistColumnHeader
    {
        get => GetValue(ArtistColumnHeaderProperty);
        set => SetValue(ArtistColumnHeaderProperty, value);
    }

    public string AlbumColumnHeader
    {
        get => GetValue(AlbumColumnHeaderProperty);
        set => SetValue(AlbumColumnHeaderProperty, value);
    }

    public string DurationColumnHeader
    {
        get => GetValue(DurationColumnHeaderProperty);
        set => SetValue(DurationColumnHeaderProperty, value);
    }

    public string FavoriteColumnHeader
    {
        get => GetValue(FavoriteColumnHeaderProperty);
        set => SetValue(FavoriteColumnHeaderProperty, value);
    }

    public CompactSixColumnTrackTableHeader() => InitializeComponent();
}
