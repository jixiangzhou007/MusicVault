using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MusicVault.App.Views;

public partial class PageContentHost : UserControl
{
    public PageContentHost()
    {
        InitializeComponent();
        PageHost.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty)
                Dispatcher.UIThread.Post(EnsurePageContentStretch);
        };
        LayoutUpdated += (_, _) => EnsurePageContentStretch();
    }

    private void EnsurePageContentStretch()
    {
        PageHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        PageHost.VerticalAlignment = VerticalAlignment.Stretch;
        PageHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        PageHost.VerticalContentAlignment = VerticalAlignment.Stretch;

        foreach (var child in PageHost.GetVisualDescendants())
        {
            if (child is ContentPresenter presenter)
            {
                presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                presenter.VerticalAlignment = VerticalAlignment.Stretch;
                presenter.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                presenter.VerticalContentAlignment = VerticalAlignment.Stretch;
            }

            if (child is UserControl pageRoot)
            {
                pageRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
                pageRoot.VerticalAlignment = VerticalAlignment.Stretch;
                pageRoot.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                pageRoot.VerticalContentAlignment = VerticalAlignment.Stretch;
            }
        }
    }
}
