using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace MusicVault.App.Views.Overlays;

public partial class PreviewLoadingModalView : UserControl
{
    public PreviewLoadingModalView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // 等布局一帧后再淡入缩放，避免首帧跳变
        Dispatcher.UIThread.Post(() =>
        {
            if (PreviewLoadingCard is null)
                return;

            PreviewLoadingCard.Opacity = 1;
            PreviewLoadingCard.RenderTransform = TransformOperations.Parse("scale(1)");
        }, DispatcherPriority.Loaded);
    }
}
