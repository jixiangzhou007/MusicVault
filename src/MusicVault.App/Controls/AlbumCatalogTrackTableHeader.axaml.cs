using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class AlbumCatalogTrackTableHeader : UserControl
{
    public AlbumCatalogTrackTableHeader()
    {
        InitializeComponent();
        ApplyHeaderLabels();
        TrackTableHeaderLabels.LabelsChanged += (_, _) => ApplyHeaderLabels();
    }

    private void ApplyHeaderLabels()
    {
        HColSelect.Text = TrackTableHeaderLabels.ColSelect;
        HColIndex.Text = TrackTableHeaderLabels.ColIndex;
        HColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HColArtist.Text = TrackTableHeaderLabels.ColArtistLibrary;
        HColDuration.Text = TrackTableHeaderLabels.ColDuration;
        HColFormat.Text = TrackTableHeaderLabels.ColFormat;
        HColStatus.Text = TrackTableHeaderLabels.ColStatus;
        HColFavorite.Text = TrackTableHeaderLabels.ColFavorite;
        HColPlaylist.Text = TrackTableHeaderLabels.ColPlaylist;
        HColActions.Text = TrackTableHeaderLabels.ColActions;
    }
}
