namespace MusicVault.App.Localization;

/// <summary>设计系统预览页文案（zh-CN / en-US）。</summary>
internal static class DesignSystemLocalizationResources
{
    public static readonly IReadOnlyDictionary<string, string> ZhCn = new Dictionary<string, string>
    {
        ["DesignSystem.Preview.Title"] = "设计系统预览",
        ["DesignSystem.PageTitle"] = "设计系统预览",
        ["DesignSystem.AccentLabelFormat"] = "当前强调色：{0}（在偏好中改色并保存后刷新本页）",
        ["DesignSystem.ThemeLabelFormat"] = "当前主题偏好：{0}",
        ["DesignSystem.DocNote"] =
            "Ant 结构规范见仓库 docs/DESIGN-SYSTEM-ANT.md。阶段 0–6 已完成（含 Badge / Popover / 发布后扫尾）。",

        ["DesignSystem.Feedback.Title"] = "反馈分层（何时用什么）",
        ["DesignSystem.Feedback.Intro"] = "Ant Design 阶段 0–6 已全部完成；以下为各反馈组件预览。",
        ["DesignSystem.Feedback.SceneTip"] = "按场景选一种反馈，避免 Alert 代替 Message、全屏 Modal 代替 Tooltip。",
        ["DesignSystem.Feedback.MessageDesc"] = "顶栏轻提示 · 保存成功、入队完成（不打断操作）",
        ["DesignSystem.Feedback.AlertDesc"] = "页内说明 · 发现合规、设置警告（持续可见）",
        ["DesignSystem.Feedback.EmptyDesc"] = "列表无数据 · EmptyStateView",
        ["DesignSystem.Feedback.ModalDesc"] = "居中卡片 · 确认/预览；阶段 1 起部分危险操作用 Popconfirm",
        ["DesignSystem.Feedback.DrawerDesc"] = "右侧滑层 · 歌词/元数据/队列；Form Drawer 大表单（歌词编辑/标签搜索/平台登录，560px）",
        ["DesignSystem.Feedback.TooltipDesc"] = "悬停说明 · 深色小浮层",
        ["DesignSystem.Feedback.ProgressDesc"] = "已知进度 · 下载/扫描/任务弹窗",
        ["DesignSystem.Feedback.ResultDesc"] = "流程结束摘要 · ResultView（刮削/更新）",
        ["DesignSystem.Feedback.SkeletonDesc"] = "列表首屏占位 · SkeletonBlock / 列表骨架",
        ["DesignSystem.Feedback.PopconfirmDesc"] = "窗口居中 · 短确认浮层（删除/清空/移除曲目）",

        ["DesignSystem.SemanticColors.Title"] = "语义状态色（四套）",
        ["DesignSystem.SemanticColors.Intro"] = "Tag / Alert / Progress 共用；Info 用 Accent 系，Error 用 DangerBrush。",
        ["DesignSystem.SemanticColors.InfoCaption"] = "AccentBrush · 提示/进行中",
        ["DesignSystem.SemanticColors.SuccessCaption"] = "SuccessBrush · 完成/成功",
        ["DesignSystem.SemanticColors.WarningCaption"] = "WarningSoft · 需注意",
        ["DesignSystem.SemanticColors.ErrorCaption"] = "DangerBrush · 失败/危险",

        ["DesignSystem.Motion.Title"] = "动效 Token",
        ["DesignSystem.Motion.Intro"] = "资源键见 Styles/Tokens/Motion.axaml。Transform 位移必须用 px（如 Drawer 560px）。",
        ["DesignSystem.Motion.FastLine"] = "MotionDurationFast = 150ms（Tooltip）",
        ["DesignSystem.Motion.BaseLine"] = "MotionDurationBase = 250ms（Drawer / 蒙层）",
        ["DesignSystem.Motion.DrawerLine"] = "DrawerSlideOffsetPx = 560 · DrawerWidthDefault = 360",

        ["DesignSystem.NotAdapt.Title"] = "明确不适配（保持现状）",
        ["DesignSystem.NotAdapt.Message"] = "不改为 Ant 模式",
        ["DesignSystem.NotAdapt.Description"] =
            "均衡器＝居中 Modal；播放条＝可拖动 seek；音量＝Flyout；偏好＝左侧 ListBox；无右下角 Notification 堆叠。",

        ["DesignSystem.Palette.Title"] = "色板",
        ["DesignSystem.Palette.AccentSoftSample"] = "浅底正文",
        ["DesignSystem.Palette.OnAccentSoftLabel"] = "OnAccentSoft 字",
        ["DesignSystem.Palette.StatPurpleFixed"] = "StatPurple（固定）",
        ["DesignSystem.Palette.StatOrangeFixed"] = "StatOrange（固定）",
        ["DesignSystem.Palette.StatPinkFixed"] = "StatPink（固定）",
        ["DesignSystem.Palette.WarningSample"] = "警告提示示例：请先在偏好中配置 API 地址。",

        ["DesignSystem.Typography.Title"] = "排版",
        ["DesignSystem.Typography.MicroCaption"] = "micro 10px（音质标签）",
        ["DesignSystem.Typography.LeadCaption"] = "lead 16px（全屏艺术家等）",

        ["DesignSystem.Icons.Title"] = "图标体系（Segoe Fluent Icons）",
        ["DesignSystem.Icons.Intro"] =
            "使用 class icon-sm / icon-md + icon-muted|icon-accent；字体来自 IconFontFamily，无需再写 FontFamily。",
        ["DesignSystem.Icons.Sizes"] = "xs 12 · sm 16 · md 20 · lg 24 · 2xl 36 · hero 72（WinUI 常用 16/20）",

        ["DesignSystem.DefaultControls.Title"] = "默认控件（TextBox / ComboBox / CheckBox）",
        ["DesignSystem.DefaultControls.Intro"] = "未加 discover-field 等 class 时即为此外观：描边、4px 圆角、32px 高。",
        ["DesignSystem.DefaultControls.Button"] = "默认 Button（无 class）",
        ["DesignSystem.DefaultControls.TextBoxWatermark"] = "默认 TextBox",
        ["DesignSystem.DefaultControls.ComboBox"] = "默认 ComboBox",
        ["DesignSystem.DefaultControls.ComboBoxOptionB"] = "选项 B",
        ["DesignSystem.DefaultControls.CheckBox"] = "默认 CheckBox",

        ["DesignSystem.Spacing.Title"] = "间距与控件高度",
        ["DesignSystem.Spacing.ControlHeightTokens"] =
            "ControlHeightSm=28 · ControlHeightMd=32（WinUI 标准） · ControlHeightLg=40",
        ["DesignSystem.Spacing.ControlHeightLegend"] = "Sm / Md / Lg 控件高度示意",
        ["DesignSystem.Search.ClassCaption"] =
            "top-search.browse-search + browse-search + browse-filter（等高 ToolbarButtonHeight 28px）",

        ["DesignSystem.Buttons.Title"] = "按钮",
        ["DesignSystem.Buttons.PrimaryCaption"] = "大按钮 primary/secondary 用于全宽主操作；工具栏内勿直接用裸 primary/flat-btn。",
        ["DesignSystem.Buttons.ToolbarCaption"] =
            "工具栏标准：高 ToolbarButtonHeight(30px)，随「界面字号」档位缩放。主色 hover 用 AccentHoverBrush（随强调色变）。",
        ["DesignSystem.Buttons.IconCaption"] =
            "图标按钮三档：row-action-btn 28 / icon-toolbar-btn 22 / top-icon-btn 32。hover 统一 ButtonIconHoverBrush，深浅主题自动切换。",
        ["DesignSystem.Buttons.DialogCaption"] = "app-dialog-btn 高 32px，仅用于弹窗底部。",
        ["DesignSystem.Buttons.PrimaryToolbar"] = "主色工具栏",
        ["DesignSystem.Buttons.PrimaryDialog"] = "primary 对话框",

        ["DesignSystem.Search.Title"] = "搜索框 + 搜索按钮",
        ["DesignSystem.Search.Watermark"] = "曲库 / 传输页搜索…",
        ["DesignSystem.Search.Button"] = "搜索",

        ["DesignSystem.Form.Title"] = "表单",
        ["DesignSystem.Form.RadioLabel"] = "单选",
        ["DesignSystem.Form.CheckLabel"] = "复选",
        ["DesignSystem.Form.InputLabel"] = "输入",
        ["DesignSystem.Form.SelectLabel"] = "下拉",
        ["DesignSystem.Form.OptionA"] = "选项 A",
        ["DesignSystem.Form.OptionB"] = "选项 B",
        ["DesignSystem.Form.AutoCoverLyrics"] = "自动获取封面与歌词",
        ["DesignSystem.Form.InputWatermark"] = "未聚焦为灰字",

        ["DesignSystem.Popconfirm.Title"] = "Popconfirm（窗口居中）",
        ["DesignSystem.Popconfirm.Intro"] =
            "短确认浮层：在应用主窗口客户区居中显示；Esc / 点击外部关闭；危险操作用 ant-btn-danger。",
        ["DesignSystem.Popconfirm.DeleteTitle"] = "删除此项？",
        ["DesignSystem.Popconfirm.DeleteDescription"] = "此操作不可撤销。",
        ["DesignSystem.Popconfirm.DeleteConfirm"] = "删除",
        ["DesignSystem.Popconfirm.DeleteButton"] = "危险删除",
        ["DesignSystem.Popconfirm.ClearTitle"] = "清除记录？",
        ["DesignSystem.Popconfirm.ClearDescription"] = "可仅清除列表，或同时删除本地文件。",
        ["DesignSystem.Popconfirm.ClearConfirm"] = "仅清除列表",
        ["DesignSystem.Popconfirm.ClearAlternate"] = "清除并删除文件",
        ["DesignSystem.Popconfirm.ClearButton"] = "双选项清除",

        ["DesignSystem.ModalOverlay.Title"] = "模态浮层（modal-scrim + ant-popover）",
        ["DesignSystem.ModalOverlay.DialogTitle"] = "确认操作",
        ["DesignSystem.ModalOverlay.DialogBody"] = "遮罩 ModalScrimBrush + 居中卡片 app-modal-panel。",
        ["DesignSystem.ModalOverlay.Cancel"] = "取消",
        ["DesignSystem.ModalOverlay.Confirm"] = "确认",

        ["DesignSystem.ListRow.Title"] = "列表行（选中 vs 普通）",
        ["DesignSystem.ListRow.Normal"] = "普通行 · 图标次要色",
        ["DesignSystem.ListRow.Selected"] = "选中行 · 浅强调底 + 高对比正文",

        ["DesignSystem.TooltipSection.Title"] = "Ant Tooltip（全局样式）",
        ["DesignSystem.TooltipSection.Intro"] = "所有 ToolTip.Tip 自动应用深色半透明浮层与白字、4px 圆角；悬停约 150ms 后显示。",
        ["DesignSystem.TooltipSection.HoverButton"] = "悬停查看提示",
        ["DesignSystem.TooltipSection.HoverTip"] = "这是 Ant Design 风格的文字提示，带缩放淡入动画。",
        ["DesignSystem.TooltipSection.IconTip"] = "图标按钮同样使用全局 Tooltip 样式",

        ["DesignSystem.ProgressSection.Title"] = "Ant Progress（线形）",
        ["DesignSystem.ProgressSection.Intro"] =
            "传输行、弹窗任务、曲库扫描等统一使用 ant-progress-line；发现页精简行用 ant-progress-line-small。",
        ["DesignSystem.ProgressSection.DefaultLabel"] = "默认 4px",
        ["DesignSystem.ProgressSection.SmallLabel"] = "Small 2px（发现传输速览）",
        ["DesignSystem.ProgressSection.ModalLabel"] = "Modal 8px + 百分比",
        ["DesignSystem.ProgressSection.ActiveLabel"] = "Active（不确定进度）",
        ["DesignSystem.ProgressSection.StatusLabel"] = "Success / Exception",

        ["DesignSystem.ResultSection.Title"] = "Result（流程结束）",
        ["DesignSystem.ResultSection.Intro"] = "图标 + 标题 + 说明 + 可选扩展区；用于刮削完成、API 更新结果等 AppModal。",
        ["DesignSystem.ResultSection.SuccessTitle"] = "操作成功",
        ["DesignSystem.ResultSection.SuccessDescription"] = "全部曲目已写入所选项目。",
        ["DesignSystem.ResultSection.WarningTitle"] = "部分未完成",
        ["DesignSystem.ResultSection.WarningDescription"] = "可在工具栏将载入范围设为「仅刮削失败」后重试。",
        ["DesignSystem.ResultSection.ErrorTitle"] = "未能完成",
        ["DesignSystem.ResultSection.ErrorDescription"] = "请查看明细并重试。",
        ["DesignSystem.ResultSection.InfoTitle"] = "发现新版本",
        ["DesignSystem.ResultSection.InfoDescription"] = "内置 go-music-api 可更新至最新版本。",
        ["DesignSystem.ResultSection.UpdateNow"] = "立即更新",
        ["DesignSystem.ResultSection.UpdateLater"] = "稍后",

        ["DesignSystem.SkeletonSection.Title"] = "Skeleton（列表首屏占位）",
        ["DesignSystem.SkeletonSection.Intro"] =
            "未知时长的列表/网格加载用骨架屏；已知进度任务仍用 Progress。背景 ContainerBrush，圆角 4px，可选 pulse（ant-skeleton-active）。",
        ["DesignSystem.SkeletonSection.BasicLabel"] = "基础块 / 行",
        ["DesignSystem.SkeletonSection.CompositeLabel"] = "组合占位",
        ["DesignSystem.SkeletonSection.BrowseLabel"] = "浏览列表行",
        ["DesignSystem.SkeletonSection.DiscoverLabel"] = "发现搜索结果",

        ["DesignSystem.FormDrawer.Title"] = "Form Drawer（大表单）",
        ["DesignSystem.FormDrawer.Intro"] =
            "宽/长表单从居中 Modal 迁至右侧 FormDrawerHost（ZIndex 170）：蒙层 + translateX(560px) 滑入；Esc / 蒙层 / 关闭钮退出。入口：元数据「编辑歌词」、在线识别、设置「平台登录」。均衡器仍居中 Modal。",
        ["DesignSystem.FormDrawer.AlertMessage"] = "与侧栏 Drawer 分层",
        ["DesignSystem.FormDrawer.AlertDescription"] =
            "RightSidebar＝歌词/元数据/队列；FormDrawer＝一次性大表单任务，可叠在侧栏之上。",

        ["DesignSystem.Steps.Title"] = "Steps（向导步骤）",
        ["DesignSystem.Steps.Intro"] = "水平步骤条：wait / process / finish / error；批量入队确认弹窗顶部展示。",
        ["DesignSystem.Steps.WriteQueue"] = "写入队列",

        ["DesignSystem.Badge.Title"] = "Badge（角标 / 状态点）",
        ["DesignSystem.Badge.Intro"] =
            "与 Tag / Alert / Progress 共用四套语义色；顶栏传输数字用 ant-badge-count，列表状态列用 ant-badge-status。",
        ["DesignSystem.Badge.CountLabel"] = "Count（数字角标）",
        ["DesignSystem.Badge.TransferLabel"] = "传输",
        ["DesignSystem.Badge.StatusLabel"] = "Status（18×18 状态块）",
        ["DesignSystem.Badge.DotLabel"] = "Dot（6×6 圆点）",

        ["DesignSystem.Popover.Title"] = "Popover（就近浮层）",
        ["DesignSystem.Popover.Intro"] =
            "音量、传输页更多菜单等用 Flyout + ant-popover-flyout；FlyoutPresenter 统一 FlyoutSurfaceBrush / 10px 圆角 / PopoverShadow。居中 Modal 与窗口居中 Popconfirm 分层不变。",
        ["DesignSystem.Popover.Refresh"] = "刷新",
        ["DesignSystem.Popover.ClearOverview"] = "清除速览已完成",
        ["DesignSystem.Popover.ClearAll"] = "清空全部台账…",
        ["DesignSystem.Popover.Hint"] =
            "↑ 传输页「更多」菜单同款面板。音量 Flyout 内容区用 ant-popover-inline（透明内层，外壳由 FlyoutPresenter 提供）。",

        ["DesignSystem.ThemePlayer.Title"] = "主题与播放器 Chrome（批次 A 验收区）",
        ["DesignSystem.ThemePlayer.Intro"] =
            "主流程页面已 Token 化；播放器 chrome 已接入 PlayerChrome*（批次 B）。切换偏好「外观 → 主题 / 强调色」后，对照下表与真实播放栏。",
        ["DesignSystem.ThemePlayer.LightThemeLabel"] = "浅色主题",
        ["DesignSystem.ThemePlayer.LightThemeDesc"] =
            "设置 → 外观 → 浅色：播放栏图标/文字用 Text*Brush；弹窗输入用 InputFieldBrush；精简窗仍用 PlayerChrome* 暗色。",
        ["DesignSystem.ThemePlayer.AuditLabel"] = "审计",
        ["DesignSystem.ThemePlayer.AuditDesc"] = "仓库根目录运行 .\\scripts\\audit-design-system.ps1 → docs/execution/design-system-audit-*.md",
        ["DesignSystem.ThemePlayer.ChromeSample"] = "播放器 Chrome 样例（PlayerChrome* · 批次 B）",
        ["DesignSystem.ThemePlayer.ChromeControls"] = "播放 / 暂停 · 进度条 · 音量",

        ["DesignSystem.AntFeedback.Title"] = "Ant 反馈（Alert / Empty / Segmented）",
        ["DesignSystem.AntFeedback.AlertMessage"] = "平台提示文案",
        ["DesignSystem.AntFeedback.AlertDescription"] = "次要引导说明（发现页同款）",
        ["DesignSystem.AntFeedback.EmptyTitle"] = "空状态标题",
        ["DesignSystem.AntFeedback.EmptyDescription"] = "用于曲库、传输、歌单等列表无数据时。",
        ["DesignSystem.AntFeedback.SegmentedNote"] =
            "Segmented 请用 IntSegmentedBar（代码切换 selected，避免 XAML 绑定导致整页空白）。",
    };

    public static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>
    {
        ["DesignSystem.Preview.Title"] = "Design system preview",
        ["DesignSystem.PageTitle"] = "Design system preview",
        ["DesignSystem.AccentLabelFormat"] = "Current accent: {0} (change in Preferences, save, then refresh this page)",
        ["DesignSystem.ThemeLabelFormat"] = "Current theme preference: {0}",
        ["DesignSystem.DocNote"] =
            "Ant structure spec: docs/DESIGN-SYSTEM-ANT.md in the repo. Phases 0–6 complete (including Badge / Popover / post-release cleanup).",

        ["DesignSystem.Feedback.Title"] = "Feedback layers (when to use what)",
        ["DesignSystem.Feedback.Intro"] = "Ant Design phases 0–6 are complete; component previews below.",
        ["DesignSystem.Feedback.SceneTip"] =
            "Pick one feedback type per scenario—avoid Alert for Message, or full-screen Modal for Tooltip.",
        ["DesignSystem.Feedback.MessageDesc"] = "Top bar toast · save success, enqueue done (non-blocking)",
        ["DesignSystem.Feedback.AlertDesc"] = "In-page notice · discover compliance, settings warnings (persistent)",
        ["DesignSystem.Feedback.EmptyDesc"] = "Empty list · EmptyStateView",
        ["DesignSystem.Feedback.ModalDesc"] = "Centered card · confirm/preview; phase 1+ uses Popconfirm for some destructive actions",
        ["DesignSystem.Feedback.DrawerDesc"] =
            "Right slide-over · lyrics/metadata/queue; Form Drawer for large forms (lyrics edit / tag search / platform login, 560px)",
        ["DesignSystem.Feedback.TooltipDesc"] = "Hover hint · dark compact overlay",
        ["DesignSystem.Feedback.ProgressDesc"] = "Known progress · download/scan/task dialogs",
        ["DesignSystem.Feedback.ResultDesc"] = "Flow summary · ResultView (scrape/update)",
        ["DesignSystem.Feedback.SkeletonDesc"] = "List first paint · SkeletonBlock / list skeleton",
        ["DesignSystem.Feedback.PopconfirmDesc"] = "Window-centered · short confirm overlay (delete/clear/remove track)",

        ["DesignSystem.SemanticColors.Title"] = "Semantic status colors (four sets)",
        ["DesignSystem.SemanticColors.Intro"] = "Shared by Tag / Alert / Progress; Info uses Accent, Error uses DangerBrush.",
        ["DesignSystem.SemanticColors.InfoCaption"] = "AccentBrush · info / in progress",
        ["DesignSystem.SemanticColors.SuccessCaption"] = "SuccessBrush · done / success",
        ["DesignSystem.SemanticColors.WarningCaption"] = "WarningSoft · needs attention",
        ["DesignSystem.SemanticColors.ErrorCaption"] = "DangerBrush · failure / danger",

        ["DesignSystem.Motion.Title"] = "Motion tokens",
        ["DesignSystem.Motion.Intro"] =
            "Resource keys in Styles/Tokens/Motion.axaml. Transform offsets must use px (e.g. Drawer 560px).",
        ["DesignSystem.Motion.FastLine"] = "MotionDurationFast = 150ms (Tooltip)",
        ["DesignSystem.Motion.BaseLine"] = "MotionDurationBase = 250ms (Drawer / scrim)",
        ["DesignSystem.Motion.DrawerLine"] = "DrawerSlideOffsetPx = 560 · DrawerWidthDefault = 360",

        ["DesignSystem.NotAdapt.Title"] = "Explicitly not adapted (keep as-is)",
        ["DesignSystem.NotAdapt.Message"] = "Not switching to Ant patterns",
        ["DesignSystem.NotAdapt.Description"] =
            "Equalizer = centered Modal; player bar = draggable seek; volume = Flyout; preferences = left ListBox; no bottom-right Notification stack.",

        ["DesignSystem.Palette.Title"] = "Color palette",
        ["DesignSystem.Palette.AccentSoftSample"] = "Soft background text",
        ["DesignSystem.Palette.OnAccentSoftLabel"] = "OnAccentSoft text",
        ["DesignSystem.Palette.StatPurpleFixed"] = "StatPurple (fixed)",
        ["DesignSystem.Palette.StatOrangeFixed"] = "StatOrange (fixed)",
        ["DesignSystem.Palette.StatPinkFixed"] = "StatPink (fixed)",
        ["DesignSystem.Palette.WarningSample"] = "Warning example: configure the API address in Preferences first.",

        ["DesignSystem.Typography.Title"] = "Typography",
        ["DesignSystem.Typography.MicroCaption"] = "micro 10px (quality tag)",
        ["DesignSystem.Typography.LeadCaption"] = "lead 16px (fullscreen artist, etc.)",

        ["DesignSystem.Icons.Title"] = "Icon system (Segoe Fluent Icons)",
        ["DesignSystem.Icons.Intro"] =
            "Use class icon-sm / icon-md + icon-muted|icon-accent; font from IconFontFamily—no need to set FontFamily.",
        ["DesignSystem.Icons.Sizes"] = "xs 12 · sm 16 · md 20 · lg 24 · 2xl 36 · hero 72 (WinUI common 16/20)",

        ["DesignSystem.DefaultControls.Title"] = "Default controls (TextBox / ComboBox / CheckBox)",
        ["DesignSystem.DefaultControls.Intro"] =
            "Appearance without discover-field etc.: border, 4px radius, 32px height.",
        ["DesignSystem.DefaultControls.Button"] = "Default Button (no class)",
        ["DesignSystem.DefaultControls.TextBoxWatermark"] = "Default TextBox",
        ["DesignSystem.DefaultControls.ComboBox"] = "Default ComboBox",
        ["DesignSystem.DefaultControls.ComboBoxOptionB"] = "Option B",
        ["DesignSystem.DefaultControls.CheckBox"] = "Default CheckBox",

        ["DesignSystem.Spacing.Title"] = "Spacing & control height",
        ["DesignSystem.Spacing.ControlHeightTokens"] =
            "ControlHeightSm=28 · ControlHeightMd=32 (WinUI standard) · ControlHeightLg=40",
        ["DesignSystem.Spacing.ControlHeightLegend"] = "Sm / Md / Lg control height preview",
        ["DesignSystem.Search.ClassCaption"] =
            "top-search.browse-search + browse-search + browse-filter (same height ToolbarButtonHeight 28px)",

        ["DesignSystem.Buttons.Title"] = "Buttons",
        ["DesignSystem.Buttons.PrimaryCaption"] =
            "Large primary/secondary for full-width primary actions; avoid bare primary/flat-btn in toolbars.",
        ["DesignSystem.Buttons.ToolbarCaption"] =
            "Toolbar standard: ToolbarButtonHeight (30px), scales with UI font size. Primary hover uses AccentHoverBrush (follows accent).",
        ["DesignSystem.Buttons.IconCaption"] =
            "Icon buttons: row-action-btn 28 / icon-toolbar-btn 22 / top-icon-btn 32. Hover uses ButtonIconHoverBrush; auto light/dark.",
        ["DesignSystem.Buttons.DialogCaption"] = "app-dialog-btn is 32px tall—dialog footers only.",
        ["DesignSystem.Buttons.PrimaryToolbar"] = "Primary toolbar",
        ["DesignSystem.Buttons.PrimaryDialog"] = "primary dialog",

        ["DesignSystem.Search.Title"] = "Search box + search button",
        ["DesignSystem.Search.Watermark"] = "Library / transfers search…",
        ["DesignSystem.Search.Button"] = "Search",

        ["DesignSystem.Form.Title"] = "Form",
        ["DesignSystem.Form.RadioLabel"] = "Radio",
        ["DesignSystem.Form.CheckLabel"] = "Checkbox",
        ["DesignSystem.Form.InputLabel"] = "Input",
        ["DesignSystem.Form.SelectLabel"] = "Select",
        ["DesignSystem.Form.OptionA"] = "Option A",
        ["DesignSystem.Form.OptionB"] = "Option B",
        ["DesignSystem.Form.AutoCoverLyrics"] = "Auto-fetch cover and lyrics",
        ["DesignSystem.Form.InputWatermark"] = "Placeholder when unfocused",

        ["DesignSystem.Popconfirm.Title"] = "Popconfirm (window-centered)",
        ["DesignSystem.Popconfirm.Intro"] =
            "Short confirm overlay centered in the main window client area; Esc / outside click closes; destructive actions use ant-btn-danger.",
        ["DesignSystem.Popconfirm.DeleteTitle"] = "Delete this item?",
        ["DesignSystem.Popconfirm.DeleteDescription"] = "This action cannot be undone.",
        ["DesignSystem.Popconfirm.DeleteConfirm"] = "Delete",
        ["DesignSystem.Popconfirm.DeleteButton"] = "Danger delete",
        ["DesignSystem.Popconfirm.ClearTitle"] = "Clear records?",
        ["DesignSystem.Popconfirm.ClearDescription"] = "Clear the list only, or also delete local files.",
        ["DesignSystem.Popconfirm.ClearConfirm"] = "Clear list only",
        ["DesignSystem.Popconfirm.ClearAlternate"] = "Clear and delete files",
        ["DesignSystem.Popconfirm.ClearButton"] = "Dual-option clear",

        ["DesignSystem.ModalOverlay.Title"] = "Modal overlay (modal-scrim + ant-popover)",
        ["DesignSystem.ModalOverlay.DialogTitle"] = "Confirm action",
        ["DesignSystem.ModalOverlay.DialogBody"] = "Scrim ModalScrimBrush + centered card app-modal-panel.",
        ["DesignSystem.ModalOverlay.Cancel"] = "Cancel",
        ["DesignSystem.ModalOverlay.Confirm"] = "Confirm",

        ["DesignSystem.ListRow.Title"] = "List row (selected vs normal)",
        ["DesignSystem.ListRow.Normal"] = "Normal row · secondary icon color",
        ["DesignSystem.ListRow.Selected"] = "Selected row · soft accent background + high-contrast text",

        ["DesignSystem.TooltipSection.Title"] = "Ant Tooltip (global style)",
        ["DesignSystem.TooltipSection.Intro"] =
            "All ToolTip.Tip uses dark semi-transparent overlay with white text, 4px radius; ~150ms hover delay.",
        ["DesignSystem.TooltipSection.HoverButton"] = "Hover for tooltip",
        ["DesignSystem.TooltipSection.HoverTip"] = "Ant Design–style text tooltip with scale/fade animation.",
        ["DesignSystem.TooltipSection.IconTip"] = "Icon buttons use the same global Tooltip style",

        ["DesignSystem.ProgressSection.Title"] = "Ant Progress (linear)",
        ["DesignSystem.ProgressSection.Intro"] =
            "Transfers, task dialogs, library scan use ant-progress-line; discover compact rows use ant-progress-line-small.",
        ["DesignSystem.ProgressSection.DefaultLabel"] = "Default 4px",
        ["DesignSystem.ProgressSection.SmallLabel"] = "Small 2px (discover transfer overview)",
        ["DesignSystem.ProgressSection.ModalLabel"] = "Modal 8px + percent",
        ["DesignSystem.ProgressSection.ActiveLabel"] = "Active (indeterminate)",
        ["DesignSystem.ProgressSection.StatusLabel"] = "Success / Exception",

        ["DesignSystem.ResultSection.Title"] = "Result (flow complete)",
        ["DesignSystem.ResultSection.Intro"] =
            "Icon + title + description + optional extra; used in scrape complete, API update result AppModals.",
        ["DesignSystem.ResultSection.SuccessTitle"] = "Operation succeeded",
        ["DesignSystem.ResultSection.SuccessDescription"] = "All tracks were written to the selected project.",
        ["DesignSystem.ResultSection.WarningTitle"] = "Partially incomplete",
        ["DesignSystem.ResultSection.WarningDescription"] =
            "Set load scope to “failed scrape only” on the toolbar and retry.",
        ["DesignSystem.ResultSection.ErrorTitle"] = "Could not complete",
        ["DesignSystem.ResultSection.ErrorDescription"] = "See details and retry.",
        ["DesignSystem.ResultSection.InfoTitle"] = "New version available",
        ["DesignSystem.ResultSection.InfoDescription"] = "Bundled go-music-api can be updated to the latest version.",
        ["DesignSystem.ResultSection.UpdateNow"] = "Update now",
        ["DesignSystem.ResultSection.UpdateLater"] = "Later",

        ["DesignSystem.SkeletonSection.Title"] = "Skeleton (list first paint)",
        ["DesignSystem.SkeletonSection.Intro"] =
            "Unknown-duration list/grid loads use skeleton; known progress still uses Progress. Background ContainerBrush, 4px radius, optional pulse (ant-skeleton-active).",
        ["DesignSystem.SkeletonSection.BasicLabel"] = "Basic block / line",
        ["DesignSystem.SkeletonSection.CompositeLabel"] = "Composite placeholder",
        ["DesignSystem.SkeletonSection.BrowseLabel"] = "Browse list row",
        ["DesignSystem.SkeletonSection.DiscoverLabel"] = "Discover search results",

        ["DesignSystem.FormDrawer.Title"] = "Form Drawer (large form)",
        ["DesignSystem.FormDrawer.Intro"] =
            "Wide/long forms moved from centered Modal to right FormDrawerHost (ZIndex 170): scrim + translateX(560px) slide-in; Esc / scrim / close exits. Entry: metadata “Edit lyrics”, online ID, settings “Platform login”. Equalizer stays centered Modal.",
        ["DesignSystem.FormDrawer.AlertMessage"] = "Layered with sidebar Drawer",
        ["DesignSystem.FormDrawer.AlertDescription"] =
            "RightSidebar = lyrics/metadata/queue; FormDrawer = one-off large form tasks, can stack above sidebar.",

        ["DesignSystem.Steps.Title"] = "Steps (wizard)",
        ["DesignSystem.Steps.Intro"] =
            "Horizontal step bar: wait / process / finish / error; shown atop batch enqueue confirm dialog.",
        ["DesignSystem.Steps.WriteQueue"] = "Write to queue",

        ["DesignSystem.Badge.Title"] = "Badge (count / status dot)",
        ["DesignSystem.Badge.Intro"] =
            "Same four semantic colors as Tag / Alert / Progress; transfer count uses ant-badge-count, list status column uses ant-badge-status.",
        ["DesignSystem.Badge.CountLabel"] = "Count (numeric badge)",
        ["DesignSystem.Badge.TransferLabel"] = "Transfers",
        ["DesignSystem.Badge.StatusLabel"] = "Status (18×18 block)",
        ["DesignSystem.Badge.DotLabel"] = "Dot (6×6)",

        ["DesignSystem.Popover.Title"] = "Popover (anchored overlay)",
        ["DesignSystem.Popover.Intro"] =
            "Volume, transfers “more” menu use Flyout + ant-popover-flyout; FlyoutPresenter uses FlyoutSurfaceBrush / 10px radius / PopoverShadow. Centered Modal vs window-centered Popconfirm layering unchanged.",
        ["DesignSystem.Popover.Refresh"] = "Refresh",
        ["DesignSystem.Popover.ClearOverview"] = "Clear completed from overview",
        ["DesignSystem.Popover.ClearAll"] = "Clear all ledger…",
        ["DesignSystem.Popover.Hint"] =
            "↑ Same panel as transfers “More” menu. Volume Flyout content uses ant-popover-inline (transparent inner; shell from FlyoutPresenter).",

        ["DesignSystem.ThemePlayer.Title"] = "Theme & player chrome (batch A acceptance)",
        ["DesignSystem.ThemePlayer.Intro"] =
            "Main pages are tokenized; player chrome uses PlayerChrome* (batch B). After changing Preferences → Appearance → theme / accent, compare the table below with the real player bar.",
        ["DesignSystem.ThemePlayer.LightThemeLabel"] = "Light theme",
        ["DesignSystem.ThemePlayer.LightThemeDesc"] =
            "Settings → Appearance → Light: player bar icons/text use Text*Brush; dialog inputs use InputFieldBrush; compact window still uses dark PlayerChrome*.",
        ["DesignSystem.ThemePlayer.AuditLabel"] = "Audit",
        ["DesignSystem.ThemePlayer.AuditDesc"] =
            "Run .\\scripts\\audit-design-system.ps1 at repo root → docs/execution/design-system-audit-*.md",
        ["DesignSystem.ThemePlayer.ChromeSample"] = "Player chrome sample (PlayerChrome* · batch B)",
        ["DesignSystem.ThemePlayer.ChromeControls"] = "Play / pause · progress · volume",

        ["DesignSystem.AntFeedback.Title"] = "Ant feedback (Alert / Empty / Segmented)",
        ["DesignSystem.AntFeedback.AlertMessage"] = "Platform notice copy",
        ["DesignSystem.AntFeedback.AlertDescription"] = "Secondary guidance (same as discover page)",
        ["DesignSystem.AntFeedback.EmptyTitle"] = "Empty state title",
        ["DesignSystem.AntFeedback.EmptyDescription"] = "When library, transfers, playlists, etc. have no data.",
        ["DesignSystem.AntFeedback.SegmentedNote"] =
            "For Segmented use IntSegmentedBar (toggle selected in code—XAML binding can blank the whole page).",
    };

    public static bool TryGet(string key, string culture, out string value)
    {
        var dict = string.Equals(culture, LocalizationService.EnUs, StringComparison.OrdinalIgnoreCase)
            ? EnUs
            : ZhCn;
        return dict.TryGetValue(key, out value!);
    }
}
