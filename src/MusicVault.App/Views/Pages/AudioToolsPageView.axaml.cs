using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views;

namespace MusicVault.App.Views.Pages;

public partial class AudioToolsPageView : UserControl, IRemoveTrackConfirmPage
{
    private bool _workSetScrollWired;
    private bool _batchScrapeHeaderAlignmentWired;

    public AudioToolsPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += (_, _) => WireWorkSetListScroll();
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        BatchScrapeListInteraction.Wire(this, BatchScrapeItems);
        WireWorkSetListScroll();
        WireBatchScrapeHeaderAlignment();
    }

    private void WireBatchScrapeHeaderAlignment()
    {
        if (_batchScrapeHeaderAlignmentWired)
            return;

        // 外层已有 PageHeroToolbarInsetMargin(16/16)；contentInset 再取 16 会双层缩进，表头阴影与列错位
        TrackTablePairedLayout.Wire(BatchScrapeHeaderGrid, BatchScrapeItems, contentInsetOverride: 0);
        _batchScrapeHeaderAlignmentWired = true;
    }

    private void WireWorkSetListScroll()
    {
        if (_workSetScrollWired || DataContext is not AudioToolsPageViewModel vm)
            return;

        OnlineListScrollInteraction.Wire(
            BatchScrapeItems,
            () => vm.LoadMoreWorkSetItemsCommand.ExecuteAsync(null),
            visible => WorkSetScrollTopBtn.IsVisible = visible);
        _workSetScrollWired = true;
    }

    private void OnWorkSetScrollTopClick(object? sender, RoutedEventArgs e) =>
        OnlineListScrollInteraction.ScrollToTop(BatchScrapeItems);

    private void OnOrganizeFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is AudioToolsPageViewModel vm)
            LocalSearchInput.OnEnterKeyDown(sender, e, vm.LoadWorkingSetCommand);
    }

    public void RequestRemoveTrackConfirm(TrackItemViewModel item)
    {
        if (DataContext is not AudioToolsPageViewModel vm)
            return;

        vm.PrepareRemoveTrackConfirm(item);
        RemoveTrackPopconfirm.ShowConfirm();
    }
}
