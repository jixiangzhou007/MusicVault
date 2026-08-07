using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.ViewModels.Sidebar;

namespace MusicVault.App.Views.Sidebar;

public partial class RightSidebarView : UserControl
{
    private const double ScrollbarGutterWidth = 14;

    public RightSidebarView()
    {
        InitializeComponent();
        QueueListBox.AddHandler(Button.ClickEvent, OnQueueButtonClick, RoutingStrategies.Bubble, handledEventsToo: true);
        QueueListBox.LayoutUpdated += (_, _) => SyncQueueTableLayout();
        QueueListBox.SizeChanged += (_, _) => SyncQueueTableLayout();
        DataContextChanged += (_, _) => SyncQueueTableLayout();
    }

    private void SyncQueueTableLayout()
    {
        var listWidth = QueueListBox.Bounds.Width;
        if (listWidth <= 1 || QueueTableHeader == null)
            return;

        var gutter = 0.0;
        if (QueueListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scrollViewer
            && scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1)
        {
            gutter = ScrollbarGutterWidth;
        }

        var rightInset = 12 + gutter;
        QueueTableHeader.HeaderDividerControl.Margin = new Thickness(12, 0, rightInset, 0);
        QueueTableHeader.ActiveHeaderGrid.Margin = new Thickness(12, 0, rightInset, 3);

        foreach (var item in QueueListBox.GetVisualChildren().OfType<ListBoxItem>())
        {
            item.Width = listWidth;
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        }
    }

    private async void OnQueueDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not RightSidebarViewModel vm)
            return;
        if (vm.IsQueueBatchMode)
            return;
        if (sender is ListBox { SelectedItem: QueueRowViewModel row })
            await vm.PlayQueueItemCommand.ExecuteAsync(row);
    }

    private async void OnQueueButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RightSidebarViewModel vm)
            return;
        if (e.Source is not Button { Tag: QueueRowViewModel row } btn)
            return;
        if (!btn.Classes.Contains("track-favorite-btn"))
            return;

        e.Handled = true;
        await vm.ToggleFavoriteCommand.ExecuteAsync(row);
    }
}
