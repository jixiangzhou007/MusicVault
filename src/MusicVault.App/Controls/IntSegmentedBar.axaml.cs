using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MusicVault.App.Controls;

/// <summary>通用整数索引 Segmented：代码切换 selected 类，避免 Classes.selected 绑定导致页面加载失败。</summary>
public partial class IntSegmentedBar : UserControl
{
    private readonly List<Button> _segmentButtons = [];

    public static readonly StyledProperty<IList<string>?> SegmentLabelsProperty =
        AvaloniaProperty.Register<IntSegmentedBar, IList<string>?>(nameof(SegmentLabels));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<IntSegmentedBar, int>(
            nameof(SelectedIndex),
            defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public IList<string>? SegmentLabels
    {
        get => GetValue(SegmentLabelsProperty);
        set => SetValue(SegmentLabelsProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public IntSegmentedBar()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentLabelsProperty)
            RebuildSegments();
        else if (change.Property == SelectedIndexProperty)
            ApplySelectedVisual();
    }

    private void RebuildSegments()
    {
        _segmentButtons.Clear();
        SegmentHost.Children.Clear();

        var labels = SegmentLabels;
        if (labels is null || labels.Count == 0)
            return;

        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = labels[i],
                Tag = index,
                Classes = { "ant-segmented-item" }
            };
            button.Click += OnSegmentClick;
            _segmentButtons.Add(button);
            SegmentHost.Children.Add(button);
        }

        if (SelectedIndex >= labels.Count)
            SelectedIndex = 0;

        ApplySelectedVisual();
    }

    private void OnSegmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index })
            return;

        SelectedIndex = index;
    }

    private void ApplySelectedVisual()
    {
        var selected = SelectedIndex;
        foreach (var button in _segmentButtons)
        {
            if (button.Tag is not int index)
                continue;

            button.Classes.Set("selected", index == selected);
        }
    }
}
