using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Browse;
using MusicVault.App.Controls;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class ArtistsPageView : UserControl, IRemoveTrackConfirmPage
{
    private const double AlbumScrollStep = 134;
    private const double AlbumScrollOverflowEpsilon = 2;
    private ArtistsPageViewModel? _vm;
    private bool _albumScrollButtonsVisible = true;
    private bool _suppressArtistListSelection;

    public ArtistsPageView()
    {
        InitializeComponent();
        ArtistList.SelectionChanged += OnArtistListSelectionChanged;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttached;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => BindViewModel();

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => BindViewModel();

    private void BindViewModel()
    {
        if (_vm != null)
        {
            _vm.LetterScrollRequested -= OnLetterScrollRequested;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as ArtistsPageViewModel;
        if (_vm == null)
            return;

        _vm.LetterScrollRequested += OnLetterScrollRequested;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        WireArtistTracksIfNeeded();
        if (_vm is IAlbumCatalogOnlineTrackHost host)
        {
            AlbumCatalogTrackListInteraction.Wire(
                ArtistAlbumDetailTrackList,
                host,
                () => _vm.LoadMoreAlbumDetailTracksCommand.ExecuteAsync(null),
                visible => ArtistAlbumDetailScrollTopBtn.IsVisible = visible);
        }
        Dispatcher.UIThread.Post(UpdateAlbumScrollChrome, DispatcherPriority.Loaded);
    }

    private void OnArtistAlbumDetailScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(ArtistAlbumDetailTrackList);

    private void OnArtistSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm == null)
            return;

        e.Handled = true;
        _vm.ApplyArtistSearchCommand.Execute(null);
    }

    private void OnArtistListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressArtistListSelection || _vm == null || _vm.ShowingTracks)
            return;

        if (e.AddedItems.Count == 0)
            return;

        if (e.AddedItems[0] is ArtistSectionHeaderViewModel)
        {
            ClearArtistListSelection();
            return;
        }

        if (e.AddedItems[0] is not ArtistListItemViewModel artist)
            return;

        _ = _vm.SelectArtistCommand.ExecuteAsync(artist);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ArtistsPageViewModel.ShowingTracks)
            or nameof(ArtistsPageViewModel.ShowingAlbumTracks)
            or nameof(ArtistsPageViewModel.SelectedArtist))
        {
            WireArtistTracksIfNeeded();
            if (e.PropertyName is nameof(ArtistsPageViewModel.ShowingTracks)
                && _vm is { ShowingTracks: false })
                ClearArtistListSelection();
        }

        if (e.PropertyName is nameof(ArtistsPageViewModel.HasArtistAlbums)
            && AlbumScroll != null)
        {
            AlbumScroll.Offset = new Vector(0, 0);
            Dispatcher.UIThread.Post(UpdateAlbumScrollChrome, DispatcherPriority.Loaded);
        }
    }

    private void OnAlbumScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateAlbumScrollChrome();

    private void OnAlbumScrollSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateAlbumScrollChrome();

    private void UpdateAlbumScrollChrome()
    {
        if (AlbumScroll == null || AlbumCarouselGrid == null)
            return;

        var needsScroll = AlbumScroll.Extent.Width > AlbumScroll.Viewport.Width + AlbumScrollOverflowEpsilon;
        if (needsScroll == _albumScrollButtonsVisible)
            return;

        _albumScrollButtonsVisible = needsScroll;

        if (AlbumScrollLeftBtn != null)
            AlbumScrollLeftBtn.IsVisible = needsScroll;
        if (AlbumScrollRightBtn != null)
            AlbumScrollRightBtn.IsVisible = needsScroll;

        if (needsScroll)
        {
            AlbumCarouselGrid.ColumnDefinitions = new ColumnDefinitions("40,*,40");
            Grid.SetColumn(AlbumScroll, 1);
            AlbumScroll.Margin = new Thickness(6, 0);
            if (AlbumScrollLeftBtn != null)
                Grid.SetColumn(AlbumScrollLeftBtn, 0);
            if (AlbumScrollRightBtn != null)
                Grid.SetColumn(AlbumScrollRightBtn, 2);
        }
        else
        {
            AlbumCarouselGrid.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(AlbumScroll, 0);
            AlbumScroll.Margin = new Thickness(0);
            AlbumScroll.Offset = new Vector(0, 0);
        }
    }

    private void ClearArtistListSelection()
    {
        if (ArtistList == null)
            return;

        _suppressArtistListSelection = true;
        try
        {
            ArtistList.SelectedIndex = -1;
        }
        finally
        {
            _suppressArtistListSelection = false;
        }
    }

    private bool _artistTracksScrollWired;

    private void WireArtistTracksIfNeeded()
    {
        if (_vm is not { ShowingTracks: true, ShowingAlbumTracks: false })
            return;

        TrackListInteraction.Wire(this, ArtistTracks, new TrackListPageOptions
        {
            Tracks = null,
            SyncNowPlaying = true,
            LoadMoreAsync = () => _vm.LoadMoreArtistTracksCommand.ExecuteAsync(null)
        });

        if (_artistTracksScrollWired)
            return;

        OnlineListScrollInteraction.Wire(
            ArtistTracks,
            () => _vm.LoadMoreArtistTracksCommand.ExecuteAsync(null),
            visible => ArtistTracksScrollTopBtn.IsVisible = visible);
        _artistTracksScrollWired = true;
    }

    private void OnArtistTracksScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(ArtistTracks);

    private void OnAlbumScrollLeft(object? sender, RoutedEventArgs e) =>
        ScrollAlbums(-AlbumScrollStep);

    private void OnAlbumScrollRight(object? sender, RoutedEventArgs e) =>
        ScrollAlbums(AlbumScrollStep);

    private void ScrollAlbums(double delta)
    {
        if (AlbumScroll == null)
            return;

        var max = Math.Max(0, AlbumScroll.Extent.Width - AlbumScroll.Viewport.Width);
        var x = Math.Clamp(AlbumScroll.Offset.X + delta, 0, max);
        AlbumScroll.Offset = new Vector(x, 0);
    }

    private void OnIndexLetterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string letter })
            return;

        e.Handled = true;
        _vm?.JumpToLetterCommand.Execute(letter);
    }

    private void OnLetterScrollRequested(object? sender, string letter)
    {
        if (ArtistList.Items.Count == 0)
            return;

        var targetIndex = FindSectionIndex(letter);
        if (targetIndex < 0)
            return;

        Dispatcher.UIThread.Post(() => ScrollArtistListToSection(targetIndex), DispatcherPriority.Loaded);
    }

    private int FindSectionIndex(string letter)
    {
        for (var i = 0; i < ArtistList.Items.Count; i++)
        {
            if (ArtistList.Items[i] is ArtistSectionHeaderViewModel header
                && string.Equals(header.Letter, letter, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < ArtistList.Items.Count; i++)
        {
            if (ArtistList.Items[i] is not ArtistSectionHeaderViewModel header)
                continue;

            if (ArtistSortHelper.CompareSectionKeys(header.Letter, letter) >= 0)
                return i;
        }

        return -1;
    }

    private void ScrollArtistListToSection(int index)
    {
        ArtistList.UpdateLayout();
        ArtistList.ScrollIntoView(index);

        if (ArtistList.ContainerFromIndex(index) is Control container)
        {
            container.BringIntoView();
            return;
        }

        if (FindScrollViewer(ArtistList) is not ScrollViewer scroll)
            return;

        var offset = EstimateOffsetToIndex(index);
        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        scroll.Offset = new Vector(0, Math.Clamp(offset, 0, max));
    }

    private double EstimateOffsetToIndex(int index)
    {
        const double headerHeight = 44;
        const double artistRowHeight = 72;
        double y = 0;

        for (var i = 0; i < index && i < ArtistList.Items.Count; i++)
        {
            y += ArtistList.Items[i] is ArtistSectionHeaderViewModel
                ? headerHeight
                : artistRowHeight;
        }

        return y;
    }

    private static ScrollViewer? FindScrollViewer(Control root)
    {
        if (root is ScrollViewer sv)
            return sv;

        foreach (var child in root.GetVisualDescendants())
        {
            if (child is ScrollViewer found)
                return found;
        }

        return null;
    }

    public void RequestRemoveTrackConfirm(TrackItemViewModel item)
    {
        if (DataContext is not ArtistsPageViewModel vm)
            return;

        vm.PrepareRemoveTrackConfirm(item);
        RemoveTrackPopconfirm.ShowConfirm();
    }
}
