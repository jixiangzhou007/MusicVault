using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class ResultView : UserControl
{
    public static readonly StyledProperty<ResultStatus> StatusProperty =
        AvaloniaProperty.Register<ResultView, ResultStatus>(nameof(Status), ResultStatus.Info);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ResultView, string>(nameof(Title));

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ResultView, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<ResultView, bool>(nameof(ShowIcon), true);

    public static readonly StyledProperty<object?> ExtraProperty =
        AvaloniaProperty.Register<ResultView, object?>(nameof(Extra));

    public static readonly StyledProperty<bool> ShowActionsProperty =
        AvaloniaProperty.Register<ResultView, bool>(nameof(ShowActions));

    public static readonly StyledProperty<string> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<ResultView, string>(nameof(PrimaryButtonText), string.Empty);

    public static readonly StyledProperty<string> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<ResultView, string>(nameof(SecondaryButtonText), string.Empty);

    public static readonly StyledProperty<bool> ShowPrimaryButtonProperty =
        AvaloniaProperty.Register<ResultView, bool>(nameof(ShowPrimaryButton));

    public static readonly StyledProperty<bool> ShowSecondaryButtonProperty =
        AvaloniaProperty.Register<ResultView, bool>(nameof(ShowSecondaryButton));

    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<ResultView, ICommand?>(nameof(PrimaryCommand));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<ResultView, ICommand?>(nameof(SecondaryCommand));

    private string? _appliedPrimaryDefault;
    private string? _appliedSecondaryDefault;

    static ResultView()
    {
        StatusProperty.Changed.AddClassHandler<ResultView>((view, _) => view.ApplyStatus());
    }

    public ResultStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool ShowIcon
    {
        get => GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public object? Extra
    {
        get => GetValue(ExtraProperty);
        set => SetValue(ExtraProperty, value);
    }

    public bool ShowActions
    {
        get => GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public string PrimaryButtonText
    {
        get => GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public string SecondaryButtonText
    {
        get => GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public bool ShowPrimaryButton
    {
        get => GetValue(ShowPrimaryButtonProperty);
        set => SetValue(ShowPrimaryButtonProperty, value);
    }

    public bool ShowSecondaryButton
    {
        get => GetValue(ShowSecondaryButtonProperty);
        set => SetValue(ShowSecondaryButtonProperty, value);
    }

    public ICommand? PrimaryCommand
    {
        get => GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public ResultView()
    {
        InitializeComponent();
        ApplyDefaultButtonLabels();
        CommonDialogLabels.LabelsChanged += OnDialogLabelsChanged;
        ApplyStatus();
    }

    private void OnDialogLabelsChanged(object? sender, EventArgs e) => ApplyDefaultButtonLabels();

    private void ApplyDefaultButtonLabels()
    {
        var confirm = CommonDialogLabels.Confirm;
        var cancel = CommonDialogLabels.Cancel;
        if (string.IsNullOrEmpty(PrimaryButtonText) || PrimaryButtonText == _appliedPrimaryDefault)
            PrimaryButtonText = confirm;
        if (string.IsNullOrEmpty(SecondaryButtonText) || SecondaryButtonText == _appliedSecondaryDefault)
            SecondaryButtonText = cancel;
        _appliedPrimaryDefault = confirm;
        _appliedSecondaryDefault = cancel;
    }

    private void ApplyStatus()
    {
        if (IconGlyph is null || IconHost is null)
            return;

        IconGlyph.Text = ResultStatusGlyphs.GetGlyph(Status);
        IconHost.Classes.Clear();
        IconHost.Classes.Add("ant-result-icon");
        IconHost.Classes.Add(ResultStatusGlyphs.GetStatusClass(Status));

        IconGlyph.Classes.Clear();
        IconGlyph.Classes.Add("icon-2xl");
        IconGlyph.Classes.Add(Status switch
        {
            ResultStatus.Success => "ant-result-glyph-success",
            ResultStatus.Error => "ant-result-glyph-error",
            ResultStatus.Warning => "ant-result-glyph-warning",
            _ => "ant-result-glyph-info",
        });
    }
}
