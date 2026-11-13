# 设置二级页收口 Implementation Plan

> **For agentic workers:** 每阶段开始前先向用户陈述思路，确认后再改代码。不要一次改完七页。

**Goal:** 七项导航不动，逐页把设置右栏收成偏好中心，专家项分层。

**Architecture:** 先统一 `settings-page-header` 与 `108,8,Auto,*` 表单行，再按页减负或搬家。保存规则维持 PRODUCT_DESIGN §6.2。

**Tech Stack:** Avalonia `SettingsPageView.axaml`、`SettingsPageViewModel` 可见性属性、现有 `settings-field-row` / `ant-switch` / `antd-card-tabs`。

**Spec:** [2026-08-17-settings-section-pages.md](../specs/2026-08-17-settings-section-pages.md)

**Freeze:** 设置导航大改、自动保存、拆巨型 VM、换色、换图标字体。

**Commits:** 仅当用户明确要求时提交。

---

## 文件地图

| 文件 | 职责 |
|------|------|
| `src/MusicVault.App/Views/Pages/SettingsPageView.axaml` | 各二级布局 |
| `src/MusicVault.App/ViewModels/Pages/SettingsPageViewModel.cs` | `ShowSectionPageHeader` / 保存可见性 / Tab |
| `src/MusicVault.App/Styles/Components/Settings.axaml` | 页头、字段行、外观/播放特例 |
| `src/MusicVault.App/Localization/*.cs` | 分组标题、折叠「高级」文案 |
| `docs/DESIGN_SYSTEM.md` §14.7 | 列宽合同（已写 108,8） |
| `tests/MusicVault.Tests/` | 若有设置可见性测试则补；否则 Headless/发布验 |

---

## 阶段 0：页头与表单行

**改前思路（用户确认后再做）：** 只动 chrome，不搬家字段。

- [x] 外观、播放、下载等所有 `settings-field-row` 列定义改为 `108,8,Auto,*`（AcoustID 行暂留）
- [x] `ShowSectionPageHeader` 仅排除 Overview（外观/播放改走共用页头，去掉第二份自绘）
- [x] 外观即时说明、播放状态句留在共用页头副文案区
- [x] 保存条逻辑不改：`HasPendingChanges` → 吸底；否则页头保存钮
- [x] 发布重启；切七个二级看页头是否齐

## 阶段 1：外观

- [x] 「恢复默认」从第一行移到强调色下方
- [x] 发布；语言/主题/字号/色块即时仍生效

## 阶段 2：概览

- [x] 第一屏：KPI + 快捷入口（含管理来源）
- [x] 介绍、免责、功能 Tab、许可证移到该页底部折叠或「关于」已有入口，不删文案
- [x] 发布；空库引导仍在 KPI 下

## 阶段 3：下载 + 播放

- [ ] 播放页删除 NAS 缓存/目标库行；下载页同一组加入
- [ ] 平台能力芯片移出播放偏好（放到更新诊断或在线源状态，选一处）
- [ ] 播放：日常开关一组，实验引擎一组
- [ ] 发布；确认没有双份 NAS 开关

## 阶段 4：数据

- [ ] 去掉 AcoustID 整块（阶段 5 立即接上，或本阶段末尾若用户要一次做完 4+5）
- [ ] 备份表、分页、导出诊断保留

## 阶段 5：在线源

- [ ] 默认区：登录状态、一键检测、捆绑服务开关
- [ ] 折叠：API URL、Cookie、采集源、AcoustID
- [ ] 发布

## 阶段 6：更新与诊断

- [ ] 默认 Tab 为应用更新；changelog/排查不作为第一屏
- [ ] 不删四个 Tab，只改默认与说明密度

## 阶段 7：验证

- [ ] `dotnet test` 相关 + `.\scripts\publish-with-api.ps1 -Restart`
