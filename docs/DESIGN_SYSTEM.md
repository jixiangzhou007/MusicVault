# MusicVault 设计系统（Design System + VI）

本文档是 MusicVault 桌面端 UI 的**单一真相来源**。新页面、新组件必须遵循本规范；改样式时优先改 **Token / 组件 Class**，禁止在页面里零散写字号、颜色、图标尺寸。

实施分 **四个阶段**，每阶段完成后暂停，由负责人确认后再进入下一阶段。

---

## 实施路线图（四阶段 + 停点）

| 阶段 | 目标 | 交付物 | 停点 |
|------|------|--------|------|
| **P0** | 规范定稿 | 本文档 + Token/Class 命名表 | 确认文档与范围 |
| **P1** | 强调色 / VI 可一键换色 | `ThemeService` 扩展、清除硬编码蓝、语义色资源 | 运行应用，设置里换色验证 |
| **P2** | 排版 + 图标统一 | `text-*` / `icon-*` 样式类，优先改元数据/发现/设置/列表行 | 设计预览页可见 |
| **P3** | 组件库拆分与全面统一 | `Styles/` 分文件、`DesignSystemPage` 预览、逐页迁移 | 全应用目测一致 |
| **P4** | 组件样式 Token 化 | `Styles/Components/*` 字号/图标尺寸全部 `{DynamicResource}` | 改 Token 后组件层联动 |
| **S1** | 系统补全 | `Spacing.axaml`、语义色 Token、class 别名、`text-lead`、预览页扩展 | 预览页可验新 Token |
| **S2** | 组件样式颜色 Token 化 | `Styles/Components/*` 清除 `#` 魔法色 | 切换主题组件层一致 |
| **S3** | View 层迁移 | 清除 View `#` 色、Fluent Icons、`app-dialog` | 深/浅主题全页一致 |
| **S4** | 按钮与排版扫尾 | 各页 `flat-btn`/`primary`/`danger`、`text-subtitle` | 无 inline 按钮样式 |
| **S5** | 间距 Token 化 | `FieldContentMargin`、`PageContentMargin`、元数据缩进 class | 布局 magic number 清除 |

**当前进度：S5 + 批次 A–D 已完成；UI 优化 U1–U5 已完成；工程优化 V1–V5 已完成。**

---

## 13. 豁免与固定色（批次 A）

以下区域**允许**硬编码色或固定 chrome，不视为 View/组件层违规；新增豁免须在本节登记。

| 区域 | 路径 | 原因 | 计划 |
|------|------|------|------|
| 设计预览演示块 | `Views/Pages/DesignSystemPageView.axaml` | 演示 Success/Error 色块，非生产页面 | 保留或改 Token 演示 |
| 语义 Result 变体 | `Styles/Components/Result.axaml` | Success/Error 浅底块 | **U3 已完成**：`SuccessSoftBrush` / `ErrorSoftBrush` |
| Ant Alert 语义底 | `Styles/Components/Feedback.axaml` | Info/Warning/Error 浅底块 | **U3 已完成**：`InfoSoftBrush` / `ErrorSoftBrush` 等 |
| Steps 状态色 | `Styles/Components/Steps.axaml` | Finish/Error 圆点固定色 | **U3 已完成**：语义 Soft Token |
| 危险/警告按钮 | `Styles/Components/Button.axaml`（`#FA8C16`） | Ant 警告橙，固定不随 Accent | 保留或抽 `WarningSolidBrush` |
| 主题 Token 定义 | `ThemeResources*.axaml`、`AccentPalette.cs` | Token 源，非页面魔法色 | 不适用 |
| Fluent 默认 Accent | `App.axaml` `ColorPaletteResources` | 框架启动默认 | 不适用 |

**审计**：运行 ``.\scripts\audit-design-system.ps1``，报告输出至 ``docs/execution/design-system-audit-*.md``。发布/CI 使用 ``-FailOnViolation``（非豁免区 hardcoded `#` + View 字面量 `Margin` 违规即失败）。

**相关文档**：Ant 结构迁移见 [DESIGN-SYSTEM-ANT.md](./DESIGN-SYSTEM-ANT.md)（附录，主规范以本文为准）。

---

## 1. 设计原则

1. **语义优先**：页面只引用 `AccentBrush`、`TextSecondaryBrush`，不写 `#2D7DFF`。
2. **一处改、全局变**：强调色（蓝/橙/紫/红…）只改设置里的 **Accent**，所有交互强调、选中图标、链接、聚焦框同步更新。
3. **组件 class 复用**：同一类控件同一套 class（如列表行右下角图标均为 `row-action-btn` + `icon-sm`）。
4. **专业默认 + 可配置**：默认沿用当前蓝色 VI；偏好设置可改 Accent、字号档位（P2 后接入）。
5. **可见可验**：提供「设计系统预览」页，避免在设置页等低频页面改完「肉眼看不出来」。

---

## 2. 三层架构

```
用户设置（Accent、主题深浅、字号档位）
        ↓
ThemeService → 写入 Application.Resources（语义 Token）
        ↓
组件样式（AppStyles：btn-* / field-* / text-* / icon-*）
        ↓
页面 XAML（仅布局 + Classes，无魔法数颜色）
```

---

## 3. 颜色体系（VI / 强调色）

### 3.1 中性色（不随 Accent 变化）

| Token | 用途 |
|-------|------|
| `BackgroundBrush` | 窗口底 |
| `SurfaceBrush` / `SurfaceElevatedBrush` | 卡片、面板 |
| `TextPrimaryBrush` | 主文字 |
| `TextSecondaryBrush` | 次要文字、未聚焦输入 |
| `TextTertiaryBrush` | 辅助、表头、占位 |
| `BorderSubtleBrush` / `SeparatorBrush` | 分割线、边框 |
| `SearchFieldBrush` | 输入框背景 |
| `DangerBrush` | 删除、错误（固定红） |
| `FavoriteBrush` | 收藏爱心（固定红，与 `DangerBrush` 同色，不随 Accent 变） |
| `InputFieldBrush` | 模态弹窗内输入框底（与 `SurfaceElevatedBrush` 区分） |
| `ModalScrimBrush` | 应用内模态遮罩（约 70% 黑） |
| `FullscreenScrimBrush` | 全屏正在播放背景遮罩 |
| `WarningSoftBrush` / `WarningTextBrush` / `WarningBorderBrush` | 警告提示块（关于页等） |
| `CoverPlaceholderBrush` | 专辑/封面占位（与 `PlayerCoverPlaceholderBrush` 同色） |
| `StatPinkBrush` | 歌词徽章等固定装饰粉 |
| `SuccessBrush` | 下载成功态（固定绿） |
| `OverlayHoverLightBrush` / `OverlaySelectedLightBrush` | 顶栏/浏览条半透明 hover、选中底 |
| `OverlayChipHoverBrush` / `OverlayProgressTrackBrush` | 发现页 chip hover、下载进度轨 |
| `FlyoutSurfaceBrush` / `FlyoutBorderBrush` | 音量等 Flyout 面板 |
| `SliderThumbFillBrush` | 滑块拇指填充 |
| `FullscreenControlHoverBrush` | 全屏控件 hover 底 |
| `PlayerChromeBaseBrush` / `PlayerChromeCoverBrush` | 精简播放器根底、封面区（深浅主题均为暗色） |
| `PlayerChromeForeground*` / `PlayerChromeBarIconBrush` | 精简窗与桌面歌词上的白字层级 |
| `PlayerChromeHeroScrimBrush` / `PlayerChromeCoverFadeBrush` | 封面底部渐变遮罩 |
| `PopoverShadow` | 悬浮卡片阴影（`BoxShadows`） |

### 3.2 强调色族（随 Accent 设置变化）— P1 实现

| Token | 用途 |
|-------|------|
| `AccentBrush` | 主按钮底、进度条、**选中态图标**、链接 |
| `AccentSoftBrush` | 选中行背景、浅强调底 |
| `OnAccentSoftForegroundBrush` | 浅强调底上的正文（自动高对比，避免蓝底蓝字） |
| `AccentMutedBrush` | Hover 边框、弱高亮（替代 `#402D7DFF`） |
| `AccentForegroundBrush` | 放在 Accent 实心上的字/图标（自动对比色） |
| `FocusRingBrush` | 输入框、下拉聚焦描边（1px） |
| `LinkBrush` | 纯文字链接、A+/A-、返回箭头 |
| `NavSelectedBrush` / `NavSelectedBgBrush` | 侧栏导航选中 |
| `PlayerProgressFillBrush` | 播放进度 |
| `StatAccentBrush` | 统计强调（原 `StatBlueBrush`，改名随 Accent） |

**默认 Accent**：`#007AFF`（经典蓝）。设置页为 7 个预设色块；旧配置中的 `#2D7DFF` 会自动归并到 `#007AFF`。

**不随 Accent 变的装饰色**（数据分类用）：`StatPurpleBrush`、`StatOrangeBrush` 等可保留。

### 3.3 设计师高亮建议（P1/P2 落地时参考）

- 列表**当前播放行**：`AccentSoftBrush` 底 + 标题 `TextPrimary`，行内图标 `IconAccentBrush`。
- **可点击图标**：默认 `IconSecondaryBrush`，Hover `IconPrimaryBrush`，选中/激活 `IconAccentBrush`。
- **主操作**：`btn-primary` 实心 Accent；次要操作用 `btn-ghost`。
- 发现页/下载卡片 Hover：`AccentMutedBrush` 边框，避免写死半透明蓝。

### 3.4 浅色主题搭配原则

| 区域 | 浅色 Token | 说明 |
|------|------------|------|
| 页面/卡片 | `BackgroundBrush` / `SurfaceBrush` / `ContainerBrush` | 灰底 + 白卡片，分割线用半透明黑 `SeparatorBrush` |
| 播放栏（主窗口底栏） | `PlayerBarBrush` + `TextPrimaryBrush` / `TextSecondaryBrush` | 与页面一致走语义字色，**不用** `PlayerChromeForeground*` |
| 弹窗输入 | `InputFieldBrush`（浅色 `#FFFFFF`）+ `ControlStrokeDefaultBrush` 描边 | 禁止复用深色 `#1A1A1E` |
| 列表选中 | `NavSelectedBgBrush` + `OnAccentSoftForegroundBrush` | 强调色浅底 + 深色字，避免蓝底蓝字 |
| 精简播放器 / 桌面歌词 | `PlayerChrome*`（固定暗色） | 独立沉浸窗；hover 用 `PlayerChromeHoverBrush`，不用 `NavHoverBrush` |

切换浅色后请在「设计系统预览」对照 `PlayerBarBrush`、`InputFieldBrush` 与真实播放栏。

---

## 4. 字体与排版

### 4.1 字体栈

| Token | 值 |
|-------|-----|
| `AppFontFamily` | Inter, Segoe UI Variable Text, Microsoft YaHei UI, PingFang SC, sans-serif |
| `MonoFontFamily` | Inter, Cascadia Mono, Consolas, monospace（时长、码率） |

中文、英文、数字共用 `AppFontFamily`，保证数字对齐与混排一致。

### 4.2 字号 Token（已有，将强制使用）

| Token | 默认 px | 语义 class（P2） |
|-------|---------|------------------|
| `FontSizeMicro` | 10 | `text-micro`（下载音质标签等） |
| `FontSizeCaption` | 11 | `text-caption` |
| `FontSizeLabel` | 12 | `text-label`（表单左标签、元数据「数据源」） |
| `FontSizeBody` | 13 | `text-body` |
| `FontSizeTitle3` | 15 | `text-subtitle` |
| `FontSizeLead` | 16 | `text-lead`（全屏艺术家等次级标题） |
| `FontSizeTitle2` | 22 | `text-title` |
| `FontSizeLargeTitle` | 28 | `text-display`（全屏歌名） |
| `FontSizeStatValue` | 34 | `stat-value`（统计卡片大数字） |

### 4.3 偏好设置中的字号（规划）

设置页增加「界面字号：标准 / 较大」档位，映射为 Token 整体 +1～2px（P2 末或 P3），无需每页改。

---

## 5. 图标系统（Icon System）

### 5.1 图标来源

- **主图标字体**：`Segoe Fluent Icons`（Fluent 符号，与 Windows 11 一致）。
- **禁止**同一功能混用 Emoji、PNG、多套字体（除非品牌 Logo）。

### 5.2 图标尺寸 Token

| Token | px | class | 典型场景 |
|-------|-----|-------|----------|
| `IconSizeXs` | 12 | `icon-xs` | 极小提示 |
| `IconSizeSm` | 14 | `icon-sm` | 顶栏、下载行操作 |
| `IconSizeMd` | 16 | `icon-md` | 侧栏关闭、列表行操作 |
| `IconSizeLg` | 20 | `icon-lg` | 全屏返回箭头 |
| `IconSizeXl` | 24 | `icon-xl` | 空状态插图级 |
| `IconSize2xl` | 36 | `icon-2xl` | 艺术家头像占位 |
| `IconSizeHero` | 72 | `icon-hero` | 无封面占位 |

**规则**：`TextBlock` 使用图标时必带 `Classes="icon-sm"` 等，**禁止**随手写 `FontSize="13"`。

### 5.3 图标颜色 Token（随 Accent 变化）

| Token | 用途 |
|-------|------|
| `IconPrimaryBrush` | 默认图标（=`TextPrimaryBrush`） |
| `IconSecondaryBrush` | 次要、未选中（=`TextSecondaryBrush`） |
| `IconTertiaryBrush` | 禁用、占位 |
| `IconAccentBrush` | **选中、激活、收藏亮起**（=`AccentBrush`） |
| `IconOnAccentBrush` | 铺在 Accent 按钮上的图标（=`AccentForegroundBrush`） |

P1 在 `ApplyAccentColor` 时同步更新 `IconAccentBrush` / `LinkBrush`。

### 5.4 图标按钮组件（与现有 class 对齐）

| 现有 class | 目标统一名 | 说明 |
|------------|------------|------|
| `row-action-btn` | `btn-icon-row` | 曲目列表右下：收藏、加歌单，14px 图标 |
| `download-icon-btn` | `btn-icon-row` | 下载/发现记录行 |
| `icon-btn` | `btn-icon-toolbar` | 侧栏 ✕、⇄，透明底 |
| `top-icon-btn` | `btn-icon-toolbar` | 主窗口顶栏 |
| `player-cover-btn` | 独立 | 封面点击区域，非标准图标按钮 |
| 全屏返回 | `btn-icon-ghost` + `icon-lg` + `IconAccentBrush` | 无蓝框，仅箭头 |

列表行选中时图标高亮规则（已有样式方向，P2 收拢）：

- 未选中行：图标 `IconSecondaryBrush`
- 选中行：glyph `IconAccentBrush`

### 5.5 主题与字号联动

- **强调色**：`IconAccentBrush` / `AccentHoverBrush` 由 `AccentPalette.ApplyTo` 写入，换色后图标高亮与主按钮 hover 同步更新。
- **深浅主题**：`ButtonIconHoverBrush` / `ButtonGhostHoverBrush` 在 `ThemeResources` / `ThemeResources.Light` 各有一套值，切换主题自动生效。
- **界面字号**：`TypographyScale.Apply` 同时缩放 `IconSize*`、`ToolbarButtonHeight`、`RowActionButtonSize`、`IconToolbarButtonSize`、`IconButtonSizeMd`，长文案按钮高度随档位增大（国际化友好）。

### 5.6 图标按钮尺寸（`IconButton.axaml`）

| class | Token | 默认 px | 场景 |
|-------|-------|---------|------|
| `row-action-btn` / `btn-icon-row` | `RowActionButtonSize` | 28 | 曲目行收藏/下载 |
| `icon-toolbar-btn` 等 | `IconToolbarButtonSize` | 22 | 侧栏 +/刷新 |
| `icon-btn` / `top-icon-btn` | `IconButtonSizeMd` | 32 | 顶栏、侧栏关闭 |

hover：`ButtonIconHoverBrush`；pressed：`ButtonIconPressedBrush`。

---

## 6. 间距与控件高度

| Token | 默认 | 用途 |
|-------|------|------|
| `SpaceXs` | 4 | 紧凑间隙 |
| `SpaceSm` | 8 | 表单项间距 |
| `SpaceMd` | 12 | 区块内边距 |
| `SpaceLg` | 16 | 卡片 padding |
| `SpaceXl` | 20 | 大区块 |
| `ControlHeightSm` | 28 | 输入框、下拉、搜索条（`discover-field` / `browse-search`） |
| `ToolbarButtonHeight` | 30 | 工具栏按钮、搜索钮（随字号档位缩放） |
| `DialogButtonHeight` | 34 | 模态 / 对话框 footer 按钮（`app-dialog-btn`） |
| `DialogButtonMinWidth` | 88 | 对话框按钮最小宽度 |
| `MenuItemHeight` | 28 | 紧凑上下文菜单项（`app-compact-menu`） |
| `ControlHeightMd` | 30 | 风格下拉等略高字段 |
| `ControlHeightLg` | 40 | 主按钮、大操作 |
| `LabelColumnWidth` | 60 | 元数据/表单左标签列（遗留；侧栏元数据实际列宽 72px） |
| `FieldScrollGutter` | 24 | 元数据右侧滚动条留白 |
| `ContentWidthForm` | 560 | 简单表单栈（曲库编辑等） |
| `ContentWidthList` | 640 | 设置内列表/表、changelog、状态列表 |
| `ContentWidthPreview` | 720 | 设计系统预览、长文说明 |
| `ControlWidthStandard` | 280 | 标准输入/搜索/连接结果 pill |
| `ControlWidthWide` | 420 | 宽输入、更新 feed、Tooltip 上限、首页引导文案 |
| `SearchFieldWidth` | 280 | 工具栏搜索框（同 `ControlWidthStandard`） |
| `ControlWidthToolbar` | 96 | 下载页重试钮、批量筛选下拉最小宽 |
| `ControlWidthToolbarSecondary` | 72 | 下载页搜索钮等紧凑工具栏按钮 |
| `ControlWidthFilterMin` | 100 | 筛选 ComboBox 最小宽、首页引导操作钮 |
| `ControlWidthFilterCompact` | 112 | 批量选择下拉最大宽 |
| `ControlWidthFilterMax` | 128 | 平台筛选下拉最大宽 |
| `ControlWidthFilterSortMax` | 120 | 排序下拉最大宽 |
| `ControlWidthOrganizeWide` | 144 | 整理工具作用域下拉 |
| `PlayerBarTitleMaxWidth` | 320 | 底栏正在播放标题区最大宽 |
| `VolumeSliderWidth` | 108 | 底栏 / 精简播放栏音量条 |
| `OnboardingHeroSize` | 180 | 首页空态引导封面与行高 |

**动效 Token**（`Styles/Tokens/Motion.axaml`，U2 已接入唱臂 / Drawer）：

| Token | 默认 | 用途 |
|-------|------|------|
| `MotionDurationFast` | 150ms | Tooltip、导航 hover |
| `MotionDurationBase` | 250ms | Drawer 滑入、蒙层 |
| `MotionDurationSlow` | 350ms | 预留 |
| `MotionDurationDrawer` | 250ms | 侧栏抽屉（与 `DrawerSlideOffsetPx` 配对） |
| `MotionDurationTonearm` | 750ms | 全屏唱臂起落 |
| `MotionTonearmDegreesPerTick` | 0.65° | 唱臂逐帧角速度 |
| `DrawerSlideOffsetPx` | 560 | Drawer 滑入位移（px） |

**间距网格**：所有新增间距须为 4 的倍数（`SpaceXs`–`SpaceXl`）；区块级外边距优先用 `MarginBottomXs/Sm/Md/Lg`，禁止 View 层一次性 magic number。

---

## 7. 组件 Class 清单（目标态）

### 按钮

| Class（代码） | 文档别名 | 说明 |
|---------------|----------|------|
| `primary` | `btn-primary` | 实心强调（保存、搜索、识别） |
| `secondary` | `btn-secondary` | 描边浅底次要 |
| `flat-btn` | `btn-ghost` | 透明/浅底文字按钮 |
| `danger` | `btn-danger` | 危险操作（删除记录和文件） |
| `icon-btn` / `top-icon-btn` | `btn-icon-toolbar` | 顶栏/侧栏图标钮 |
| `row-action-btn` | `btn-icon-row` | 列表行小图标钮 |
| `browse-search` | — | 搜索提交钮（高 `ToolbarButtonHeight`，字 `FontSizeCaption`，文字居中） |
| `browse-filter` | — | 工具栏筛选切换（与 `browse-search` 同高；选中 `AccentSoftBrush`） |
| `btn-toolbar` | — | 列表/面板右上角紧凑操作（清空、刷新等，**禁止**用 `flat-btn`） |
| `app-compact-menu-item` | — | 紧凑 `MenuItem`（`FontSizeCaption`，高 28） |
| `app-dialog-btn` | — | 对话框 footer 按钮（高 32，MinWidth 88，**文字水平垂直居中**） |
| `fullscreen-back-btn` 等 | `btn-icon-ghost` | 无框强调色图标（全屏返回） |

**交互 Token（随主题 / 强调色）**

| Token | 用途 |
|-------|------|
| `AccentHoverBrush` | 主色按钮 hover（随 Accent 计算） |
| `DangerHoverBrush` | 危险按钮 hover |
| `ButtonGhostHoverBrush` | 描边/ghost 工具栏按钮 hover |
| `ButtonIconHoverBrush` | 透明底图标按钮 hover |
| `ButtonIconPressedBrush` | 图标按钮按下 |

样式实现：`Styles/Components/Button.axaml` + `IconButton.axaml`。

### 表单

| Class | 说明 |
|-------|------|
| `field-input` | 文本框：未聚焦灰字，聚焦 1px Accent |
| `field-select` | 下拉（平台、风格） |
| `choice-inline` | 横向单选，12px 字，不破坏系统蓝点 |
| `choice-check` | 复选框 12px 字 |

### 文本

| Class | 说明 |
|-------|------|
| `text-caption` / `text-label` / `text-body` / `text-subtitle` / `text-lead` / `text-title` / `text-display` | 见 §4.2 |

### 模态 / 对话框

| Class | 说明 |
|-------|------|
| `modal-scrim` | 全窗半透明遮罩（`ModalScrimBrush`） |
| `player-elevated-popover` | 居中悬浮卡片容器 |
| `app-modal-panel` | 确认对话框内容区 padding |
| `app-dialog` / `app-dialog-shell` / `app-dialog-footer` / `app-dialog-actions` | 独立 Window 对话框结构 |
| `app-dialog-btn` | 对话框 footer 按钮：`DialogButtonHeight` + 居中 + `FontSizeLabel` |

### 搜索

| Class | 说明 |
|-------|------|
| `top-search.browse-search` | 曲库/传输/歌曲页搜索输入（高 `ToolbarButtonHeight`） |
| `browse-search` | 配套搜索按钮（同高、Accent 实心） |
| `browse-filter` | 歌曲页「我喜欢」等同排筛选 |
| `btn-toolbar` | 正在播放「清空」等面板内紧凑按钮 |
| `discover-field` | 发现页搜索/解析输入（高 28，圆角 6） |
| `app-dialog-field` | 模态表单输入（高 30，`InputFieldBrush` + 常显边框，`FontSizeBody`） |
| `app-dialog-intro` / `app-dialog-status` | 模态说明 / 状态文字 |
| `track-favorite-glyph` / `track-favorite-glyph-on` | 列表收藏爱心（`IconSizeLg`，已收藏 `FavoriteBrush`） |

### 布局（保留领域 class）

| Class | 说明 |
|-------|------|
| `track-row` / `track-col-*` | 曲目列表（见 **§14**） |
| `metadata-field-label` | P2 合并为 `text-label` |
| `right-panel` | 右侧边栏容器 |

---

## 8. 禁止事项（Code Review 检查）

- 禁止在 View 中使用 `#2D7DFF`、`#402D7DFF` 等硬编码强调色。
- 禁止 `FontSize="12"` 等魔法数（改用 `text-label` 或 DynamicResource）。
- 禁止图标 `FontSize="13"` 不通过 `icon-sm`。
- 禁止为新页复制一套 `fullscreen-xxx`、`metadata-xxx` 而不归入通用 class。
- 新功能必须先查本文档是否有现成 class。

---

## 9. 设计预览页（P2 交付）

路径规划：`Views/Pages/DesignSystemPageView.axaml`（仅 Debug 或设置内「开发者」入口）

预览内容区块：

1. 色板：中性色 + Accent + 语义色（Danger / Warning / ModalScrim / CoverPlaceholder）  
2. 排版：caption → display，含 `text-lead`  
3. 图标：xs → hero，含 `icon-2xl`  
4. 间距与控件高度：`Space*` / `ControlHeight*`  
5. 按钮：primary / secondary / flat-btn / danger + 别名 + 图标钮  
6. 搜索：`top-search.browse-search` + `browse-search`  
7. 模态：`modal-scrim` + `app-modal-panel` mock  
8. 表单：输入框、下拉、单选、复选  
9. 列表行：普通 vs 选中（图标变色）  

在设置页改 Accent 后打开预览页，可**一眼**看到全系统是否联动。

---

## 10. 文件结构规划（P3）

```
src/MusicVault.App/Styles/
  Tokens/
    Spacing.axaml           ← S1：间距与控件高度
    Typography.axaml        ← 已有
    Colors.Dark.axaml       ← 规划（暂合并在 ThemeResources.axaml）
    Colors.Light.axaml
    Icons.axaml
  Components/
    Button.axaml
    TextBox.axaml
    ComboBox.axaml
    RadioCheck.axaml
    Icons.axaml
    Navigation.axaml
    ListTrack.axaml
    Player.axaml
  AppStyles.axaml         ← 仅 Merged 引用
docs/
  DESIGN_SYSTEM.md        ← 本文件
```

---

## 11. 与当前代码的映射（迁移备忘）

| 区域 | 当前问题 | 目标 |
|------|----------|------|
| `ThemeService.ApplyAccentColor` | ~~只改 3 个 key~~ | ✅ P1：`AccentPalette` 整组 Accent* + Icon* |
| `AppStyles.axaml` | 1300+ 行混合 | 按组件拆分 |
| `ThemeResources.axaml` | Accent 重复 4 处 | 仅保留引用链 |
| 元数据侧栏 | `metadata-*` 特例 | `text-label` + `field-*` |
| 全屏 | `fullscreen-*` | `btn-icon-ghost` + `text-display` |
| 列表行图标 | 13/14px 混用 | 统一 `icon-sm` |

---

## 12. 已确认决策

| # | 决策 | 说明 |
|---|------|------|
| 1 | 默认强调色 `#007AFF`（蓝） | `AccentPalette.DefaultAccentHex`、`FluentTheme.Palettes.Accent` |
| 2 | 统计紫/橙与 Accent 分离 | `StatPurpleBrush` / `StatOrangeBrush` 固定；`StatAccentBrush` 随 Accent |
| 3 | 设计系统预览（P2） | 设置页最底部「外观」区 → **「打开设计系统预览」** → 独立大面板展示色板、字号、图标、按钮/表单/列表选中态；改 Accent 后在此一眼验证全局联动 |

### P1 已完成（Accent 联动）

- `AccentPalette.ApplyTo` 写入 `AccentBrush`、`AccentSoftBrush`、`AccentMutedBrush`、`LinkBrush`、`FocusRingBrush`、`IconAccentBrush`、`NavSelectedBrush`、`PlayerProgressFillBrush`、`StatAccentBrush` 等。
- `ThemeService.ApplyAccentColor` / `Apply()` 启动与换肤后均调用调色板。
- `AppStyles` 下载卡片 hover 边框改用 `AccentMutedBrush`；元数据输入聚焦改用 `FocusRingBrush`。
- 设置页强调色为 **7 个预设色块**；`AccentPalette` 同步 `FluentTheme` 的 `Accent` 与 `SystemAccentColor*`，单选/复选框随强调色变化。

---

### P2 已完成

- `Typography.axaml`：`FontSizeLabel`、`IconSizeXs`～`IconSizeHero`
- `ThemeResources`：`IconPrimaryBrush` / `IconSecondaryBrush` / `IconTertiaryBrush`
- `AppStyles`：`text-caption`～`text-display`、`icon-xs`～`icon-hero`、`field-input` / `field-select` / `choice-inline` / `choice-check`
- **设计系统预览**：偏好 → 底部「打开设计系统预览」
- 元数据侧栏、设置外观区、下载/发现行图标已接入上述 class

### P3 已完成

- `AppStyles.axaml` 拆为 `Styles/Components/*.axaml`（11 个组件文件），主文件仅 `StyleInclude` 聚合
- `Styles/Tokens/Typography.axaml` 为排版/图标尺寸唯一来源（`Typography.axaml` 保留兼容转发）
- **偏好页**全文案改用 `text-caption` / `text-label` / `text-subtitle` / `text-body`，去掉零散 `FontSize="11/12"`

### P4 已完成

- `Styles/Components/*.axaml` 中所有 `FontSize` 字面量改为 `{DynamicResource FontSize*}` / `IconSize*`
- 新增 Token：`FontSizeMicro`（10）、`FontSizeLead`（16）、`FontSizeStatValue`（34）
- 新增 class：`text-micro`；设计预览页展示 micro / stat 阶梯
- View 层与组件层硬编码色已清除（S2/S3）；按钮/间距已 Token 化（S4/S5）

### S1 已完成（系统补全）

- `Styles/Tokens/Spacing.axaml`：`Space*`、`ControlHeightSm/Md/Lg`、`LabelColumnWidth`、`FieldScrollGutter`、壳层高度
- 语义色：`ModalScrimBrush`、`FullscreenScrimBrush`、`WarningSoft/Text/BorderBrush`、`CoverPlaceholderBrush`、`StatPinkBrush`
- 排版 class：`text-lead`
- 按钮：`secondary` / `btn-secondary`；`primary`↔`btn-primary`、`flat-btn`↔`btn-ghost` 等别名
- `modal-scrim` 改用 `ModalScrimBrush`；表单高度接入 `ControlHeight*`
- 设计预览页扩展：语义色、间距、搜索条、模态 mock、danger/secondary

### S2 已完成（组件样式颜色 Token 化）

- 主流程 `Styles/Components/*.axaml` 已 Token 化；**豁免区**（播放器 chrome、Result 演示色等）见 **§13**
- 新增 Token：`SuccessBrush`、`Overlay*Brush`、`FlyoutSurface/BorderBrush`、`SliderThumbFillBrush`、`FullscreenControlHoverBrush`、`PopoverShadow`
- 发现页结果行、顶栏 nav-tab、下载徽章、Flyout/Popover 阴影等全部 `{DynamicResource}`

### S3 已完成（View 层迁移）

- 生产 `Views/` 已无硬编码 `#`（**豁免**：设计预览演示块、精简窗 chrome，见 **§13**）
- 发现页结果行改由样式 class 驱动
- 均衡器/播放栏图标统一 **Segoe Fluent Icons**（移除 MDL2）
- 专辑/艺术家占位改用 `CoverPlaceholderBrush` + Fluent 图标（移除 emoji）
- 侧栏、返回按钮、专辑轮播箭头改用 Fluent Icons
- `AboutWindow` / `CopyrightWindow` / `BatchMetadataWindow` 接入 `app-dialog` + `WarningSoft*` Token
- `CombinedLabelsSearchWindow` 歌词徽章改用 `StatPinkBrush`

### S4 已完成（按钮与排版扫尾）

- `Settings` / `AudioTools` / `Libraries` / `Playlists` 等页按钮统一 `flat-btn` / `primary` / `danger`
- 区块标题统一 `text-subtitle`；列表项标题统一 `text-label` / `text-caption`
- 移除冗余 `FontWeight` 与 inline `CornerRadius` / `Padding` 按钮样式

### S5 已完成（间距 Token 化）

- 新增 `MetadataLabelGap`、`FieldContentIndent`、`FieldContentMargin`、`PageContentMargin`、`SidebarEditorMargin`
- **批次 C**：View 层 378 处 inline `Margin` → `Spacing.axaml` Token + `PageLayout.axaml` 布局 class；审计仅统计字面量 magic number（排除 `{DynamicResource}` / `{Binding}`）
- 元数据面板 `metadata-content-indent` class 替代 `Margin="70,…"`
- 统计页外边距改用 `PageContentMargin`（16px）

---

---

## 14. 曲目列表表格（Track Table）

全应用曲目类列表必须共用同一套 **列宽、行高、边距、对齐** 规范。列定义常量见 `Layout/TrackTableLayout.cs`；表头复用控件见 `Controls/LibraryTrackTableHeader`、`CompactSixColumnTrackTableHeader`、`QueueSidebarTableHeader`。

### 14.1 尺寸与边距

| Token / 常量 | 值 | 用途 |
|--------------|-----|------|
| `TrackTableHeaderHeight` | 27px | 表头行高 |
| `TrackRowHeight` | 34px | 数据行高 |
| `TrackTableHeaderPairedMargin` | 16,0,33,2 | 与可滚动列表配对的表头（右缘 +17px 滚动条 gutter） |
| `TrackTableRowPairedMargin` | 16,0,33,0 | 配对列表的数据行 / 行间分割线 |
| `TrackTableScrollPadding` | 0,0,17,20 | 配对 ListBox 的 ScrollViewer 内边距 |
| `TrackRowHorizontalMargin` | 16,0,16,0 | 无滚动条 gutter 的数据行（独立表） |
| `TrackColHeaderMargin` | 8,0,8,0 | 表头文字列内边距 |
| `TrackColHeaderIndexMargin` | 0 | 序号/时长/操作等居中列（class `track-col-header-index`） |
| `TrackColCellMargin` | 8,0,8,0 | 数据单元格（class `track-col-cell`） |

**禁止**表头用 16px、数据行用 12px 的混用（会导致列错位）。

### 14.2 列宽方案（`TrackTableLayout`）

| 常量 | 列 | 典型页面 |
|------|-----|----------|
| `LibraryColumns` | # · 歌曲 · 艺术家 · 专辑 · 年份 · 时长 · 收藏 · 歌单 · 操作 | 曲库 / 歌单本地曲目（年份 56px、时长 64px、操作列 40px 固定宽） |
| `CompactSixColumns` | 序号/次数 · 歌曲 · 艺术家 · 专辑 · 时长 · 收藏 | 首页常听 / 推荐 / 播放历史（首列 56px；时长 64px、收藏 48px 固定宽） |
| `SidebarQueueColumns` | # · 歌曲 · 艺术家 · 时长 · 收藏 | 右侧播放队列 |
| `DiscoverColumns` | # · 封面 · 歌曲(含歌手) · 来源 · 时长 · 缓存 · 收藏 · 歌单 · 操作 | 发现页 / 榜单在线曲目 |
| `DiscoverBatchColumns` | 选 · # · 封面 · …（同上） | 试听记录批量操作 |
| `TransferQueueColumns` | 选 · # · 歌曲 · 歌手 · 状态 · 格式 · 大小 · 操作 | 传输页下载队列 |
| `BatchScrapeColumns` | # · 选 · 歌曲 · 艺术家 · 专辑 · 年份 · 标签 · 封面 · 歌词 · 操作 | 音频工具批量刮削 |

专辑详情在线曲目表（含格式/状态 Chip）为 **扩展方案**，右缘可留 20px 滚动条 gutter（class `album-catalog-track-row`）。

### 14.3 对齐规则

| 列类型 | 水平对齐 | 字色 / class |
|--------|----------|--------------|
| 序号、次数 | 居中 | `track-col-header-index` / `track-col-cell-leading` |
| 年份、时长、缓存 | 居中 | `track-col-header-meta` / `track-col-cell-meta` |
| 歌曲名 | 左 | `track-title` · `TextPrimaryBrush` |
| 艺术家、专辑 | 左 | `track-artist` / `track-album` · `TextPrimaryBrush` |
| 表头主列（歌曲） | 左 | `track-col-header-muted` |
| 表头其余文字列 | 左 | `track-col-header` |
| 收藏 / 歌单 / 删除 | 居中 | `row-action-btn`（28×28） |

表头与数据行 **必须使用相同** `TrackTableLayout.*Columns` 字符串；表头字号与数据行同为 `FontSizeLabel`（12px）。

### 14.4 滚动条配对（强制）

表头在 ListBox **外**、列表可纵向滚动时，必须：

1. 表头 Grid 加 class `track-table-header-paired`
2. 表头下分割线加 `track-table-divider-paired`
3. ListBox 加 class `track-table-scroll-list`（与 `virtual-list` 并用）

否则垂直滚动条出现时会吃掉 ~17px 宽度，列必然错位。

### 14.5 结构与 class

```text
StackPanel / DockPanel
  Grid.track-table-header.ant-table-header   ← 表头
  Border.track-row-divider                   ← 表头下分割线
  ListBox.virtual-list
    Border.track-row
      Grid.track-row-content                 ← 列宽与表头一致
      Border.track-row-divider               ← 行间分割线
```

### 14.6 页级对齐基准

| 区域 | 水平基准 Token | 说明 |
|------|----------------|------|
| 主内容区 | `PageContentMargin` / `PageContentHorizontalMargin`（16px） | 表单、卡片、空状态 |
| 页头工具栏 | `PageToolbarGridMargin` / `PageHeaderBlockMargin` | 与曲目表左缘对齐 |
| 首页右栏（历史/常听） | `HomeHistoryPaneMargin`（仅垂直）+ `HomeSectionHeaderMargin` / paired 表头 | 水平 16px 由表头 paired margin 统一，避免双层 inset |
| 侧栏二级列表 | `LibraryPaneHeaderMargin`（12px） | 仅侧栏树，不与主表混用 |
| 专辑在线曲目表 | `AlbumCatalogTrackTableHeaderMargin` | 左 16 / 右 20（留滚动 gutter） |
| **设置页右栏** | `SettingsPaneContentMargin`（**左 8px**） | **刻意例外**：贴 Nav 分割线，信息密度高；**不得**与主内容 16px 混用 |
| 设置页左侧 Nav | `SettingsNavRowPadding` / `SettingsNavHeaderMargin`（12px） | 与 `LibraryPaneHeaderMargin` 同档 |
| 曲库/元数据侧栏树 | `LibraryPaneHeaderMargin`（12px） | 260px 面板专用 |

**规则**：同一视觉列内的标题、工具栏、表头、数据行必须共用同一左缘（16px 或 paired 33px 右缘）；外层 ScrollViewer 滚动的列表（艺术家详情曲目）**禁止** paired 表头，使用 `ScrollPaired="False"`。从浏览页进入设置页时左缘由 16→8 的「收窄」为**产品决策**，非疏漏。

### 14.7 表单行与垂直节奏（全局）

| 场景 | 列定义 | 标签 class | 说明 |
|------|--------|------------|------|
| 偏好中心 / 长标签 | `108,8,Auto,*` | `settings-field-label` 右对齐 | 设置页专用 |
| 侧栏元数据 / 窄面板 | `72,8,*,…` | `metadata-field-label` | 260px 侧栏 |
| 简单配置页（曲库编辑） | `72,8,Auto,*` | `metadata-field-label` + `settings-field-label` | 与元数据侧栏同族 |

| 位置 | Token / class | 值 |
|------|---------------|-----|
| 字段行间距 | `settings-section-compact` 内 `settings-field-row` | 样式层统一 |
| Alert → 下一区块 | `MarginBottomSm` | 8 |
| Hero 分割线上下 | `PageHeroDividerMargin` | 0,10,0,10 |
| Tab 卡内容 | `SettingsAntdCardTabContentPadding` | 12 四边 |
| 字段说明 | ToolTip 优先；区块用 `settings-field-caption` / `ant-alert` | — |

---

## 15. 页头 Hero 区（Page Hero）

页面顶部的「英雄区」指 **标题/说明 + 可选工具栏 + 通栏分割线** 这一段，与壳层 `MainWindow` 注入的标准页头（`page-header-divider` inset）区分开。

### 15.1 三种 Hero 形态

| 形态 | class / 场景 | 按钮位置 | 分割线 |
|------|----------------|----------|--------|
| **列表 Hero** | `page-hero-copy` + `page-hero-toolbar` | 主操作在 **分割线上方** 工具栏 | `page-hero-divider` 通栏 |
| **详情 Hero（标准）** | 歌单/收藏详情 | 实体操作在 **分割线下方** `page-hero-footer` | 通栏 |
| **详情 Hero（背景）** | 艺术家/专辑大图顶 | 播放/刷新在封面旁（Spotify 式） | 无通栏线，接 Scroll 内容 |
| **壳层页头** | `MainWindow` `ShowStandardPageHeader` | 仅返回/状态；表单页可在标题行放「保存」 | `page-header-divider` **左右 inset 16px** |
| **页内区块** | 首页常听/历史 | 标题与按钮 **同一行** `page-section-header` | 直接用表头分割线，不用 Hero |

### 15.2 结构（列表页强制）

```xml
<DockPanel> <!-- 或 StackPanel -->
  <StackPanel Classes="page-hero-copy">      <!-- 仅 page-title + page-intro -->
  <Grid Classes="page-hero-toolbar">         <!-- 搜索/筛选/导入/创建等主按钮 -->
  <Border Classes="page-hero-divider"/>      <!-- 通栏，Margin=PageHeroDividerMargin -->
  <!-- 列表 / 表头 -->
</DockPanel>
```

**禁止**：在 `page-hero-copy` 文案区放主操作按钮后又在其下方再放一条 inset 分割线（歌单页旧布局）。

### 15.3 Token 与 class

| Token / class | 值 / 用途 |
|---------------|-----------|
| `PageHeaderMargin` | 文案区外边距 16,16,33,0 |
| `PageToolbarGridMargin` | 页内首行工具栏 16,16,33,8（无 Hero 文案时） |
| `PageHeroToolbarMargin` | Hero 文案下方工具栏 16,0,33,8 |
| `PageHeroDividerMargin` | 通栏分割线 0,10,0,10 |
| `page-hero-divider` | 与旧 `discover-section-split` 等价，**到左右边缘** |
| `page-header-divider` | **仅壳层**，Margin 16,6,16,0 |
| `page-toolbar-divider` | 页内区块（如发现结果摘要上方），同通栏 margin |

### 15.4 已统一页面

发现、下载、整理工具、艺术家/专辑浏览、歌单浏览、设置分区页头、设置概览快捷入口 — 均使用 `page-hero-divider` 通栏线；歌单/在线收藏主按钮已移至分割线上方。

---

*文档版本：S5 + U1–U5 + V1–V5 · 2026-06*
