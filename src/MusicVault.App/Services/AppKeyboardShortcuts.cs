using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace MusicVault.App.Services;

/// <summary>应用内快捷键：在文本输入框与可聚焦控件上时不拦截空格/方向键，避免与编辑或按钮操作冲突。</summary>
public static class AppKeyboardShortcuts
{
    public static bool IsTextEntryFocused(Visual? relativeTo)
    {
        var focused = GetFocusedElement(relativeTo);
        if (focused == null)
            return false;

        if (focused is TextBox)
            return true;

        if (focused is ComboBox { IsDropDownOpen: true })
            return true;

        return focused is Visual visual && visual.FindAncestorOfType<TextBox>() != null;
    }

    /// <summary>焦点在按钮、列表、滑块等控件上时不应触发播放快捷键。</summary>
    public static bool ShouldSuppressPlaybackShortcuts(object? focusedElement)
    {
        if (focusedElement == null)
            return false;

        if (focusedElement is TextBox or ComboBox or Button or ToggleButton or CheckBox or RadioButton
            or Slider or ListBoxItem or TabItem or MenuItem or Expander or ScrollBar)
            return true;

        if (focusedElement is not Visual visual)
            return false;

        return visual.FindAncestorOfType<Button>() != null
               || visual.FindAncestorOfType<ToggleButton>() != null
               || visual.FindAncestorOfType<ListBox>() != null
               || visual.FindAncestorOfType<ComboBox>() != null
               || visual.FindAncestorOfType<Slider>() != null;
    }

    public static bool TryHandlePlaybackShortcut(
        KeyEventArgs e,
        Visual? relativeTo,
        bool enabled,
        Action<KeyEventArgs>? onSpace,
        Action<KeyEventArgs>? onLeft,
        Action<KeyEventArgs>? onRight,
        Action<KeyEventArgs>? onCtrlL,
        Action<KeyEventArgs>? onCtrlF)
    {
        if (!enabled)
            return false;

        var focused = GetFocusedElement(relativeTo);
        if (IsTextEntryFocused(relativeTo) || ShouldSuppressPlaybackShortcuts(focused))
            return false;

        switch (e.Key)
        {
            case Key.Space when e.KeyModifiers == KeyModifiers.None:
                onSpace?.Invoke(e);
                return true;
            case Key.Left when e.KeyModifiers == KeyModifiers.None:
                onLeft?.Invoke(e);
                return true;
            case Key.Right when e.KeyModifiers == KeyModifiers.None:
                onRight?.Invoke(e);
                return true;
            case Key.L when e.KeyModifiers == KeyModifiers.Control:
                onCtrlL?.Invoke(e);
                return true;
            case Key.F when e.KeyModifiers == KeyModifiers.Control:
                onCtrlF?.Invoke(e);
                return true;
        }

        return false;
    }

    private static object? GetFocusedElement(Visual? relativeTo) =>
        TopLevel.GetTopLevel(relativeTo)?.FocusManager?.GetFocusedElement();
}
