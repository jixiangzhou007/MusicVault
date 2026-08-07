using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views.Pages;
using ModelTrack = MusicVault.Core.Models.Track;

namespace MusicVault.App.Views;

/// <summary>曲目列表共用交互：收藏、加歌单、右键打开元数据 Drawer、滚动加载、双击播放、选中同步。</summary>
public static class TrackListInteraction
{
    private static readonly ConditionalWeakTable<ListBox, ListState> Wired = new();

    public static void Wire(UserControl page, ListBox trackList, TrackListPageOptions? options = null)
    {
        options ??= new TrackListPageOptions();
        if (Wired.TryGetValue(trackList, out var existing))
        {
            existing.Update(page, options);
            return;
        }

        var state = new ListState(page, options);
        Wired.Add(trackList, state);
        state.RegisterHandlers(trackList);
    }

    private static void OpenMetadataEditor(Visual anchor, TrackItemViewModel item)
    {
        var host = FindHostPage(anchor);
        if (host == null)
            return;

        var shell = GetShell(host);
        shell?.EditTrackMetadataCommand.Execute(item);
    }

    private static bool IsWithinRowActionControl(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button or CheckBox)
                return true;
            if (visual is ListBoxItem)
                return false;
            visual = visual.GetVisualParent();
        }

        return false;
    }

    private sealed class ListState
    {
        private UserControl _page;
        private TrackListPageOptions _options;
        private MainWindowViewModel? _shell;

        private long _lastMetadataOpenTicks;
        private long _lastMetadataOpenTrackId;

        public ListState(UserControl page, TrackListPageOptions options)
        {
            _page = page;
            _options = options;
            _page.AttachedToVisualTree += OnPageAttached;
            _page.DetachedFromVisualTree += OnPageDetached;

            if (_options.Tracks != null)
                _options.Tracks.CollectionChanged += OnTracksCollectionChanged;
        }

        public void Update(UserControl page, TrackListPageOptions options)
        {
            if (_options.Tracks != null)
                _options.Tracks.CollectionChanged -= OnTracksCollectionChanged;

            _page = page;
            _options = options;

            if (_options.Tracks != null)
                _options.Tracks.CollectionChanged += OnTracksCollectionChanged;
        }

        public void RegisterHandlers(ListBox list)
        {
            list.ContextFlyout = null;
            list.AddHandler(Button.ClickEvent, (_, e) => OnButtonClick(list, e), RoutingStrategies.Bubble, handledEventsToo: true);
            list.AddHandler(InputElement.PointerPressedEvent, (_, e) => OnPointerPressed(list, e), RoutingStrategies.Bubble, handledEventsToo: true);
            list.AddHandler(Control.ContextRequestedEvent, (_, e) => OnContextRequested(list, e), RoutingStrategies.Bubble, handledEventsToo: true);
            list.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) => OnScrollChanged(list, e), RoutingStrategies.Bubble);
            list.DoubleTapped += OnDoubleTapped;
            list.SelectionChanged += OnSelectionChanged;
        }

        private void OnPageAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_options.SyncNowPlaying)
            {
                _shell = GetShell(_page);
                if (_shell != null)
                {
                    _shell.NowPlayingListSelectionSyncRequested += OnNowPlayingSelectionSyncRequested;
                    RequestNowPlayingSelectionSync();
                }
            }

            if (_options.Tracks != null)
                RefreshTrackListItems();
        }

        private void OnPageDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_shell != null)
            {
                _shell.NowPlayingListSelectionSyncRequested -= OnNowPlayingSelectionSyncRequested;
                _shell = null;
            }
        }

        private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            Dispatcher.UIThread.Post(() =>
            {
                // LoadMore 的 Add 勿整表重绑 ItemsSource，否则虚拟化滚动会跳动。
                // Reset/Replace 仍强制重绑（元数据保存后常复用同一集合引用）。
                if (e.Action is NotifyCollectionChangedAction.Reset
                    or NotifyCollectionChangedAction.Replace)
                    RefreshTrackListItems();
                RequestNowPlayingSelectionSync();
            }, DispatcherPriority.Background);

        private void RefreshTrackListItems()
        {
            if (_options.Tracks == null)
                return;

            var list = FindTrackList(_page);
            if (list == null)
                return;

            var keep = ResolveSelectionAnchor(list);
            var items = _options.Tracks.Items;
            // 元数据保存后常复用同一集合引用，需强制 ListBox 重绑行，否则歌手等列不刷新。
            list.ItemsSource = null;
            list.ItemsSource = items;

            RestoreSelection(list, keep);
        }

        private ModelTrack? ResolveSelectionAnchor(ListBox list)
        {
            if (list.SelectedItem is TrackItemViewModel selected)
                return selected.Track;

            return _shell?.Player.CurrentTrack;
        }

        private static void RestoreSelection(ListBox list, ModelTrack? track)
        {
            if (track == null)
                return;

            var items = list.ItemsSource as IEnumerable ?? list.Items;
            TrackItemViewModel? match = null;
            foreach (var entry in items)
            {
                if (entry is not TrackItemViewModel vm)
                    continue;
                if (!TrackListNowPlayingSync.Matches(vm.Track, track))
                    continue;
                match = vm;
                break;
            }

            if (match == null)
                return;

            if (!ReferenceEquals(list.SelectedItem, match))
                list.SelectedItem = match;
        }

        private void RequestNowPlayingSelectionSync()
        {
            if (!_options.SyncNowPlaying || _shell?.Player.CurrentTrack is not { } track)
                return;

            _ = OnNowPlayingSelectionSyncRequested(
                track,
                _shell.Player.CurrentQueueIndex,
                _shell.Player.IsShuffleOn);
        }

        private async void OnScrollChanged(ListBox list, ScrollChangedEventArgs e)
        {
            if (_options.LoadMoreAsync == null)
                return;

            var viewer = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (viewer == null)
                return;

            if (viewer.Extent.Height - viewer.Offset.Y - viewer.Viewport.Height < 300)
                await _options.LoadMoreAsync().ConfigureAwait(false);
        }

        private async void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not ListBox list)
                return;

            if (list.SelectedItem is not TrackItemViewModel item)
                return;

            var shell = GetShell(_page);
            if (shell != null)
                await shell.PlayTrackCommand.ExecuteAsync(item);
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is Control control)
                TrackListMetadataSync.OnSelectionChanged(control, e);
        }

        private Task OnNowPlayingSelectionSyncRequested(ModelTrack track, int queueIndex, bool isShuffleOn)
        {
            var list = FindTrackList(_page);
            if (list == null)
                return Task.CompletedTask;

            return TrackListNowPlayingSync.ApplyAsync(list, _options.Tracks, track, queueIndex, isShuffleOn);
        }

        public void OnPointerPressed(ListBox list, PointerPressedEventArgs e)
        {
            if (TryHandlePlaylistButtonPointerPressed(list, e))
            {
                e.Handled = true;
                return;
            }

            var point = e.GetCurrentPoint(e.Source as Visual ?? list);
            if (!point.Properties.IsRightButtonPressed)
                return;

            if (IsWithinRowActionControl(e.Source as Visual))
                return;

            var item = FindTrackItem(e.Source as Visual);
            if (item == null)
                return;

            if (_options.ShowOrganizeContextMenu)
            {
                ShowTrackRowContextMenu(list, item, e.Source as Visual ?? list);
                e.Handled = true;
                return;
            }

            TryOpenMetadataEditor(list, item, e.Source as Visual ?? list);
            e.Handled = true;
        }

        public void OnContextRequested(ListBox list, ContextRequestedEventArgs e)
        {
            if (IsWithinRowActionControl(e.Source as Visual))
                return;

            var item = FindTrackItem(e.Source as Visual);
            if (item == null)
                return;

            if (_options.ShowOrganizeContextMenu)
            {
                ShowTrackRowContextMenu(list, item, e.Source as Visual ?? list);
                e.Handled = true;
                return;
            }

            TryOpenMetadataEditor(list, item, e.Source as Visual ?? list);
            e.Handled = true;
        }

        private void ShowTrackRowContextMenu(ListBox list, TrackItemViewModel item, Visual anchor)
        {
            if (!ReferenceEquals(list.SelectedItem, item))
                list.SelectedItem = item;

            var flyout = new MenuFlyout();
            var meta = new MenuItem { Header = App.Localization.Get("Sidebar.Metadata") };
            meta.Click += (_, _) => TryOpenMetadataEditor(list, item, anchor);
            flyout.Items.Add(meta);

            if (_page.DataContext is TracksPageViewModel tracksVm)
            {
                var organize = new MenuItem { Header = App.Localization.Get("Library.Tracks.OrganizeEnqueueTitle") };
                organize.Click += (_, _) => tracksVm.RequestAddTrackToOrganize(item);
                flyout.Items.Add(organize);

                if (TracksPageViewModel.IsNasTrack(item.Track))
                {
                    var download = new MenuItem { Header = tracksVm.DownloadNasToLibraryLabel };
                    download.Click += async (_, _) =>
                        await tracksVm.DownloadNasTrackToLibraryCommand.ExecuteAsync(item).ConfigureAwait(false);
                    flyout.Items.Add(download);
                }
            }

            if (anchor is Control control)
                flyout.ShowAt(control);
        }

        private void TryOpenMetadataEditor(ListBox list, TrackItemViewModel item, Visual anchor)
        {
            if (IsDuplicateMetadataOpen(item.Track.Id))
                return;

            _lastMetadataOpenTicks = Environment.TickCount64;
            _lastMetadataOpenTrackId = item.Track.Id;

            if (!ReferenceEquals(list.SelectedItem, item))
                list.SelectedItem = item;

            OpenMetadataEditor(anchor, item);
        }

        private bool IsDuplicateMetadataOpen(long trackId) =>
            _lastMetadataOpenTrackId == trackId
            && Environment.TickCount64 - _lastMetadataOpenTicks < 400;

        public async void OnButtonClick(ListBox list, RoutedEventArgs e)
        {
            var btn = ResolveTrackRowButton(e.Source);
            if (btn?.Tag is not TrackItemViewModel item)
                return;

            var page = FindHostPage(btn);
            if (page == null)
                return;

            if (btn.Classes.Contains("track-favorite-btn"))
            {
                e.Handled = true;
                try
                {
                    if (page.DataContext is ArtistsPageViewModel artists)
                        await artists.ToggleFavoriteCommand.ExecuteAsync(item).ConfigureAwait(false);
                    else if (page.DataContext is TracksPageViewModel tracks)
                        await tracks.ToggleFavoriteCommand.ExecuteAsync(item).ConfigureAwait(false);
                    else if (page.DataContext is PlaylistsPageViewModel playlists)
                        await playlists.TogglePlaylistTrackFavoriteCommand.ExecuteAsync(item).ConfigureAwait(false);
                }
                catch
                {
                    // ViewModel 已写入 StatusMessage
                }

                return;
            }

            if (btn.Classes.Contains("track-delete-btn"))
            {
                e.Handled = true;
                if (page.DataContext is PlaylistsPageViewModel playlists)
                {
                    await playlists.RemoveTrackFromPlaylistCommand.ExecuteAsync(item).ConfigureAwait(false);
                    return;
                }

                if (page is IRemoveTrackConfirmPage confirmPage)
                    confirmPage.RequestRemoveTrackConfirm(item);
                return;
            }

            if (!btn.Classes.Contains("track-playlist-btn"))
                return;

            e.Handled = true;
            ShowPlaylistFlyout(btn, item, page);
        }

        private static Button? ResolveTrackRowButton(object? source)
        {
            if (source is Button button)
                return button;

            var visual = source as Visual;
            while (visual != null)
            {
                if (visual is Button b)
                    return b;
                visual = visual.GetVisualParent();
            }

            return null;
        }

        private bool TryHandlePlaylistButtonPointerPressed(ListBox list, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(e.Source as Visual ?? list);
            if (!point.Properties.IsLeftButtonPressed)
                return false;

            var btn = ResolveTrackRowButton(e.Source);
            if (btn?.Tag is not TrackItemViewModel item || !btn.Classes.Contains("track-playlist-btn"))
                return false;

            var page = FindHostPage(btn);
            if (page == null)
                return false;

            ShowPlaylistFlyout(btn, item, page);
            return true;
        }

        private static void ShowPlaylistFlyout(Button btn, TrackItemViewModel item, UserControl page)
        {
            var playlists = page.DataContext switch
            {
                ArtistsPageViewModel a => a.PlaylistsForMenu,
                TracksPageViewModel t => t.PlaylistsForMenu,
                _ => null
            };

            if (playlists == null)
                return;

            if (!playlists.Any())
            {
                MessageService.Instance.ShowInfo(App.Localization.Get("Library.Tracks.PlaylistMenuEmpty"));
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
                        if (page.DataContext is ArtistsPageViewModel a2)
                            await a2.AddToPlaylistCommand.ExecuteAsync((item, p.Id)).ConfigureAwait(false);
                        else if (page.DataContext is TracksPageViewModel t2)
                            await t2.AddToPlaylistCommand.ExecuteAsync((item, p.Id)).ConfigureAwait(false);
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

            if (flyout.Items.Count == 0)
                flyout.Items.Add(new MenuItem
                {
                    Header = App.Localization.Get("Playlists.Empty.All"),
                    IsEnabled = false
                });

            FlyoutBase.SetAttachedFlyout(btn, flyout);
            FlyoutBase.ShowAttachedFlyout(btn);
        }
    }

    private static ListBox? FindTrackList(UserControl page) => page switch
    {
        TracksPageView tracks => tracks.TrackList,
        ArtistsPageView artists => artists.ArtistTracks,
        _ => page.GetVisualDescendants().OfType<ListBox>().FirstOrDefault()
    };

    private static TrackItemViewModel? FindTrackItem(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is ListBoxItem { DataContext: TrackItemViewModel track })
                return track;

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private static UserControl? FindHostPage(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is TracksPageView tracks)
                return tracks;
            if (visual is AlbumsPageView albums)
                return albums;
            if (visual is ArtistsPageView artists)
                return artists;
            visual = visual.GetVisualParent();
        }

        return null;
    }

    private static MainWindowViewModel? GetShell(Control page)
    {
        var top = TopLevel.GetTopLevel(page);
        return top == null ? null : (MainWindowViewModel?)top.DataContext;
    }
}
