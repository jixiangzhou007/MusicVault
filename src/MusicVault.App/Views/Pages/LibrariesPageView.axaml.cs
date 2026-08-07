using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using MusicVault.App.Controls;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;

namespace MusicVault.App.Views.Pages;

public partial class LibrariesPageView : UserControl
{
    private bool _syncingLibraryFromVm;
    private LibrariesPageViewModel? _vm;
    private PaneSplitLayoutHost? _paneSplit;

    public LibrariesPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => BindViewModel();

    private void BindViewModel()
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as LibrariesPageViewModel;
        if (_vm == null)
            return;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        PostSyncLibraryListSelection(_vm.SelectedLibraryItem);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibrariesPageViewModel.SelectedLibraryItem))
            PostSyncLibraryListSelection(_vm?.SelectedLibraryItem);
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
        if (_syncingLibraryFromVm || _vm == null)
            return;

        if (LibraryList.SelectedItem is not LibraryListItemViewModel item)
            return;

        if (ReferenceEquals(_vm.SelectedLibraryItem, item))
            return;

        _vm.SelectedLibraryItem = item;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachLeftPanel(
            RootGrid,
            PaneSplitter,
            PaneSplitPageKeys.LibrariesSources,
            defaultLeftPixels: 260);
        _paneSplit.ApplyLayout();
        BindViewModel();
    }
}
