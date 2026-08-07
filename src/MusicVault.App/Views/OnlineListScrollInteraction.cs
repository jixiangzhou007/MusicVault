using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MusicVault.App.Views;

/// <summary>在线曲目 ListBox：接近底部加载更多、滚动显示回到顶部、Home/End 跳转。</summary>
public static class OnlineListScrollInteraction
{
    private const double LoadMoreThresholdPx = 300;
    private const double BackToTopShowOffsetPx = 480;

    private static readonly ConditionalWeakTable<ListBox, ScrollState> Wired = new();

    public static void Wire(
        ListBox list,
        Func<Task>? loadMoreAsync = null,
        Action<bool>? onBackToTopVisibilityChanged = null)
    {
        if (Wired.TryGetValue(list, out var existing))
        {
            existing.Update(loadMoreAsync, onBackToTopVisibilityChanged);
            return;
        }

        var state = new ScrollState(loadMoreAsync, onBackToTopVisibilityChanged);
        Wired.Add(list, state);
        state.Register(list);
    }

    public static void ScrollToTop(ListBox list)
    {
        var viewer = FindScrollViewer(list);
        if (viewer != null)
            viewer.Offset = new Vector(viewer.Offset.X, 0);
    }

    private static ScrollViewer? FindScrollViewer(Control root) =>
        root.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    private sealed class ScrollState
    {
        private Func<Task>? _loadMoreAsync;
        private Action<bool>? _onBackToTopVisibilityChanged;
        private bool _loadMoreInFlight;
        private bool _backToTopVisible;

        public ScrollState(Func<Task>? loadMoreAsync, Action<bool>? onBackToTopVisibilityChanged)
        {
            _loadMoreAsync = loadMoreAsync;
            _onBackToTopVisibilityChanged = onBackToTopVisibilityChanged;
        }

        public void Update(Func<Task>? loadMoreAsync, Action<bool>? onBackToTopVisibilityChanged)
        {
            _loadMoreAsync = loadMoreAsync;
            _onBackToTopVisibilityChanged = onBackToTopVisibilityChanged;
        }

        public void Register(ListBox list)
        {
            list.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) => OnScrollChanged(list, e), RoutingStrategies.Bubble);
            list.KeyDown += (_, e) => OnKeyDown(list, e);
        }

        private async void OnScrollChanged(ListBox list, ScrollChangedEventArgs e)
        {
            var viewer = FindScrollViewer(list);
            if (viewer == null)
                return;

            UpdateBackToTopVisibility(viewer.Offset.Y);

            if (_loadMoreAsync == null || _loadMoreInFlight)
                return;

            var remaining = viewer.Extent.Height - viewer.Offset.Y - viewer.Viewport.Height;
            if (remaining >= LoadMoreThresholdPx)
                return;

            _loadMoreInFlight = true;
            try
            {
                await _loadMoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _loadMoreInFlight = false;
            }
        }

        private void UpdateBackToTopVisibility(double offsetY)
        {
            var visible = offsetY > BackToTopShowOffsetPx;
            if (visible == _backToTopVisible)
                return;

            _backToTopVisible = visible;
            Dispatcher.UIThread.Post(() => _onBackToTopVisibilityChanged?.Invoke(visible));
        }

        private static void OnKeyDown(ListBox list, KeyEventArgs e)
        {
            var viewer = FindScrollViewer(list);
            if (viewer == null)
                return;

            if (e.Key == Key.Home)
            {
                viewer.Offset = new Vector(viewer.Offset.X, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                var max = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
                viewer.Offset = new Vector(viewer.Offset.X, max);
                e.Handled = true;
            }
        }
    }
}
