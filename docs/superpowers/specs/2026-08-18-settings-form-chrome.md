# 设置表单 chrome 合同（系统偏好）

日期：2026-08-18  
对照：用户确认方案 B；`DESIGN_SYSTEM.md` §14.6–14.7

## 意图

外观 / 播放 / 下载 / 数据 / 在线源 / 更新 六页表单像系统偏好：固定标签列、控件左缘对齐、字号三档、垂直节奏统一。概览保持仪表盘，不套本表单合同。

## 不做

- 不改左侧 7 项导航
- 不改即时 vs 保存规则
- 不拆 `SettingsPageViewModel`
- 不折叠内容、不搬家（NAS/AcoustID 本轮不动）
- 不换主题色 / 图标字体

## 合同

### 版式

| 项 | 值 |
|----|-----|
| 字段行列 | `108,12,*` |
| 标签 class | 仅 `settings-field-label`（禁止同挂 `text-label` / `metadata-field-label`） |
| 控件对齐 | 值列左齐（开关、下拉、滑条、路径、按钮） |

### 字号

| 角色 | Token / class | 约 |
|------|---------------|-----|
| 页标题 | `settings-page-title` | 既有 |
| 组标题 | `settings-group-title` → `FontSizeTitle3` SemiBold | 15 |
| 字段名 | `settings-field-label` → `FontSizeBody`，`TextAlignment=Right` | 13 |
| 说明 | `settings-field-caption` → `FontSizeCaption` | 11 |

概览专用（`settings-overview-*` / KPI）不改。

### 间距

| 项 | 值 |
|----|-----|
| `SettingsGroupBodySpacing` | 8 |
| 组标题 margin（组内首项） | `0,0,0,4` |
| 组标题 margin（组内后续） | `0,16,0,4` |
| 字段行 | `MinHeight` 36；`Margin` 0（行距靠组 Spacing） |
| 开关行 | 与普通行同一 `MinHeight` 36 |

表单页内去掉零散 `Spacing="6"` / 硬编码 16，改走 Token 或组 Spacing。

### 结构

- 播放 / 更新保留 `AntdCardTab`
- 组用 `settings-group` + `settings-group-title`；不用第二套 `settings-overview-panel-title` 当表单组标题

## 成功标准

- 六页字段行列均为 `108,12,*`
- 设置页无 `metadata-field-label`；字段标签无并存 `text-label`
- 字段标签最终样式 `FontSizeBody` + `TextAlignment=Right`
- 组标题统一 `settings-group-title`
- 相关 chrome 测试通过；发布重启后外观/下载目视：标签列与控件列各一条竖线
