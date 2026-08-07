using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MusicVault.App;

/// <summary>
/// 全局按钮文字居中：对所有 Button/ToggleButton 在模板应用后统一修正 PART_ContentPresenter。
/// 与 Styles/ButtonControlThemes.axaml 配合；新建按钮无需逐页设置。
/// </summary>
internal static class ButtonAlignmentFix
{
    public static void Install()
    {
        Button.TemplateAppliedEvent.AddClassHandler<Button>(OnTemplateApplied, handledEventsToo: true);
        ToggleButton.TemplateAppliedEvent.AddClassHandler<ToggleButton>(OnTemplateApplied, handledEventsToo: true);
    }

    private static void OnTemplateApplied(Button sender, TemplateAppliedEventArgs e) => Apply(sender, e);

    private static void OnTemplateApplied(ToggleButton sender, TemplateAppliedEventArgs e) => Apply(sender, e);

    private static void Apply(ContentControl control, TemplateAppliedEventArgs e)
    {
        if (control.Classes.Contains("nav-item"))
            return;

        var preserveLeft = control.HorizontalContentAlignment == HorizontalAlignment.Left;

        if (!preserveLeft && control.HorizontalContentAlignment != HorizontalAlignment.Stretch)
        {
            control.HorizontalContentAlignment = HorizontalAlignment.Center;
            control.VerticalContentAlignment = VerticalAlignment.Center;
        }

        if (e.NameScope.Find("PART_ContentPresenter") is not ContentPresenter presenter)
            return;

        presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
        presenter.VerticalAlignment = VerticalAlignment.Stretch;

        if (preserveLeft)
        {
            presenter.HorizontalContentAlignment = HorizontalAlignment.Left;
            return;
        }

        if (control.HorizontalContentAlignment == HorizontalAlignment.Stretch)
        {
            presenter.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return;
        }

        presenter.HorizontalContentAlignment = HorizontalAlignment.Center;
        presenter.VerticalContentAlignment = VerticalAlignment.Center;
        CenterTextChildren(presenter);

        void OnLayoutUpdated(object? _, EventArgs __)
        {
            if (presenter.GetVisualChildren().Any())
            {
                presenter.LayoutUpdated -= OnLayoutUpdated;
                CenterTextChildren(presenter);
            }
        }

        presenter.LayoutUpdated += OnLayoutUpdated;
    }

    private static void CenterTextChildren(ContentPresenter presenter)
    {
        foreach (var child in presenter.GetVisualChildren())
        {
            switch (child)
            {
                case AccessText accessText:
                    accessText.HorizontalAlignment = HorizontalAlignment.Center;
                    accessText.VerticalAlignment = VerticalAlignment.Center;
                    accessText.TextAlignment = TextAlignment.Center;
                    break;
                case TextBlock textBlock:
                    textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    textBlock.VerticalAlignment = VerticalAlignment.Center;
                    textBlock.TextAlignment = TextAlignment.Center;
                    break;
            }
        }
    }
}
