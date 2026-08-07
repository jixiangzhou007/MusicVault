using System;
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

public partial class PlaylistsPageView : UserControl
{
    private PaneSplitLayoutHost? _paneSplit;
    private PlaylistsPageViewModel? _vm;
    private bool _chartListScrollWired;
    private bool _listenHistoryListScrollWired;
    private bool _playlistTracksScrollWired;
    private bool _chartHeaderAlignmentWired;
    private bool _listenHistoryHeaderAlignmentWired;

    public PlaylistsPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += OnDataContextChanged;
        PlaylistBrowseScroll.SizeChanged += OnPlaylistBrowseScrollLayoutChanged;
        PlaylistBrowseScroll.ScrollChanged += OnPlaylistBrowseScrollLayoutChanged;
    }

    private void OnPlaylistBrowseScrollLayoutChanged(object? sender, EventArgs e) =>
        SyncChartsGridWidth();

    private void SyncChartsGridWidth()
    {
        if (ChartsGrid == null)
            return;

        var viewport = PlaylistBrowseScroll.Viewport.Width;
        if (viewport <= 0)
            return;

        var target = Math.Max(0, viewport - ChartsGrid.Margin.Left - ChartsGrid.Margin.Right);
        if (Math.Abs(ChartsGrid.Width - target) > 0.5)
            ChartsGrid.Width = target;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        OnDataContextChangedScroll();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachLeftPanel(
            RootGrid,
            PlaylistSplitter,
            PaneSplitPageKeys.PlaylistsNav,
            defaultLeftPixels: 260);
        _paneSplit.ApplyLayout();
        SyncChartsGridWidth();
        WireChartListScroll();
        WireListenHistoryListScroll();
        WirePlaylistTrackListScroll();
        WireTrackTableHeaderAlignment();
    }

    private void OnDataContextChangedScroll()
    {
        _vm = DataContext as PlaylistsPageViewModel;
        SyncChartsGridWidth();
        WireChartListScroll();
        WireListenHistoryListScroll();
        WirePlaylistTrackListScroll();
        WireTrackTableHeaderAlignment();
    }

    private void WireTrackTableHeaderAlignment()
    {
        if (!_chartHeaderAlignmentWired)
        {
            TrackTablePairedLayout.Wire(ChartDiscoverHeader, ChartTrackList);
            _chartHeaderAlignmentWired = true;
        }

        if (!_listenHistoryHeaderAlignmentWired)
        {
            TrackTablePairedLayout.Wire(ListenHistoryHeaderGrid, ListenHistoryTrackList);
            _listenHistoryHeaderAlignmentWired = true;
        }
    }

    private void WirePlaylistTrackListScroll()
    {
        if (_vm == null)
            return;

        TrackListInteraction.Wire(this, PlaylistTrackList, new TrackListPageOptions
        {
            LoadMoreAsync = () => _vm.LoadMorePlaylistTracksCommand.ExecuteAsync(null)
        });

        if (_playlistTracksScrollWired)
            return;

        OnlineListScrollInteraction.Wire(
            PlaylistTrackList,
            () => _vm.LoadMorePlaylistTracksCommand.ExecuteAsync(null));
        _playlistTracksScrollWired = true;
    }

    private void WireListenHistoryListScroll()
    {
        if (_listenHistoryListScrollWired)
            return;

        OnlineListScrollInteraction.Wire(
            ListenHistoryTrackList,
            () => _vm == null
                ? Task.CompletedTask
                : _vm.LoadMoreOnlineDetailTracksCommand.ExecuteAsync(null));
        _listenHistoryListScrollWired = true;
    }

    private void WireChartListScroll()
    {
        if (_chartListScrollWired)
            return;

        OnlineListScrollInteraction.Wire(
            ChartTrackList,
            () => _vm == null
                ? Task.CompletedTask
                : _vm.LoadMoreOnlineDetailTracksCommand.ExecuteAsync(null),
            visible => ChartScrollTopBtn.IsVisible = visible);
        _chartListScrollWired = true;
    }

    private void OnChartScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(ChartTrackList);

    private async void OnChartTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: OnlineTrackViewModel vm })
            return;

        if (!vm.CanPreviewExecute)
            return;

        await vm.PreviewCommand.ExecuteAsync(null);
    }

    private async void OnPlaylistBrowseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null || _vm.ShowingDetail)
            return;

        if (sender is not ListBox { SelectedItem: PlaylistBrowseCardViewModel card })
            return;

        await _vm.SelectBrowseCardCommand.ExecuteAsync(card);
    }

    private async void OnAddOnlineTrackToPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (_vm == null
            || sender is not Button button
            || button.DataContext is not OnlineTrackViewModel trackVm)
            return;

        var playlists = await _vm.GetAddablePlaylistsAsync().ConfigureAwait(true);
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
                    await _vm.AddOnlineTrackToPlaylistAsync(trackVm, p.Id).ConfigureAwait(true);
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

    private static void OnPlaylistRowActionPointerPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;
}
