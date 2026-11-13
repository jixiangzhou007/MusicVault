using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MusicVault.App.Controls;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class TracksPageView : UserControl, IRemoveTrackConfirmPage
{
    private const double LibraryPaneWidth = 260;
    private bool _syncingLibraryFromVm;
    private bool _trackScrollTopWired;
    private bool _trackHeaderAlignmentWired;
    private TracksPageViewModel? _tracksVm;
    private PaneSplitLayoutHost? _paneSplit;

    public TracksPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttached;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => BindViewModel();

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachLeftPanel(
            RootGrid,
            LibrarySplitter,
            PaneSplitPageKeys.TracksLibrary,
            LibraryPaneWidth,
            isSplitVisible: () => _tracksVm?.IsLibraryPaneOpen ?? true);
        BindViewModel();
    }

    private void BindViewModel()
    {
        if (_tracksVm != null)
            _tracksVm.PropertyChanged -= OnViewModelPropertyChanged;

        _tracksVm = DataContext as TracksPageViewModel;
        if (_tracksVm == null)
            return;

        _tracksVm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLibraryPaneLayout(_tracksVm.IsLibraryPaneOpen);
        PostSyncLibraryListSelection(_tracksVm.SelectedLibraryItem);
        WireTrackList(_tracksVm);
        WireTrackScrollTop();
        WireTrackTableHeaderAlignment();
    }

    private void WireTrackList(TracksPageViewModel vm)
    {
        TrackListInteraction.Wire(this, TrackList, new TrackListPageOptions
        {
            Tracks = vm.Tracks,
            LoadMoreAsync = () => vm.LoadMoreTracksCommand.ExecuteAsync(null),
            ShowOrganizeContextMenu = true
        });
        TrackListInteraction.Wire(this, FolderTrackList, new TrackListPageOptions
        {
            ShowOrganizeContextMenu = true,
            SyncNowPlaying = false
        });
    }

    private void WireTrackTableHeaderAlignment()
    {
        if (_trackHeaderAlignmentWired)
            return;

        TrackTablePairedLayout.Wire(SongsLibraryHeader, TrackList, rightEdgeCompanion: TracksToolbar);
        if (FolderTracksHeader != null && FolderTrackList != null)
            TrackTablePairedLayout.Wire(FolderTracksHeader, FolderTrackList);
        _trackHeaderAlignmentWired = true;
    }

    private void OnFolderListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not TracksPageViewModel vm)
            return;

        var item = ResolveFolderItem(e.Source) ?? (sender as ListBox)?.SelectedItem as FolderBrowseItemViewModel;
        if (item == null)
            return;

        e.Handled = true;
        vm.EnterFolderCommand.Execute(item);
    }

    private void OnFolderListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not TracksPageViewModel vm)
            return;
        if ((sender as ListBox)?.SelectedItem is not FolderBrowseItemViewModel item)
            return;

        e.Handled = true;
        vm.EnterFolderCommand.Execute(item);
    }

    private static FolderBrowseItemViewModel? ResolveFolderItem(object? source)
    {
        for (var current = source as StyledElement; current != null; current = current.Parent)
        {
            if (current.DataContext is FolderBrowseItemViewModel item)
                return item;
        }

        return null;
    }

    private void WireTrackScrollTop()
    {
        if (_trackScrollTopWired)
            return;

        OnlineListScrollInteraction.Wire(
            TrackList,
            loadMoreAsync: null,
            onBackToTopVisibilityChanged: visible => TrackScrollTopBtn.IsVisible = visible);
        _trackScrollTopWired = true;
    }

    private void OnTrackScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(TrackList);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TracksPageViewModel vm)
            return;

        switch (e.PropertyName)
        {
            case nameof(TracksPageViewModel.IsLibraryPaneOpen):
                ApplyLibraryPaneLayout(vm.IsLibraryPaneOpen);
                break;
            case nameof(TracksPageViewModel.SelectedLibraryItem):
                PostSyncLibraryListSelection(vm.SelectedLibraryItem);
                break;
            case nameof(TracksPageViewModel.Tracks):
                WireTrackList(vm);
                break;
        }
    }

    private void PostSyncLibraryListSelection(LibraryListItemViewModel? item) =>
        Dispatcher.UIThread.Post(() => SyncLibraryListSelection(item), DispatcherPriority.Loaded);

    private void SyncLibraryListSelection(LibraryListItemViewModel? item)
    {
        if (ReferenceEquals(LibraryList.SelectedItem, item))
            return;

        _syncingLibraryFromVm = true;
        try { LibraryList.SelectedItem = item; }
        finally { _syncingLibraryFromVm = false; }
    }

    private void OnLibrarySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingLibraryFromVm || DataContext is not TracksPageViewModel vm)
            return;

        if (LibraryList.SelectedItem is not LibraryListItemViewModel item)
            return;

        Dispatcher.UIThread.Post(
            () => vm.SelectLibraryFromView(item),
            DispatcherPriority.Background);
    }

    private void ApplyLibraryPaneLayout(bool isOpen)
    {
        LibraryPane.IsVisible = isOpen;
        _paneSplit?.ApplyLayout();
    }

    public void FocusTrackSearch()
    {
        TrackSearchBox?.Focus();
        TrackSearchBox?.SelectAll();
    }

    private void OnTrackSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TracksPageViewModel vm)
            return;

        LocalSearchInput.OnEnterKeyDown(sender, e, vm.ApplyTrackSearchCommand);
    }

    private void OnDeleteLibraryMenuClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DeleteLibraryPopconfirm.ShowConfirm();
    }

    public void RequestRemoveTrackConfirm(TrackItemViewModel item)
    {
        if (DataContext is not TracksPageViewModel vm)
            return;

        vm.PrepareRemoveTrackConfirm(item);
        RemoveTrackPopconfirm.ShowConfirm();
    }
}
