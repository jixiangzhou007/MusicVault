using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class DownloadQueueTableHeader : UserControl
{
    public DownloadQueueTableHeader()
    {
        InitializeComponent();
        ApplyHeaderLabels();
        TrackTableHeaderLabels.LabelsChanged += (_, _) => ApplyHeaderLabels();
    }

    internal Grid HeaderGridControl => HeaderGrid;

    internal Border HeaderDividerControl => HeaderDivider;

    private void ApplyHeaderLabels()
    {
        HColSelect.Text = TrackTableHeaderLabels.ColSelect;
        HColIndex.Text = TrackTableHeaderLabels.ColIndex;
        HColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HColArtist.Text = TrackTableHeaderLabels.ColArtistDiscover;
        HColStatus.Text = TrackTableHeaderLabels.ColStatus;
        HColFormat.Text = TrackTableHeaderLabels.ColFormat;
        HColSize.Text = TrackTableHeaderLabels.ColSize;
        HColActions.Text = TrackTableHeaderLabels.ColActions;
    }
}
