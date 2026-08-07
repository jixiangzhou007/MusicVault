using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Controls;

namespace MusicVault.App.Views;

/// <summary>paired 表格：表头与 ListBox 数据行共用左 16 / 右 16+滚动条占位 inset。</summary>
public static class TrackTablePairedLayout
{
    private const double FallbackContentInset = 16;
    private const double FallbackScrollBarGutter = 17;
    private const double FallbackHeaderBottomInset = 2;

    public static void Wire(Grid headerGrid, ListBox listBox) =>
        WireInternal(headerGrid, listBox);

    public static void Wire(DiscoverOnlineTrackTableHeader header, ListBox listBox) =>
        WireInternal(header.HeaderGrid, listBox);

    private static void WireInternal(Grid? headerGrid, ListBox? listBox)
    {
        if (headerGrid == null || listBox == null)
            return;

        void Sync()
        {
            if (headerGrid == null || listBox == null)
                return;

            var contentInset = ReadResourceDouble("TrackTablePairedContentInset", FallbackContentInset);
            var headerBottom = ReadResourceDouble("TrackTablePairedHeaderBottomInset", FallbackHeaderBottomInset);
            var scrollBarGutter = ResolveScrollBarGutter(listBox);
            headerGrid.Margin = new Thickness(contentInset, 0, contentInset + scrollBarGutter, headerBottom);
        }

        listBox.AttachedToVisualTree += (_, _) => Sync();
        listBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
                Dispatcher.UIThread.Post(Sync, DispatcherPriority.Background);
        };

        if (FindScrollViewer(listBox) is { } scrollViewer)
            scrollViewer.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.BoundsProperty || e.Property == ScrollViewer.ViewportProperty)
                    Dispatcher.UIThread.Post(Sync, DispatcherPriority.Background);
            };

        Sync();
    }

    private static double ResolveScrollBarGutter(ListBox listBox)
    {
        var fallback = ReadResourceDouble("TrackTableScrollBarGutter", FallbackScrollBarGutter);
        if (FindScrollViewer(listBox) is not { } scrollViewer)
            return fallback;

        var measured = scrollViewer.Bounds.Width - scrollViewer.Viewport.Width;
        return measured > 0.5 ? measured : fallback;
    }

    private static double ReadResourceDouble(string key, double fallback)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true && value is double d)
            return d;

        return fallback;
    }

    private static ScrollViewer? FindScrollViewer(Control root) =>
        root.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
}
