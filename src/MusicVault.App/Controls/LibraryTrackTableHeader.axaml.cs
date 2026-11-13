using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class LibraryTrackTableHeader : UserControl
{
    public static readonly StyledProperty<bool> ScrollPairedProperty =
        AvaloniaProperty.Register<LibraryTrackTableHeader, bool>(nameof(ScrollPaired), true);

    public bool ScrollPaired
    {
        get => GetValue(ScrollPairedProperty);
        set => SetValue(ScrollPairedProperty, value);
    }

    /// <summary>供 <see cref="Views.TrackTablePairedLayout"/> 同步滚动条 gutter。</summary>
    public Grid PairingHeaderGrid => HeaderGrid;

    public LibraryTrackTableHeader()
    {
        InitializeComponent();
        ApplyHeaderLabels();
        TrackTableHeaderLabels.LabelsChanged += (_, _) => ApplyHeaderLabels();
        UpdatePairedClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollPairedProperty)
            UpdatePairedClasses();
    }

    private void ApplyHeaderLabels()
    {
        HColIndex.Text = TrackTableHeaderLabels.ColIndex;
        HColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HColArtist.Text = TrackTableHeaderLabels.ColArtistLibrary;
        HColAlbum.Text = TrackTableHeaderLabels.ColAlbum;
        HColYear.Text = TrackTableHeaderLabels.ColYear;
        HColDuration.Text = TrackTableHeaderLabels.ColDuration;
        HColFavorite.Text = TrackTableHeaderLabels.ColFavorite;
        HColPlaylist.Text = TrackTableHeaderLabels.ColPlaylist;
        HColActions.Text = TrackTableHeaderLabels.ColActions;
    }

    private void UpdatePairedClasses()
    {
        if (HeaderGrid == null || HeaderDivider == null)
            return;

        if (ScrollPaired)
        {
            HeaderGrid.Classes.Add("track-table-header-paired");
            HeaderDivider.Classes.Add("track-table-divider-paired");
        }
        else
        {
            HeaderGrid.Classes.Remove("track-table-header-paired");
            HeaderDivider.Classes.Remove("track-table-divider-paired");
        }
    }
}
