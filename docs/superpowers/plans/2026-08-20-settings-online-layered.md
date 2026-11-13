# 在线源日常/高级分层 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 在线源页默认展示连接状态 + 内置服务；平台接口 / 识曲 / 采集源三个 Expander，阻断时自动展开相关块。

**Architecture:** 纯函数 `OnlineAdvancedExpandPolicy` 计算三块展开；ViewModel 管会话收起标志与 `IsExpanded` 赋值时机；视图重排并用 `Expander.ant-collapse`。

**Tech Stack:** Avalonia Expander、CommunityToolkit.Mvvm、xUnit、现有 Settings chrome 测试。

**Spec:** `docs/superpowers/specs/2026-08-20-settings-online-layered-design.md`

---

### Task 1: Policy + tests

- [x] 写 `OnlineAdvancedExpandPolicyTests`（表驱动）
- [x] 实现 `src/MusicVault.App/Services/OnlineAdvancedExpandPolicy.cs`
- [x] 跑测试通过

### Task 2: Gatherer probe key

- [x] `GathererItemViewModel` 存 `LastProbeStatusKey` / `IsProbeFailed`
- [x] `ApplyStatus` / `RefreshGathererItems` / `ProbeGatherersAsync` 同步 key

### Task 3: ViewModel

- [x] 连接摘要 tone（绿/灰/红）属性
- [x] `IsPlatformApisExpanded` / Recognition / Gatherers + 会话收起
- [x] 进入面板 / 测试失败 / Probe 完成时赋值

### Task 4: View + l10n + chrome

- [x] `SettingsPageView.axaml` 重排
- [x] 「需处理」l10n
- [x] 更新 `SettingsSectionChromeTests.Online_page_*`
- [x] `publish-with-api.ps1 -Restart`
