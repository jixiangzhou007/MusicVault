using Avalonia.Controls;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.App.Views;

/// <summary>曲目列表选中变化时，同步右侧元数据编辑面板。</summary>
internal static class TrackListMetadataSync
{
    public static void OnSelectionChanged(Control view, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not TrackItemViewModel item) return;

        var shell = (MainWindowViewModel?)((MainWindow?)TopLevel.GetTopLevel(view))?.DataContext;
        if (shell == null || shell.RightSidebar.Mode != RightPanelMode.Metadata)
            return;

        shell.SyncMetadataEditorForSelection(item);
    }
}
