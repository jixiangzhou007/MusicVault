using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.Localization;
namespace MusicVault.App.Controls;

/// <summary>
/// 桌面版短确认浮层：在应用主窗口客户区居中显示（非 Web 锚点气泡）；确认后执行 <see cref="ConfirmedCommand"/>。
/// </summary>
public class PopconfirmHost : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PopconfirmHost, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<PopconfirmHost, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<PopconfirmHost, string>(nameof(ConfirmText), string.Empty);

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<PopconfirmHost, string>(nameof(CancelText), string.Empty);

    public static readonly StyledProperty<bool> ConfirmIsDangerProperty =
        AvaloniaProperty.Register<PopconfirmHost, bool>(nameof(ConfirmIsDanger));

    public static readonly StyledProperty<string?> AlternateConfirmTextProperty =
        AvaloniaProperty.Register<PopconfirmHost, string?>(nameof(AlternateConfirmText));

    public static readonly StyledProperty<bool> ShowAlternateConfirmProperty =
        AvaloniaProperty.Register<PopconfirmHost, bool>(nameof(ShowAlternateConfirm));

    public static readonly StyledProperty<bool> AlternateConfirmIsDangerProperty =
        AvaloniaProperty.Register<PopconfirmHost, bool>(nameof(AlternateConfirmIsDanger), true);

    public static readonly StyledProperty<ICommand?> ConfirmedCommandProperty =
        AvaloniaProperty.Register<PopconfirmHost, ICommand?>(nameof(ConfirmedCommand));

    public static readonly StyledProperty<ICommand?> AlternateConfirmedCommandProperty =
        AvaloniaProperty.Register<PopconfirmHost, ICommand?>(nameof(AlternateConfirmedCommand));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<PopconfirmHost, object?>(nameof(CommandParameter));

    private Popup? _popup;
    private EventHandler<KeyEventArgs>? _keyDownHandler;

    static PopconfirmHost()
    {
        FocusableProperty.OverrideDefaultValue<PopconfirmHost>(false);
        ContentProperty.Changed.AddClassHandler<PopconfirmHost>((host, e) =>
        {
            if (e.OldValue is Button oldBtn)
                oldBtn.Click -= host.OnTriggerClick;
            if (e.NewValue is Button newBtn)
                newBtn.Click += host.OnTriggerClick;
        });
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

    public string ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    public string CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public bool ConfirmIsDanger
    {
        get => GetValue(ConfirmIsDangerProperty);
        set => SetValue(ConfirmIsDangerProperty, value);
    }

    public string? AlternateConfirmText
    {
        get => GetValue(AlternateConfirmTextProperty);
        set => SetValue(AlternateConfirmTextProperty, value);
    }

    public bool ShowAlternateConfirm
    {
        get => GetValue(ShowAlternateConfirmProperty);
        set => SetValue(ShowAlternateConfirmProperty, value);
    }

    public bool AlternateConfirmIsDanger
    {
        get => GetValue(AlternateConfirmIsDangerProperty);
        set => SetValue(AlternateConfirmIsDangerProperty, value);
    }

    public ICommand? ConfirmedCommand
    {
        get => GetValue(ConfirmedCommandProperty);
        set => SetValue(ConfirmedCommandProperty, value);
    }

    public ICommand? AlternateConfirmedCommand
    {
        get => GetValue(AlternateConfirmedCommandProperty);
        set => SetValue(AlternateConfirmedCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private string? _appliedConfirmDefault;
    private string? _appliedCancelDefault;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyDefaultButtonLabels();
        CommonDialogLabels.LabelsChanged += OnDialogLabelsChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CommonDialogLabels.LabelsChanged -= OnDialogLabelsChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDialogLabelsChanged(object? sender, EventArgs e) => ApplyDefaultButtonLabels();

    private void ApplyDefaultButtonLabels()
    {
        var confirm = CommonDialogLabels.Confirm;
        var cancel = CommonDialogLabels.Cancel;
        if (string.IsNullOrEmpty(ConfirmText) || ConfirmText == _appliedConfirmDefault)
            ConfirmText = confirm;
        if (string.IsNullOrEmpty(CancelText) || CancelText == _appliedCancelDefault)
            CancelText = cancel;
        _appliedConfirmDefault = confirm;
        _appliedCancelDefault = cancel;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        if (_popup != null)
        {
            _popup.Closed += OnPopupClosed;
            _popup.Opened += OnPopupOpened;
        }

        var cancelBtn = e.NameScope.Find<Button>("PART_CancelButton");
        if (cancelBtn != null)
            cancelBtn.Click += OnCancelClick;

        var confirmBtn = e.NameScope.Find<Button>("PART_ConfirmButton");
        if (confirmBtn != null)
            confirmBtn.Click += OnConfirmClick;

        var alternateBtn = e.NameScope.Find<Button>("PART_AlternateConfirmButton");
        if (alternateBtn != null)
            alternateBtn.Click += OnAlternateConfirmClick;

    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenPopup();
    }

    /// <summary>以窗口居中方式打开确认浮层（供上下文菜单等无触发按钮场景）。</summary>
    public void ShowConfirm() => OpenPopup();

    private void OpenPopup()
    {
        if (_popup == null)
            return;

        var anchor = TopLevel.GetTopLevel(this) as Control ?? ResolvePlacementTarget();
        if (anchor == null)
            return;

        _popup.Placement = PlacementMode.Center;
        _popup.PlacementTarget = anchor;

        Dispatcher.UIThread.Post(() =>
        {
            if (_popup != null && ReferenceEquals(_popup.PlacementTarget, anchor))
                _popup.IsOpen = true;
        }, DispatcherPriority.Loaded);
    }

    private Control? ResolvePlacementTarget() =>
        Content as Control ?? this;

    private void ClosePopup()
    {
        if (_popup != null)
            _popup.IsOpen = false;
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        _keyDownHandler ??= OnTopLevelKeyDown;
        topLevel.AddHandler(InputElement.KeyDownEvent, _keyDownHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null && _keyDownHandler != null)
            topLevel.RemoveHandler(InputElement.KeyDownEvent, _keyDownHandler);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _popup?.IsOpen != true)
            return;

        ClosePopup();
        e.Handled = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => ClosePopup();

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        ExecuteCommand(ConfirmedCommand);
        ClosePopup();
    }

    private void OnAlternateConfirmClick(object? sender, RoutedEventArgs e)
    {
        ExecuteCommand(AlternateConfirmedCommand);
        ClosePopup();
    }

    private void ExecuteCommand(ICommand? command)
    {
        if (command == null)
            return;

        var parameter = CommandParameter;
        if (command.CanExecute(parameter))
            command.Execute(parameter);
    }
}
