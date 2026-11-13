# 设置 · 偏好自动保存（设计）

日期：2026-08-20  
状态：已确认（用户选 A → 防抖策略 1 → 实现路径乙）  
对照：`PRODUCT_DESIGN.md` §6.2 / §14.2–14.3、`SettingsPageViewModel`（含 `.Appearance` / `.Playback` / `.Download` / `.Online`）、`SettingsPageView.axaml` 页头保存钮与 `settings-sticky-save-bar`

## 1. 意图

设置里的**偏好字段**改完即生效并落盘，对标系统偏好中心。朋友不必再找「保存设置」。  
危险/一次性操作仍用明确按钮与确认。

本轮解冻 `PRODUCT_DESIGN` 中「全面自动保存」；**不同时**做导航收到 4 项。

## 2. 不做

- 撤销条 / Undo / 放弃更改
- 左栏 7 项改名、合并或收到
- 拆分 `SettingsPageViewModel`
- 重做主题 / 设计系统
- 为每个分区写独立 `ApplyXxxSettingsAsync` 全量复制（路径甲）
- 新建重量级 `SettingsAutosaveCoordinator` 服务层（路径丙，可留作后续重构）
- 改变备份恢复、清缓存、探针、登录、更新安装等**操作**语义

## 3. 保存规则（唯一合同）

| 类别 | 行为 | 示例 |
|------|------|------|
| **即时偏好** | 变更后立刻调度整包落盘 | 开关、下拉、滑条、色板、并发数、音质索引、采集源开关、更新频率 |
| **防抖偏好** | 变更后 **400ms** 防抖再落盘；切分区 / 离开设置 / 应用退出前强制冲刷 | API URL、下载路径、夸克 Cookie、AcoustID Key、识曲 URL、Update Feed URL、自定义采集 BaseUrl |
| **外观** | 保持现有即时路径；可并入同一调度，对外行为不变 | 主题、语言、字号、强调色 |
| **操作** | 不经自动保存；按钮 / Popconfirm | 备份、恢复、清 NAS 缓存、检测/安装更新、探针、登录、导出诊断、重置用量、打开文件夹等 |

禁止再引入第三种「改完要点保存」规则。新偏好字段必须标为即时或防抖之一。

## 4. 实现路径（乙）

1. 保留现有 `SaveSettingsAsync` 主体为**唯一**偏好落盘入口（整包写入 `_settings` + 必要副作用：主题、字号、下载根、Gatherer bootstrap、AcoustID 配置等）。
2. 将 `MarkPendingChanges()` 替换为 `ScheduleAutosave(AutosaveKind.Instant | Debounced)`：
   - Instant：取消未决防抖后立即 `PersistPreferencesAsync`
   - Debounced：重置 400ms 计时器，到期后同一 `PersistPreferencesAsync`
3. `PersistPreferencesAsync` ≈ 现 `SaveSettingsAsync` 的持久化与副作用，但：
   - **不再**依赖 `HasPendingChanges` 驱动 UI
   - 成功时默认**不**弹成功 Toast（避免每拨一下开关就打扰）；需要用户可知的副作用（如实验性播放引擎需重启）仍用现有提示通道
   - 失败：壳层 / 状态条短错误；字段保留编辑值；不静默吞掉
4. 外观页：可继续走 `ApplyAppearanceSettingsAsync`，或改为调用同一 `PersistPreferencesAsync`；验收以「改完即生效且落盘」为准，不强制合并代码路径。
5. LibVLC 引擎等已有「立刻 Persist」的特例：保持立刻写入，不经脏态。

## 5. UI 变更

移除（设置偏好页）：

- 页头 `ShowHeaderSaveButton` /「保存设置」
- 底栏 `ShowStickySaveBar` / `PendingChangesHint`
- 导航项未保存圆点（`ShowPendingDot` 因脏态为 true 的路径）
- 页内「需保存 / PersistHint」类说明（若仍残留）

可保留：

- 外观页既有「即时生效」类短说明（可选缩短为一句，或改为全局不再强调「保存」）
- 概览页推荐应用等**显式**动作按钮

Usage Insights 里「设置保存次数」：自动保存仍计为一次 persist（或按防抖合并后的一次写入计一次）；不在本轮改 Insights IA。

## 6. 生命周期冲刷

必须在以下时机 `FlushAutosaveAsync()`（取消计时器并若有未写入变更则立即 persist）：

1. `SelectedSection` 即将切换时
2. 离开设置页（导航离开 Settings）时
3. 应用退出 / `App` 已有设置保存路径之前（与现有 shutdown save 对齐，避免丢最后一次防抖）

冲刷与 Instant 写入共用同一串行化：同时只跑一个 persist；排队合并为最新状态（后写覆盖）。

## 7. 字段分类清单（实现核对表）

### 7.1 即时（非穷尽，以 `MarkPendingChanges` 调用点为准）

播放：热键、恢复播放、NAS 缓存、桌面歌词开关与数值、默认循环/随机、默认音量、响度归一、均衡器开关等（引擎切换保持现有立刻 Persist）。  
下载：并发、音质、发现批量向导开关、NAS 目标库索引等。  
在线：内置 API 三开关、采集源 Enabled。  
更新：检查频率（若当前走 pending）。  
数据页偏好：AcoustID 启用开关。

### 7.2 防抖

`DownloadDirectory`、`MusicApiBaseUrl`、`NetEaseApiUrl`、`QqMusicApiUrl`、`MyFreeMp3QuarkCookies`、识曲相关 URL、`AcoustIdApiKey`、`UpdateFeedUrl`、采集源 `BaseUrl` 文本。

### 7.3 操作（不自动保存）

备份/恢复、打开目录、清缓存、测试/探针/登录/诊断、检查更新与安装、导出诊断与用量、重置用量、打开均衡器窗口等。

实现前用测试或静态清单扫一遍 `MarkPendingChanges` / 文本 `On*Changed`，与上表对齐；有遗漏以「文本防抖、其余即时」为默认。

## 8. 文档

- 更新 `PRODUCT_DESIGN.md`：§6.2 保存规则改为自动保存合同；§14.2 删除「全面自动保存」冻结；§14.3 将「设置自动保存」标为进行中/已决议（导航收到仍中期）。
- 专项 chrome / online layered spec 中「仍需保存设置」句子在实现后改为指向本文。

## 9. 验收

1. 改下载并发 → 立刻写入；杀进程重启后仍在。
2. 改统一 API URL：连续输入期间不连写；停 400ms 或切到「播放」后写入；重启仍在。
3. 设置页无「保存设置」按钮与脏态底栏；导航无未保存圆点。
4. 备份仍须点「立即备份」；恢复仍确认。
5. 落盘失败时有可见错误，值未丢。
6. 单测：Instant vs Debounced 调度、冲刷取消计时器、连续变更只落盘一次（防抖合并）；chrome 断言无 sticky save / header save。

## 10. 风险

| 风险 | 缓解 |
|------|------|
| 整包写入在拨开关时略重 | 可接受；后续可再做字段级 write |
| URL 半成品被冲刷写入 | 预期行为；空/非法 URL 仍由现有探测与状态文案表达 |
| 与外观双路径重复写入 | `_suppressPendingChanges` / 加载期抑制保持；合并调度时去重 |
| 退出时丢防抖 | 强制挂接 Flush（§6） |
