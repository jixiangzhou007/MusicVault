using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class DownloadsPageView : UserControl
{
    private const double ScrollbarGutterWidth = 14;

    private bool _downloadScrollWired;

    public DownloadsPageView()
    {
        InitializeComponent();
        DownloadListBox.LayoutUpdated += OnDownloadListLayoutUpdated;
        DownloadListBox.SizeChanged += OnDownloadListSizeChanged;
        DownloadListBox.DoubleTapped += OnDownloadItemDoubleTapped;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += (_, _) => WireDownloadListScroll();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        WireDownloadListScroll();

    private void WireDownloadListScroll()
    {
        if (_downloadScrollWired || DataContext is not DownloadsPageViewModel vm)
            return;

        OnlineListScrollInteraction.Wire(
            DownloadListBox,
            () => vm.LoadMoreFilteredItemsCommand.ExecuteAsync(null),
            visible => DownloadScrollTopBtn.IsVisible = visible);
        _downloadScrollWired = true;
    }

    private void OnDownloadScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(DownloadListBox);

    private void OnDownloadListLayoutUpdated(object? sender, EventArgs e) => SyncTransferTableLayout();

    private void OnDownloadListSizeChanged(object? sender, SizeChangedEventArgs e) => SyncTransferTableLayout();

    private void SyncTransferTableLayout()
    {
        var listWidth = DownloadListBox.Bounds.Width;
        if (listWidth <= 1)
            return;

        var gutter = 0.0;
        if (DownloadListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scrollViewer
            && scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1)
        {
            gutter = ScrollbarGutterWidth;
        }

        var rightInset = 12 + gutter;
        TransferTableHeader.HeaderGridControl.Margin = new Thickness(16, 0, rightInset, 3);
        TransferTableHeader.HeaderDividerControl.Margin = new Thickness(16, 0, rightInset, 0);

        foreach (var item in DownloadListBox.GetVisualChildren().OfType<ListBoxItem>())
        {
            item.Width = listWidth;
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        }
    }

    private void OnFilterSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DownloadsPageViewModel vm)
            return;

        if (e.Key == Key.Escape)
        {
            vm.ClearSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        LocalSearchInput.OnEnterKeyDown(sender, e, vm.ApplyFilterSearchCommand);
    }

    private async void OnDownloadItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not DownloadsPageViewModel vm)
            return;

        if (DownloadListBox.SelectedItem is DownloadItemViewModel item && item.CanPlay)
            await vm.PlayItemCommand.ExecuteAsync(item);
    }

    private void OnCancelActiveMenuClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CancelActivePopconfirm.ShowConfirm();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        DownloadListBox.LayoutUpdated -= OnDownloadListLayoutUpdated;
        DownloadListBox.SizeChanged -= OnDownloadListSizeChanged;
        base.OnDetachedFromVisualTree(e);
    }
}
