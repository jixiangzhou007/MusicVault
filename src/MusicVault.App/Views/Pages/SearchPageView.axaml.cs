using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MusicVault.App.Controls;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class SearchPageView : UserControl
{
    private const double TransfersPaneWidth = 280;
    private PaneSplitLayoutHost? _paneSplit;
    private bool _resultsScrollWired;
    private bool _headerAlignmentWired;

    public SearchPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachRightPanel(
            RootGrid,
            TransfersSplitter,
            PaneSplitPageKeys.SearchTransfers,
            TransfersPaneWidth,
            isSplitVisible: () => DataContext is SearchPageViewModel vm && vm.IsTransfersPaneOpen);

        WireResultsListScroll();
        WireDiscoverHeaderAlignment();

        if (DataContext is SearchPageViewModel vm)
        {
            vm.RefreshMusicApiDisplay();
            ApplyTransfersPaneLayout(vm.IsTransfersPaneOpen);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (DataContext is SearchPageViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTransfersPaneLayout(vm.IsTransfersPaneOpen);
        }

        WireResultsListScroll();
        WireDiscoverHeaderAlignment();
    }

    private void WireResultsListScroll()
    {
        if (_resultsScrollWired)
            return;

        OnlineListScrollInteraction.Wire(
            ResultsList,
            () => DataContext is SearchPageViewModel vm
                ? vm.LoadMoreResultsCommand.ExecuteAsync(null)
                : Task.CompletedTask,
            visible => ResultsScrollTopBtn.IsVisible = visible);
        _resultsScrollWired = true;
    }

    private void WireDiscoverHeaderAlignment()
    {
        if (_headerAlignmentWired)
            return;

        TrackTablePairedLayout.Wire(DiscoverHeader, ResultsList);
        _headerAlignmentWired = true;
    }

    private void OnResultsScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(ResultsList);

    private void OnBrowseChartsGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not SearchPageViewModel vm)
            return;

        vm.UpdateBrowseChartsLayout(e.NewSize.Width);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SearchPageViewModel vm
            && e.PropertyName == nameof(SearchPageViewModel.IsTransfersPaneOpen))
            ApplyTransfersPaneLayout(vm.IsTransfersPaneOpen);
    }

    private async void OnSearchFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SearchPageViewModel vm)
            return;

        if (e.Key == Key.Escape)
        {
            vm.ClearSearchCriteriaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !vm.CanSearch)
            return;

        if (vm.SearchCommand.CanExecute(null))
            await vm.SearchCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private async void OnLinkInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SearchPageViewModel vm)
            return;

        if (e.Key == Key.Escape)
        {
            vm.ClearLinkInputCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !vm.CanParse)
            return;

        if (vm.ParseLinkCommand.CanExecute(null))
            await vm.ParseLinkCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private void ApplyTransfersPaneLayout(bool isOpen)
    {
        TransfersPane.IsVisible = isOpen;
        _paneSplit?.ApplyLayout();
    }

    private async void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: OnlineTrackViewModel vm })
            return;

        if (!vm.CanPreviewExecute)
            return;

        await vm.PreviewCommand.ExecuteAsync(null);
    }

    private async void OnAddToPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.DataContext is not OnlineTrackViewModel trackVm
            || DataContext is not SearchPageViewModel pageVm)
            return;

        var playlists = await pageVm.GetAddablePlaylistsAsync().ConfigureAwait(true);
        if (playlists.Count == 0)
        {
            MessageService.Instance.ShowInfo("当前暂无可用歌单，请先在“歌单”页创建歌单。");
            e.Handled = true;
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var playlist in playlists)
        {
            var p = playlist;
            var menuItem = new MenuItem { Header = p.Name };
            menuItem.Click += async (_, _) =>
            {
                try
                {
                    await pageVm.AddOnlineTrackToPlaylistAsync(trackVm, p.Id).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    MessageService.Instance.ShowError(
                        ServiceMessageLocalizer.Format(
                            App.Localization,
                            "Library.Tracks.PlaylistAddFailed",
                            ex.Message));
                }
            };
            flyout.Items.Add(menuItem);
        }

        FlyoutBase.SetAttachedFlyout(button, flyout);
        FlyoutBase.ShowAttachedFlyout(button);
        e.Handled = true;
    }
}
