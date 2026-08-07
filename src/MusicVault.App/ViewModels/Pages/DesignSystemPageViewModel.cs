using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Controls;
using MusicVault.App.Services;
using MusicVault.Core.Enums;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设计系统预览（验证 Token / 组件 class / Accent 联动）。</summary>
public partial class DesignSystemPageViewModel : ViewModelBase
{
    private readonly AppSettingsService _settings;

    public DesignSystemPageViewModel(AppSettingsService settings)
    {
        _settings = settings;
        App.Localization.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshAccentLabel();
        RefreshLocalizedStrings();
    }

    private static string L(string key) => App.Localization.Get(key);

    [ObservableProperty] private string _currentAccentLabel = AccentPalette.DefaultAccentHex;

    public IReadOnlyList<StepItem> DemoWizardSteps { get; private set; } = [];

    public string PageTitle => L("DesignSystem.PageTitle");

    public string AccentLabelText => string.Format(L("DesignSystem.AccentLabelFormat"), CurrentAccentLabel);

    public string ThemeLabelText => string.Format(L("DesignSystem.ThemeLabelFormat"), CurrentThemeName);

    public string CurrentThemeName => _settings.Current.ThemeMode switch
    {
        AppThemeMode.Light => L("Settings.Theme.Light"),
        AppThemeMode.Dark => L("Settings.Theme.Dark"),
        _ => L("Settings.Theme.System")
    };

    public string DocNote => L("DesignSystem.DocNote");

    public string FeedbackTitle => L("DesignSystem.Feedback.Title");
    public string FeedbackIntro => L("DesignSystem.Feedback.Intro");
    public string FeedbackSceneTip => L("DesignSystem.Feedback.SceneTip");
    public string MessageDesc => L("DesignSystem.Feedback.MessageDesc");
    public string AlertDesc => L("DesignSystem.Feedback.AlertDesc");
    public string EmptyDesc => L("DesignSystem.Feedback.EmptyDesc");
    public string ModalDesc => L("DesignSystem.Feedback.ModalDesc");
    public string DrawerDesc => L("DesignSystem.Feedback.DrawerDesc");
    public string TooltipDesc => L("DesignSystem.Feedback.TooltipDesc");
    public string ProgressDesc => L("DesignSystem.Feedback.ProgressDesc");
    public string ResultDesc => L("DesignSystem.Feedback.ResultDesc");
    public string SkeletonDesc => L("DesignSystem.Feedback.SkeletonDesc");
    public string PopconfirmDesc => L("DesignSystem.Feedback.PopconfirmDesc");

    public string SemanticColorsTitle => L("DesignSystem.SemanticColors.Title");
    public string SemanticColorsIntro => L("DesignSystem.SemanticColors.Intro");
    public string InfoCaption => L("DesignSystem.SemanticColors.InfoCaption");
    public string SuccessCaption => L("DesignSystem.SemanticColors.SuccessCaption");
    public string WarningCaption => L("DesignSystem.SemanticColors.WarningCaption");
    public string ErrorCaption => L("DesignSystem.SemanticColors.ErrorCaption");

    public string MotionTitle => L("DesignSystem.Motion.Title");
    public string MotionIntro => L("DesignSystem.Motion.Intro");
    public string MotionFastLine => L("DesignSystem.Motion.FastLine");
    public string MotionBaseLine => L("DesignSystem.Motion.BaseLine");
    public string MotionDrawerLine => L("DesignSystem.Motion.DrawerLine");

    public string NotAdaptTitle => L("DesignSystem.NotAdapt.Title");
    public string NotAdaptMessage => L("DesignSystem.NotAdapt.Message");
    public string NotAdaptDescription => L("DesignSystem.NotAdapt.Description");

    public string PaletteTitle => L("DesignSystem.Palette.Title");
    public string AccentSoftSample => L("DesignSystem.Palette.AccentSoftSample");
    public string OnAccentSoftLabel => L("DesignSystem.Palette.OnAccentSoftLabel");
    public string StatPurpleFixed => L("DesignSystem.Palette.StatPurpleFixed");
    public string StatOrangeFixed => L("DesignSystem.Palette.StatOrangeFixed");
    public string StatPinkFixed => L("DesignSystem.Palette.StatPinkFixed");
    public string WarningSample => L("DesignSystem.Palette.WarningSample");

    public string TypographyTitle => L("DesignSystem.Typography.Title");
    public string TypeMicroCaption => L("DesignSystem.Typography.MicroCaption");
    public string TypeLeadCaption => L("DesignSystem.Typography.LeadCaption");

    public string IconsTitle => L("DesignSystem.Icons.Title");
    public string IconsIntro => L("DesignSystem.Icons.Intro");
    public string IconsSizes => L("DesignSystem.Icons.Sizes");

    public string DefaultControlsTitle => L("DesignSystem.DefaultControls.Title");
    public string DefaultControlsIntro => L("DesignSystem.DefaultControls.Intro");
    public string DefaultButtonLabel => L("DesignSystem.DefaultControls.Button");
    public string DefaultTextBoxWatermark => L("DesignSystem.DefaultControls.TextBoxWatermark");
    public string DefaultComboBoxLabel => L("DesignSystem.DefaultControls.ComboBox");
    public string DefaultComboBoxOptionB => L("DesignSystem.DefaultControls.ComboBoxOptionB");
    public string DefaultCheckBoxLabel => L("DesignSystem.DefaultControls.CheckBox");

    public string SpacingTitle => L("DesignSystem.Spacing.Title");
    public string SpacingControlHeightTokens => L("DesignSystem.Spacing.ControlHeightTokens");
    public string ControlHeightLegend => L("DesignSystem.Spacing.ControlHeightLegend");

    public string ButtonsTitle => L("DesignSystem.Buttons.Title");
    public string ButtonsPrimaryCaption => L("DesignSystem.Buttons.PrimaryCaption");
    public string ButtonsToolbarCaption => L("DesignSystem.Buttons.ToolbarCaption");
    public string ButtonsIconCaption => L("DesignSystem.Buttons.IconCaption");
    public string ButtonsDialogCaption => L("DesignSystem.Buttons.DialogCaption");
    public string PrimaryToolbarButton => L("DesignSystem.Buttons.PrimaryToolbar");
    public string PrimaryDialogButton => L("DesignSystem.Buttons.PrimaryDialog");

    public string SearchTitle => L("DesignSystem.Search.Title");
    public string SearchWatermark => L("DesignSystem.Search.Watermark");
    public string SearchButton => L("DesignSystem.Search.Button");
    public string SearchClassCaption => L("DesignSystem.Search.ClassCaption");

    public string FormTitle => L("DesignSystem.Form.Title");
    public string FormRadioLabel => L("DesignSystem.Form.RadioLabel");
    public string FormCheckLabel => L("DesignSystem.Form.CheckLabel");
    public string FormInputLabel => L("DesignSystem.Form.InputLabel");
    public string FormSelectLabel => L("DesignSystem.Form.SelectLabel");
    public string FormOptionA => L("DesignSystem.Form.OptionA");
    public string FormOptionB => L("DesignSystem.Form.OptionB");
    public string FormAutoCoverLyrics => L("DesignSystem.Form.AutoCoverLyrics");
    public string FormInputWatermark => L("DesignSystem.Form.InputWatermark");

    public string PopconfirmTitle => L("DesignSystem.Popconfirm.Title");
    public string PopconfirmIntro => L("DesignSystem.Popconfirm.Intro");
    public string PopconfirmDeleteTitle => L("DesignSystem.Popconfirm.DeleteTitle");
    public string PopconfirmDeleteDescription => L("DesignSystem.Popconfirm.DeleteDescription");
    public string PopconfirmDeleteConfirm => L("DesignSystem.Popconfirm.DeleteConfirm");
    public string PopconfirmDeleteButton => L("DesignSystem.Popconfirm.DeleteButton");
    public string PopconfirmClearTitle => L("DesignSystem.Popconfirm.ClearTitle");
    public string PopconfirmClearDescription => L("DesignSystem.Popconfirm.ClearDescription");
    public string PopconfirmClearConfirm => L("DesignSystem.Popconfirm.ClearConfirm");
    public string PopconfirmClearAlternate => L("DesignSystem.Popconfirm.ClearAlternate");
    public string PopconfirmClearButton => L("DesignSystem.Popconfirm.ClearButton");

    public string ModalOverlayTitle => L("DesignSystem.ModalOverlay.Title");
    public string ModalDialogTitle => L("DesignSystem.ModalOverlay.DialogTitle");
    public string ModalDialogBody => L("DesignSystem.ModalOverlay.DialogBody");
    public string ModalCancel => L("DesignSystem.ModalOverlay.Cancel");
    public string ModalConfirm => L("DesignSystem.ModalOverlay.Confirm");

    public string ListRowTitle => L("DesignSystem.ListRow.Title");
    public string ListRowNormal => L("DesignSystem.ListRow.Normal");
    public string ListRowSelected => L("DesignSystem.ListRow.Selected");

    public string TooltipSectionTitle => L("DesignSystem.TooltipSection.Title");
    public string TooltipSectionIntro => L("DesignSystem.TooltipSection.Intro");
    public string TooltipHoverButton => L("DesignSystem.TooltipSection.HoverButton");
    public string TooltipHoverTip => L("DesignSystem.TooltipSection.HoverTip");
    public string TooltipIconTip => L("DesignSystem.TooltipSection.IconTip");

    public string ProgressSectionTitle => L("DesignSystem.ProgressSection.Title");
    public string ProgressSectionIntro => L("DesignSystem.ProgressSection.Intro");
    public string ProgressDefaultLabel => L("DesignSystem.ProgressSection.DefaultLabel");
    public string ProgressSmallLabel => L("DesignSystem.ProgressSection.SmallLabel");
    public string ProgressModalLabel => L("DesignSystem.ProgressSection.ModalLabel");
    public string ProgressActiveLabel => L("DesignSystem.ProgressSection.ActiveLabel");
    public string ProgressStatusLabel => L("DesignSystem.ProgressSection.StatusLabel");

    public string ResultSectionTitle => L("DesignSystem.ResultSection.Title");
    public string ResultSectionIntro => L("DesignSystem.ResultSection.Intro");
    public string ResultSuccessTitle => L("DesignSystem.ResultSection.SuccessTitle");
    public string ResultSuccessDescription => L("DesignSystem.ResultSection.SuccessDescription");
    public string ResultWarningTitle => L("DesignSystem.ResultSection.WarningTitle");
    public string ResultWarningDescription => L("DesignSystem.ResultSection.WarningDescription");
    public string ResultErrorTitle => L("DesignSystem.ResultSection.ErrorTitle");
    public string ResultErrorDescription => L("DesignSystem.ResultSection.ErrorDescription");
    public string ResultInfoTitle => L("DesignSystem.ResultSection.InfoTitle");
    public string ResultInfoDescription => L("DesignSystem.ResultSection.InfoDescription");
    public string ResultUpdateNow => L("DesignSystem.ResultSection.UpdateNow");
    public string ResultUpdateLater => L("DesignSystem.ResultSection.UpdateLater");

    public string SkeletonSectionTitle => L("DesignSystem.SkeletonSection.Title");
    public string SkeletonSectionIntro => L("DesignSystem.SkeletonSection.Intro");
    public string SkeletonBasicLabel => L("DesignSystem.SkeletonSection.BasicLabel");
    public string SkeletonCompositeLabel => L("DesignSystem.SkeletonSection.CompositeLabel");
    public string SkeletonBrowseLabel => L("DesignSystem.SkeletonSection.BrowseLabel");
    public string SkeletonDiscoverLabel => L("DesignSystem.SkeletonSection.DiscoverLabel");

    public string FormDrawerTitle => L("DesignSystem.FormDrawer.Title");
    public string FormDrawerIntro => L("DesignSystem.FormDrawer.Intro");
    public string FormDrawerAlertMessage => L("DesignSystem.FormDrawer.AlertMessage");
    public string FormDrawerAlertDescription => L("DesignSystem.FormDrawer.AlertDescription");

    public string StepsTitle => L("DesignSystem.Steps.Title");
    public string StepsIntro => L("DesignSystem.Steps.Intro");

    public string BadgeTitle => L("DesignSystem.Badge.Title");
    public string BadgeIntro => L("DesignSystem.Badge.Intro");
    public string BadgeCountLabel => L("DesignSystem.Badge.CountLabel");
    public string BadgeTransferLabel => L("DesignSystem.Badge.TransferLabel");
    public string BadgeStatusLabel => L("DesignSystem.Badge.StatusLabel");
    public string BadgeDotLabel => L("DesignSystem.Badge.DotLabel");

    public string PopoverTitle => L("DesignSystem.Popover.Title");
    public string PopoverIntro => L("DesignSystem.Popover.Intro");
    public string PopoverRefresh => L("DesignSystem.Popover.Refresh");
    public string PopoverClearOverview => L("DesignSystem.Popover.ClearOverview");
    public string PopoverClearAll => L("DesignSystem.Popover.ClearAll");
    public string PopoverHint => L("DesignSystem.Popover.Hint");

    public string ThemePlayerTitle => L("DesignSystem.ThemePlayer.Title");
    public string ThemePlayerIntro => L("DesignSystem.ThemePlayer.Intro");
    public string ThemeLightThemeLabel => L("DesignSystem.ThemePlayer.LightThemeLabel");
    public string ThemeLightThemeDesc => L("DesignSystem.ThemePlayer.LightThemeDesc");
    public string ThemeAuditLabel => L("DesignSystem.ThemePlayer.AuditLabel");
    public string ThemeAuditDesc => L("DesignSystem.ThemePlayer.AuditDesc");
    public string ThemePlayerChromeSample => L("DesignSystem.ThemePlayer.ChromeSample");
    public string ThemePlayerChromeControls => L("DesignSystem.ThemePlayer.ChromeControls");

    public string AntFeedbackTitle => L("DesignSystem.AntFeedback.Title");
    public string AntFeedbackAlertMessage => L("DesignSystem.AntFeedback.AlertMessage");
    public string AntFeedbackAlertDescription => L("DesignSystem.AntFeedback.AlertDescription");
    public string EmptyStateTitle => L("DesignSystem.AntFeedback.EmptyTitle");
    public string EmptyStateDescription => L("DesignSystem.AntFeedback.EmptyDescription");
    public string SegmentedNote => L("DesignSystem.AntFeedback.SegmentedNote");

    public void RefreshAccentLabel()
    {
        CurrentAccentLabel = AccentColorPresets.Normalize(_settings.Current.AccentColor);
        OnPropertyChanged(nameof(AccentLabelText));
    }

    public void RefreshThemeLabel()
    {
        OnPropertyChanged(nameof(CurrentThemeName));
        OnPropertyChanged(nameof(ThemeLabelText));
    }

    public void RefreshLocalizedStrings()
    {
        DemoWizardSteps =
        [
            new StepItem(L("Discover.BatchEnqueue.Modal.Step.Select"), StepStatus.Finish, 1),
            new StepItem(L("Discover.BatchEnqueue.Modal.Step.Confirm"), StepStatus.Process, 2),
            new StepItem(L("DesignSystem.Steps.WriteQueue"), StepStatus.Wait, 3, showConnector: false),
        ];

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(AccentLabelText));
        OnPropertyChanged(nameof(ThemeLabelText));
        OnPropertyChanged(nameof(CurrentThemeName));
        OnPropertyChanged(nameof(DocNote));
        OnPropertyChanged(nameof(FeedbackTitle));
        OnPropertyChanged(nameof(FeedbackIntro));
        OnPropertyChanged(nameof(FeedbackSceneTip));
        OnPropertyChanged(nameof(MessageDesc));
        OnPropertyChanged(nameof(AlertDesc));
        OnPropertyChanged(nameof(EmptyDesc));
        OnPropertyChanged(nameof(ModalDesc));
        OnPropertyChanged(nameof(DrawerDesc));
        OnPropertyChanged(nameof(TooltipDesc));
        OnPropertyChanged(nameof(ProgressDesc));
        OnPropertyChanged(nameof(ResultDesc));
        OnPropertyChanged(nameof(SkeletonDesc));
        OnPropertyChanged(nameof(PopconfirmDesc));
        OnPropertyChanged(nameof(SemanticColorsTitle));
        OnPropertyChanged(nameof(SemanticColorsIntro));
        OnPropertyChanged(nameof(InfoCaption));
        OnPropertyChanged(nameof(SuccessCaption));
        OnPropertyChanged(nameof(WarningCaption));
        OnPropertyChanged(nameof(ErrorCaption));
        OnPropertyChanged(nameof(MotionTitle));
        OnPropertyChanged(nameof(MotionIntro));
        OnPropertyChanged(nameof(MotionFastLine));
        OnPropertyChanged(nameof(MotionBaseLine));
        OnPropertyChanged(nameof(MotionDrawerLine));
        OnPropertyChanged(nameof(NotAdaptTitle));
        OnPropertyChanged(nameof(NotAdaptMessage));
        OnPropertyChanged(nameof(NotAdaptDescription));
        OnPropertyChanged(nameof(PaletteTitle));
        OnPropertyChanged(nameof(AccentSoftSample));
        OnPropertyChanged(nameof(OnAccentSoftLabel));
        OnPropertyChanged(nameof(StatPurpleFixed));
        OnPropertyChanged(nameof(StatOrangeFixed));
        OnPropertyChanged(nameof(StatPinkFixed));
        OnPropertyChanged(nameof(WarningSample));
        OnPropertyChanged(nameof(TypographyTitle));
        OnPropertyChanged(nameof(TypeMicroCaption));
        OnPropertyChanged(nameof(TypeLeadCaption));
        OnPropertyChanged(nameof(IconsTitle));
        OnPropertyChanged(nameof(IconsIntro));
        OnPropertyChanged(nameof(IconsSizes));
        OnPropertyChanged(nameof(DefaultControlsTitle));
        OnPropertyChanged(nameof(DefaultControlsIntro));
        OnPropertyChanged(nameof(DefaultButtonLabel));
        OnPropertyChanged(nameof(DefaultTextBoxWatermark));
        OnPropertyChanged(nameof(DefaultComboBoxLabel));
        OnPropertyChanged(nameof(DefaultComboBoxOptionB));
        OnPropertyChanged(nameof(DefaultCheckBoxLabel));
        OnPropertyChanged(nameof(SpacingTitle));
        OnPropertyChanged(nameof(SpacingControlHeightTokens));
        OnPropertyChanged(nameof(ControlHeightLegend));
        OnPropertyChanged(nameof(ButtonsTitle));
        OnPropertyChanged(nameof(ButtonsPrimaryCaption));
        OnPropertyChanged(nameof(ButtonsToolbarCaption));
        OnPropertyChanged(nameof(ButtonsIconCaption));
        OnPropertyChanged(nameof(ButtonsDialogCaption));
        OnPropertyChanged(nameof(PrimaryToolbarButton));
        OnPropertyChanged(nameof(PrimaryDialogButton));
        OnPropertyChanged(nameof(SearchTitle));
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(SearchButton));
        OnPropertyChanged(nameof(SearchClassCaption));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(FormRadioLabel));
        OnPropertyChanged(nameof(FormCheckLabel));
        OnPropertyChanged(nameof(FormInputLabel));
        OnPropertyChanged(nameof(FormSelectLabel));
        OnPropertyChanged(nameof(FormOptionA));
        OnPropertyChanged(nameof(FormOptionB));
        OnPropertyChanged(nameof(FormAutoCoverLyrics));
        OnPropertyChanged(nameof(FormInputWatermark));
        OnPropertyChanged(nameof(PopconfirmTitle));
        OnPropertyChanged(nameof(PopconfirmIntro));
        OnPropertyChanged(nameof(PopconfirmDeleteTitle));
        OnPropertyChanged(nameof(PopconfirmDeleteDescription));
        OnPropertyChanged(nameof(PopconfirmDeleteConfirm));
        OnPropertyChanged(nameof(PopconfirmDeleteButton));
        OnPropertyChanged(nameof(PopconfirmClearTitle));
        OnPropertyChanged(nameof(PopconfirmClearDescription));
        OnPropertyChanged(nameof(PopconfirmClearConfirm));
        OnPropertyChanged(nameof(PopconfirmClearAlternate));
        OnPropertyChanged(nameof(PopconfirmClearButton));
        OnPropertyChanged(nameof(ModalOverlayTitle));
        OnPropertyChanged(nameof(ModalDialogTitle));
        OnPropertyChanged(nameof(ModalDialogBody));
        OnPropertyChanged(nameof(ModalCancel));
        OnPropertyChanged(nameof(ModalConfirm));
        OnPropertyChanged(nameof(ListRowTitle));
        OnPropertyChanged(nameof(ListRowNormal));
        OnPropertyChanged(nameof(ListRowSelected));
        OnPropertyChanged(nameof(TooltipSectionTitle));
        OnPropertyChanged(nameof(TooltipSectionIntro));
        OnPropertyChanged(nameof(TooltipHoverButton));
        OnPropertyChanged(nameof(TooltipHoverTip));
        OnPropertyChanged(nameof(TooltipIconTip));
        OnPropertyChanged(nameof(ProgressSectionTitle));
        OnPropertyChanged(nameof(ProgressSectionIntro));
        OnPropertyChanged(nameof(ProgressDefaultLabel));
        OnPropertyChanged(nameof(ProgressSmallLabel));
        OnPropertyChanged(nameof(ProgressModalLabel));
        OnPropertyChanged(nameof(ProgressActiveLabel));
        OnPropertyChanged(nameof(ProgressStatusLabel));
        OnPropertyChanged(nameof(ResultSectionTitle));
        OnPropertyChanged(nameof(ResultSectionIntro));
        OnPropertyChanged(nameof(ResultSuccessTitle));
        OnPropertyChanged(nameof(ResultSuccessDescription));
        OnPropertyChanged(nameof(ResultWarningTitle));
        OnPropertyChanged(nameof(ResultWarningDescription));
        OnPropertyChanged(nameof(ResultErrorTitle));
        OnPropertyChanged(nameof(ResultErrorDescription));
        OnPropertyChanged(nameof(ResultInfoTitle));
        OnPropertyChanged(nameof(ResultInfoDescription));
        OnPropertyChanged(nameof(ResultUpdateNow));
        OnPropertyChanged(nameof(ResultUpdateLater));
        OnPropertyChanged(nameof(SkeletonSectionTitle));
        OnPropertyChanged(nameof(SkeletonSectionIntro));
        OnPropertyChanged(nameof(SkeletonBasicLabel));
        OnPropertyChanged(nameof(SkeletonCompositeLabel));
        OnPropertyChanged(nameof(SkeletonBrowseLabel));
        OnPropertyChanged(nameof(SkeletonDiscoverLabel));
        OnPropertyChanged(nameof(FormDrawerTitle));
        OnPropertyChanged(nameof(FormDrawerIntro));
        OnPropertyChanged(nameof(FormDrawerAlertMessage));
        OnPropertyChanged(nameof(FormDrawerAlertDescription));
        OnPropertyChanged(nameof(StepsTitle));
        OnPropertyChanged(nameof(StepsIntro));
        OnPropertyChanged(nameof(DemoWizardSteps));
        OnPropertyChanged(nameof(BadgeTitle));
        OnPropertyChanged(nameof(BadgeIntro));
        OnPropertyChanged(nameof(BadgeCountLabel));
        OnPropertyChanged(nameof(BadgeTransferLabel));
        OnPropertyChanged(nameof(BadgeStatusLabel));
        OnPropertyChanged(nameof(BadgeDotLabel));
        OnPropertyChanged(nameof(PopoverTitle));
        OnPropertyChanged(nameof(PopoverIntro));
        OnPropertyChanged(nameof(PopoverRefresh));
        OnPropertyChanged(nameof(PopoverClearOverview));
        OnPropertyChanged(nameof(PopoverClearAll));
        OnPropertyChanged(nameof(PopoverHint));
        OnPropertyChanged(nameof(ThemePlayerTitle));
        OnPropertyChanged(nameof(ThemePlayerIntro));
        OnPropertyChanged(nameof(ThemeLightThemeLabel));
        OnPropertyChanged(nameof(ThemeLightThemeDesc));
        OnPropertyChanged(nameof(ThemeAuditLabel));
        OnPropertyChanged(nameof(ThemeAuditDesc));
        OnPropertyChanged(nameof(ThemePlayerChromeSample));
        OnPropertyChanged(nameof(ThemePlayerChromeControls));
        OnPropertyChanged(nameof(AntFeedbackTitle));
        OnPropertyChanged(nameof(AntFeedbackAlertMessage));
        OnPropertyChanged(nameof(AntFeedbackAlertDescription));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        OnPropertyChanged(nameof(SegmentedNote));
    }
}
