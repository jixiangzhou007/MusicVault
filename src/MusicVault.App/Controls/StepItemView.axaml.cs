using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

public partial class StepItemView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<StepItemView, string>(nameof(Title));

    public static readonly StyledProperty<StepStatus> StatusProperty =
        AvaloniaProperty.Register<StepItemView, StepStatus>(nameof(Status), StepStatus.Wait);

    public static readonly StyledProperty<int> StepNumberProperty =
        AvaloniaProperty.Register<StepItemView, int>(nameof(StepNumber), 1);

    public static readonly StyledProperty<bool> ShowConnectorProperty =
        AvaloniaProperty.Register<StepItemView, bool>(nameof(ShowConnector), true);

    static StepItemView()
    {
        StatusProperty.Changed.AddClassHandler<StepItemView>((view, _) => view.ApplyStatus());
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public StepStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public int StepNumber
    {
        get => GetValue(StepNumberProperty);
        set => SetValue(StepNumberProperty, value);
    }

    public bool ShowConnector
    {
        get => GetValue(ShowConnectorProperty);
        set => SetValue(ShowConnectorProperty, value);
    }

    public StepItemView()
    {
        InitializeComponent();
        ApplyStatus();
    }

    private void ApplyStatus()
    {
        if (IconBorder is null || IconGlyph is null)
            return;

        IconBorder.Classes.Clear();
        IconBorder.Classes.Add("ant-steps-icon");
        IconBorder.Classes.Add(Status switch
        {
            StepStatus.Process => "ant-steps-icon-process",
            StepStatus.Finish => "ant-steps-icon-finish",
            StepStatus.Error => "ant-steps-icon-error",
            _ => "ant-steps-icon-wait",
        });

        IconGlyph.Text = Status switch
        {
            StepStatus.Finish => "\uE73E",
            StepStatus.Error => "\uE783",
            _ => StepNumber.ToString(),
        };

        IconGlyph.Classes.Clear();
        IconGlyph.Classes.Add("ant-steps-icon-glyph");
        IconGlyph.Classes.Add(Status switch
        {
            StepStatus.Process => "ant-steps-glyph-process",
            StepStatus.Finish => "ant-steps-glyph-finish",
            StepStatus.Error => "ant-steps-glyph-error",
            _ => "ant-steps-glyph-wait",
        });
    }
}
