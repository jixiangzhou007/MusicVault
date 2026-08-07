using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views.Pages;

namespace MusicVault.App.Views;

/// <summary>批量刮削队列列表交互：右键打开元数据 Drawer、删除确认。</summary>
public static class BatchScrapeListInteraction
{
    private static readonly ConditionalWeakTable<ItemsControl, ListState> Wired = new();

    public static void Wire(UserControl page, ItemsControl list)
    {
        if (Wired.TryGetValue(list, out var existing))
        {
            existing.Update(page);
            return;
        }

        var state = new ListState(page);
        Wired.Add(list, state);
        state.RegisterHandlers(list);
    }

    private static void OpenMetadataEditor(Visual anchor, TrackItemViewModel item)
    {
        var shell = GetShell(anchor);
        shell?.EditTrackMetadataCommand.Execute(item);
    }

    private static bool IsWithinRowActionControl(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button or CheckBox)
                return true;
            if (visual is ContentPresenter)
                return false;
            visual = visual.GetVisualParent();
        }

        return false;
    }

    private sealed class ListState
    {
        private UserControl _page;
        private long _lastMetadataOpenTicks;
        private long _lastMetadataOpenTrackId;

        public ListState(UserControl page) => _page = page;

        public void Update(UserControl page) => _page = page;

        public void RegisterHandlers(ItemsControl list)
        {
            list.ContextFlyout = null;
            list.AddHandler(Button.ClickEvent, (_, e) => OnButtonClick(e), RoutingStrategies.Bubble, handledEventsToo: true);
            list.AddHandler(InputElement.PointerPressedEvent, (_, e) => OnPointerPressed(e), RoutingStrategies.Bubble, handledEventsToo: true);
            list.AddHandler(Control.ContextRequestedEvent, (_, e) => OnContextRequested(e), RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void OnPointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(e.Source as Visual ?? _page);
            if (!point.Properties.IsRightButtonPressed)
                return;

            if (IsWithinRowActionControl(e.Source as Visual))
                return;

            var item = FindQueueItem(e.Source as Visual);
            if (item == null)
                return;

            TryOpenMetadataEditor(item, e.Source as Visual ?? _page);
            e.Handled = true;
        }

        private void OnContextRequested(ContextRequestedEventArgs e)
        {
            if (IsWithinRowActionControl(e.Source as Visual))
                return;

            var item = FindQueueItem(e.Source as Visual);
            if (item == null)
                return;

            TryOpenMetadataEditor(item, e.Source as Visual ?? _page);
            e.Handled = true;
        }

        private void TryOpenMetadataEditor(BatchScrapeQueueItemViewModel item, Visual anchor)
        {
            if (IsDuplicateMetadataOpen(item.Track.Id))
                return;

            _lastMetadataOpenTicks = Environment.TickCount64;
            _lastMetadataOpenTrackId = item.Track.Id;
            OpenMetadataEditor(anchor, new TrackItemViewModel(item.Track));
        }

        private bool IsDuplicateMetadataOpen(long trackId) =>
            _lastMetadataOpenTrackId == trackId
            && Environment.TickCount64 - _lastMetadataOpenTicks < 400;

        private void OnButtonClick(RoutedEventArgs e)
        {
            if (e.Source is not Button { Tag: BatchScrapeQueueItemViewModel queueItem } btn)
                return;

            if (!btn.Classes.Contains("track-delete-btn"))
                return;

            e.Handled = true;
            if (_page is IRemoveTrackConfirmPage confirmPage)
                confirmPage.RequestRemoveTrackConfirm(new TrackItemViewModel(queueItem.Track));
        }
    }

    private static BatchScrapeQueueItemViewModel? FindQueueItem(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Control { DataContext: BatchScrapeQueueItemViewModel item })
                return item;

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private static MainWindowViewModel? GetShell(Visual visual)
    {
        var top = TopLevel.GetTopLevel(visual);
        return top == null ? null : (MainWindowViewModel?)top.DataContext;
    }
}
