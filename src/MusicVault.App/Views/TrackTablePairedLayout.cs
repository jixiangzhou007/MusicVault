using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Controls;

namespace MusicVault.App.Views;

/// <summary>
/// paired 表格：表头（及可选顶栏）与 ListBox 数据行左右 inset 对齐。
/// 右缘仅按实测滚动条占位追加；Fluent 叠加滚动条测得 0 时不预留，避免表头/顶栏比行更靠左。
/// 外层已有 PageHeroToolbarInsetMargin 时传入 contentInsetOverride: 0，只同步滚动条 gutter。
/// </summary>
public static class TrackTablePairedLayout
{
    private const double FallbackContentInset = 16;
    private const double FallbackHeaderBottomInset = 2;

    public static void Wire(Grid headerGrid, ListBox listBox, double? contentInsetOverride = null, Control? rightEdgeCompanion = null) =>
        WireInternal(headerGrid, listBox, contentInsetOverride, rightEdgeCompanion);

    public static void Wire(DiscoverOnlineTrackTableHeader header, ListBox listBox, double? contentInsetOverride = null, Control? rightEdgeCompanion = null) =>
        WireInternal(header.HeaderGrid, listBox, contentInsetOverride, rightEdgeCompanion);

    public static void Wire(LibraryTrackTableHeader header, ListBox listBox, double? contentInsetOverride = null, Control? rightEdgeCompanion = null) =>
        WireInternal(header.PairingHeaderGrid, listBox, contentInsetOverride, rightEdgeCompanion);

    private static void WireInternal(Grid? headerGrid, ListBox? listBox, double? contentInsetOverride, Control? rightEdgeCompanion)
    {
        if (headerGrid == null || listBox == null)
            return;

        void Sync()
        {
            if (headerGrid == null || listBox == null)
                return;

            var contentInset = contentInsetOverride
                ?? ReadResourceDouble("TrackTablePairedContentInset", FallbackContentInset);
            var headerBottom = ReadResourceDouble("TrackTablePairedHeaderBottomInset", FallbackHeaderBottomInset);
            var scrollBarGutter = ResolveScrollBarGutter(listBox);
            var right = contentInset + scrollBarGutter;
            // 行 Margin 仅为内容 inset；仅当滚动条真实占宽时表头再追加，与行右缘对齐
            headerGrid.Margin = new Thickness(contentInset, 0, right, headerBottom);
            if (rightEdgeCompanion != null)
            {
                var m = rightEdgeCompanion.Margin;
                rightEdgeCompanion.Margin = new Thickness(m.Left, m.Top, right, m.Bottom);
            }
        }

        listBox.AttachedToVisualTree += (_, _) => Sync();
        listBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty || e.Property == Visual.IsVisibleProperty)
                Dispatcher.UIThread.Post(Sync, DispatcherPriority.Background);
        };

        if (FindScrollViewer(listBox) is { } scrollViewer)
            scrollViewer.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.BoundsProperty || e.Property == ScrollViewer.ViewportProperty)
                    Dispatcher.UIThread.Post(Sync, DispatcherPriority.Background);
            };

        Sync();
        // 首次布局 / 切换列表可见后再同步一次，避免收藏等后显列表用到过期 gutter
        Dispatcher.UIThread.Post(Sync, DispatcherPriority.Loaded);
    }

    private static double ResolveScrollBarGutter(ListBox listBox)
    {
        if (FindScrollViewer(listBox) is not { } scrollViewer)
            return 0;

        // 只认布局占位；Visible + 叠加条时 Width≈Viewport，预留 17 会把表头/顶栏推左（与行错位）
        var measured = scrollViewer.Bounds.Width - scrollViewer.Viewport.Width;
        return measured > 0.5 ? measured : 0;
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
