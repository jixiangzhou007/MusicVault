using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Browse;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class AlbumsPageView : UserControl
{
    private AlbumsPageViewModel? _vm;
    private bool _suppressAlbumListSelection;

    public AlbumsPageView()
    {
        InitializeComponent();
        AlbumList.SelectionChanged += OnAlbumListSelectionChanged;
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

        _vm = DataContext as AlbumsPageViewModel;
        if (_vm == null)
            return;

        _vm.LetterScrollRequested += OnLetterScrollRequested;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        AlbumCatalogTrackListInteraction.Wire(
            AlbumDetailTrackList,
            _vm,
            () => _vm.LoadMoreAlbumDetailTracksCommand.ExecuteAsync(null),
            visible => AlbumDetailScrollTopBtn.IsVisible = visible);
    }

    private void OnAlbumDetailScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(AlbumDetailTrackList);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AlbumsPageViewModel.ShowingTracks)
            && _vm is { ShowingTracks: false })
            ClearAlbumListSelection();
    }

    private void OnAlbumSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm == null)
            return;

        e.Handled = true;
        _vm.ApplyAlbumSearchCommand.Execute(null);
    }

    private void OnAlbumListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressAlbumListSelection || _vm == null || _vm.ShowingTracks)
            return;

        if (e.AddedItems.Count == 0)
            return;

        if (e.AddedItems[0] is ArtistSectionHeaderViewModel)
        {
            ClearAlbumListSelection();
            return;
        }

        if (e.AddedItems[0] is not AlbumListItemViewModel album)
            return;

        _ = _vm.SelectAlbumCommand.ExecuteAsync(album);
    }

    private void OnIndexLetterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string letter })
            return;

        e.Handled = true;
        _vm?.JumpToLetterCommand.Execute(letter);
    }

    private void ClearAlbumListSelection()
    {
        if (AlbumList == null)
            return;

        _suppressAlbumListSelection = true;
        try
        {
            AlbumList.SelectedIndex = -1;
        }
        finally
        {
            _suppressAlbumListSelection = false;
        }
    }

    private void OnLetterScrollRequested(object? sender, string letter)
    {
        if (AlbumList.Items.Count == 0)
            return;

        var targetIndex = FindSectionIndex(letter);
        if (targetIndex < 0)
            return;

        Dispatcher.UIThread.Post(() => ScrollAlbumListToSection(targetIndex), DispatcherPriority.Loaded);
    }

    private int FindSectionIndex(string letter)
    {
        for (var i = 0; i < AlbumList.Items.Count; i++)
        {
            if (AlbumList.Items[i] is ArtistSectionHeaderViewModel header
                && string.Equals(header.Letter, letter, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < AlbumList.Items.Count; i++)
        {
            if (AlbumList.Items[i] is not ArtistSectionHeaderViewModel header)
                continue;

            if (ArtistSortHelper.CompareSectionKeys(header.Letter, letter) >= 0)
                return i;
        }

        return -1;
    }

    private void ScrollAlbumListToSection(int index)
    {
        AlbumList.UpdateLayout();
        AlbumList.ScrollIntoView(index);

        if (AlbumList.ContainerFromIndex(index) is Control container)
        {
            container.BringIntoView();
            return;
        }

        if (FindScrollViewer(AlbumList) is not ScrollViewer scroll)
            return;

        var offset = EstimateOffsetToIndex(index);
        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        scroll.Offset = new Vector(0, Math.Clamp(offset, 0, max));
    }

    private double EstimateOffsetToIndex(int index)
    {
        const double headerHeight = 44;
        const double albumRowHeight = 72;
        double y = 0;

        for (var i = 0; i < index && i < AlbumList.Items.Count; i++)
        {
            y += AlbumList.Items[i] is ArtistSectionHeaderViewModel
                ? headerHeight
                : albumRowHeight;
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
}
