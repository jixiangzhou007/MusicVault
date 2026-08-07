using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MusicVault.App.Controls;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class HomePageView : UserControl
{
    private const double HomeLyricsBaseFontSize = 12;
    private PaneSplitLayoutHost? _paneSplit;
    private HomePageViewModel? _homeVm;
    private bool _historyScrollWired;

    public HomePageView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += OnDataContextChanged;
        TrackListBox.AddHandler(Button.ClickEvent, OnTrackListButtonClick, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        BindHomeViewModel();
        WireHistoryListScroll();
    }

    private void BindHomeViewModel()
    {
        if (_homeVm != null)
            _homeVm.PropertyChanged -= OnHomeViewModelPropertyChanged;

        _homeVm = DataContext as HomePageViewModel;
        if (_homeVm != null)
            _homeVm.PropertyChanged += OnHomeViewModelPropertyChanged;
    }

    private void OnHomeViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomePageViewModel.ShowNowPlayingPane)
            or nameof(HomePageViewModel.ShowMainHomeContent))
            _paneSplit?.ApplyLayout();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _paneSplit ??= PaneSplitLayoutHost.AttachLeftPanel(
            PlayingSplitGrid,
            PlayingPaneSplitter,
            PaneSplitPageKeys.HomeNowPlaying,
            defaultLeftPixels: ReadAppResourceDouble("NowPlayingLeftPaneDefaultWidth", 360),
            defaultLeftRatio: ReadAppResourceDouble("NowPlayingLeftPaneWeight", 0.38),
            isSplitVisible: () => DataContext is HomePageViewModel vm && vm.ShowMainHomeContent);
        _paneSplit.ApplyLayout();
        BindHomeViewModel();
        WireHistoryListScroll();
    }

    private static double ReadAppResourceDouble(string key, double fallback)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true && value is double d)
            return d;

        return fallback;
    }

    private void WireHistoryListScroll()
    {
        if (_historyScrollWired || DataContext is not HomePageViewModel vm)
            return;

        OnlineListScrollInteraction.Wire(
            TrackListBox,
            () =>
            {
                if (vm.IsRecentTabActive)
                    return vm.LoadMoreHistoryRowsCommand.ExecuteAsync(null);
                return Task.CompletedTask;
            },
            visible => HistoryScrollTopBtn.IsVisible = visible);
        _historyScrollWired = true;
    }

    private void OnHistoryScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(TrackListBox);

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HomeLyricsPanel.BaseFontSize = HomeLyricsBaseFontSize;
        HomeLyricsPanel.ActiveFontDelta = 1;
        HomeLyricsPanel.LineMarginVertical = 3;
        HomeLyricsPanel.SizeChanged += OnLyricsPanelSizeChanged;
        if (DataContext is HomePageViewModel)
            Dispatcher.UIThread.Post(() => HomeLyricsPanel.RefreshScrollPosition(), DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HomeLyricsPanel.SizeChanged -= OnLyricsPanelSizeChanged;
        if (_homeVm != null)
        {
            _homeVm.PropertyChanged -= OnHomeViewModelPropertyChanged;
            _homeVm = null;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is HomePageViewModel)
            Dispatcher.UIThread.Post(() => HomeLyricsPanel.RefreshScrollPosition(), DispatcherPriority.Loaded);
    }

    private void OnLyricsPanelSizeChanged(object? sender, SizeChangedEventArgs e) =>
        HomeLyricsPanel.RefreshScrollPosition();

    private async void OnTrackListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not HomePageViewModel home)
            return;
        if (sender is not ListBox { SelectedItem: QueueRowViewModel row })
            return;

        switch (home.ActiveListTab)
        {
            case HomeListTab.Frequent:
                await home.PlayFrequentItemCommand.ExecuteAsync(row);
                break;
            case HomeListTab.Recommend:
                await home.PlayRecommendItemCommand.ExecuteAsync(row);
                break;
            default:
                await home.PlayQueueItemCommand.ExecuteAsync(row);
                break;
        }
    }

    private async void OnTrackListButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel home)
            return;
        if (e.Source is not Button { Tag: QueueRowViewModel row } btn)
            return;
        if (!btn.Classes.Contains("track-favorite-btn"))
            return;

        e.Handled = true;
        await home.ToggleFavoriteCommand.ExecuteAsync(row);
    }
}
