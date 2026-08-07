using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Layout;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class QueueSidebarTableHeader : UserControl
{
    public const string ColumnDefinitions = TrackTableLayout.SidebarQueueColumns;

    public static readonly StyledProperty<bool> IsBatchModeProperty =
        AvaloniaProperty.Register<QueueSidebarTableHeader, bool>(nameof(IsBatchMode), false);

    public bool IsBatchMode
    {
        get => GetValue(IsBatchModeProperty);
        set => SetValue(IsBatchModeProperty, value);
    }

    public Grid ActiveHeaderGrid => IsBatchMode ? HeaderGridBatch : HeaderGrid;
    public Border HeaderDividerControl => HeaderDivider;

    public QueueSidebarTableHeader()
    {
        InitializeComponent();
        ApplyHeaderLabels();
        TrackTableHeaderLabels.LabelsChanged += (_, _) => ApplyHeaderLabels();
        ApplyBatchModeVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBatchModeProperty)
            ApplyBatchModeVisibility();
    }

    private void ApplyHeaderLabels()
    {
        HColIndex.Text = TrackTableHeaderLabels.ColIndex;
        HColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HColArtist.Text = TrackTableHeaderLabels.ColArtistLibrary;
        HColDuration.Text = TrackTableHeaderLabels.ColDuration;
        HColFavorite.Text = TrackTableHeaderLabels.ColFavorite;

        HBatchColSelect.Text = TrackTableHeaderLabels.ColSelectFull;
        HBatchColTitle.Text = TrackTableHeaderLabels.ColTitle;
        HBatchColArtist.Text = TrackTableHeaderLabels.ColArtistLibrary;
        HBatchColDuration.Text = TrackTableHeaderLabels.ColDuration;
    }

    private void ApplyBatchModeVisibility()
    {
        HeaderGrid.IsVisible = !IsBatchMode;
        HeaderGridBatch.IsVisible = IsBatchMode;
    }
}
