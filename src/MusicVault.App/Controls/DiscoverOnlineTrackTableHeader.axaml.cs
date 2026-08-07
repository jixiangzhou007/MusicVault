using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class DiscoverOnlineTrackTableHeader : UserControl
{
    public static readonly StyledProperty<bool> ScrollPairedProperty =
        AvaloniaProperty.Register<DiscoverOnlineTrackTableHeader, bool>(nameof(ScrollPaired), true);

    public bool ScrollPaired
    {
        get => GetValue(ScrollPairedProperty);
        set => SetValue(ScrollPairedProperty, value);
    }

    public Grid HeaderGrid => DiscoverHeaderGrid;

    public DiscoverOnlineTrackTableHeader()
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

    private void UpdatePairedClasses()
    {
        if (DiscoverHeaderGrid == null || HeaderDivider == null)
            return;

        if (ScrollPaired)
        {
            DiscoverHeaderGrid.Classes.Add("track-table-header-paired");
            HeaderDivider.Classes.Add("track-table-divider-paired");
        }
        else
        {
            DiscoverHeaderGrid.Classes.Remove("track-table-header-paired");
            HeaderDivider.Classes.Remove("track-table-divider-paired");
        }
    }

    private void ApplyHeaderLabels()
    {
        HColIndex.Text = TrackTableHeaderLabels.ColIndex;
        HColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HColSource.Text = TrackTableHeaderLabels.ColSource;
        HColDuration.Text = TrackTableHeaderLabels.ColDuration;
        HColCache.Text = TrackTableHeaderLabels.ColCache;
        HColFavorite.Text = TrackTableHeaderLabels.ColFavorite;
        HColPlaylist.Text = TrackTableHeaderLabels.ColPlaylist;
        HColActions.Text = TrackTableHeaderLabels.ColActions;
    }
}
