using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicVault.App.Controls;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;

namespace MusicVault.App.Views.Pages;

public partial class SettingsPageView : UserControl
{
    private bool _syncingNavFromVm;
    private SettingsPageViewModel? _vm;
    private PaneSplitLayoutHost? _paneSplit;

    public SettingsPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => BindViewModel();

    private void BindViewModel()
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as SettingsPageViewModel;
        if (_vm == null)
            return;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        PostSyncNavSelection(_vm.SelectedNavItem);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsPageViewModel.SelectedNavItem))
            PostSyncNavSelection(_vm?.SelectedNavItem);
    }

    private void PostSyncNavSelection(SettingsNavItemViewModel? item) =>
        Dispatcher.UIThread.Post(() => SyncNavSelection(item), DispatcherPriority.Loaded);

    private void SyncNavSelection(SettingsNavItemViewModel? item)
    {
        if (ReferenceEquals(SettingsNavList.SelectedItem, item))
            return;

        _syncingNavFromVm = true;
        try
        {
            SettingsNavList.SelectedItem = item;
        }
        finally
        {
            _syncingNavFromVm = false;
        }
    }

    private void OnSettingsNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingNavFromVm || _vm == null)
            return;

        if (SettingsNavList.SelectedItem is not SettingsNavItemViewModel item)
            return;

        if (ReferenceEquals(_vm.SelectedNavItem, item))
            return;

        _vm.SelectedNavItem = item;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachLeftPanel(
            RootGrid,
            NavPaneSplitter,
            PaneSplitPageKeys.SettingsNav,
            defaultLeftPixels: 260);
        _paneSplit.ApplyLayout();
        BindViewModel();
    }
}
