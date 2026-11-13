# 设置表单 chrome 实现计划

> **For agentic workers:** `docs/superpowers/specs/2026-08-18-settings-form-chrome.md`

## 文件

- `tests/.../SettingsSectionChromeTests.cs` — 标签禁止 `text-label`
- `SettingsPageView.axaml` — 去掉字段上的 `text-label`；表单区内硬编码 Spacing 改 Token
- `Styles/Components/Settings.axaml` — 行高/组标题 margin
- `docs/DESIGN_SYSTEM.md` §14.7 — 同步

## 任务

1. 测试：字段标签 class 不得含 `text-label`
2. XAML + 样式按合同改
3. 全量测试 + publish -Restart
