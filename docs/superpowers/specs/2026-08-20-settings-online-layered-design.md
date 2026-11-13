# 设置 · 在线源日常/高级分层（设计）

日期：2026-08-20  
状态：已确认（用户选 A→日常层方案 2→自动展开方案 2→实现路径 B）  
对照：`SettingsPageView.axaml` 在线源段、`SettingsPageViewModel` / `.Online.cs`、`Expander.ant-collapse`、`2026-08-18-settings-online-polish.md`

## 1. 意图

打开「在线源」时，默认像听歌软件：一眼知道能不能连、要不要自启内置服务，并完成测试/登录。  
平台 URL、Cookie、识曲地址、采集源矩阵默认折叠；有阻断问题时自动展开**相关**块。

## 2. 不做

- 不改左栏 7 项导航名称/数量
- 不改即时 vs 保存模型（在线源仍需「保存设置」）
- 不拆 `SettingsPageViewModel`
- 不删任何现有字段或能力；不跨页搬家（AcoustID 等本轮不动）
- 不把夸克 Cookie 未填当作阻断
- 不把「登录为空」当作展开平台接口的条件（登录操作留在日常层）
- 不持久化折叠状态到设置文件
- 不新做折叠控件（复用 `Expander.ant-collapse`）

## 3. 页结构（自上而下）

### 3.1 日常层（默认全部可见）

1. **页级告警** — 现有 `ShowOnlineDownloadAlert` / 内置 API 更新提示（保留）
2. **连接状态**
   - 三行摘要：统一 API、网易云、登录（见 §4）
   - 操作行：测试统一、测试网易云、平台登录、诊断登录、全量探测、排查（现有按钮；排查仍仅在测试失败时显示）
   - 登录摘要 pill、诊断文案、全量探测结果列表（现有绑定，仍挂在本区下方）
3. **内置服务** — 自动启动统一 API / 网易云 API、每周检查更新（三开关；平台不可用提示保留）

### 3.2 高级层（三个独立 Expander）

| 顺序 | 折叠标题 | 内容（现有控件原样迁入） |
|------|----------|--------------------------|
| 4 | 平台接口 | 统一 API / 网易云 / QQ / 夸克 Cookie 双列网格 |
| 5 | 识曲 | URL + 指南 / 测试 / 网易云默认 |
| 6 | 采集源 | 「测试全部」+ 采集源开关网格 |

每个 Expander：`Classes="ant-collapse settings-online-advanced"`（或等价），`IsExpanded` 绑定 ViewModel。

折叠头在「有阻断且当前收起」时显示次要徽章「需处理」（l10n），避免问题不可见。

## 4. 连接状态摘要

| 行 | 绿 | 灰 | 红 |
|----|----|----|-----|
| 统一 API | 本会话测试成功 | 尚未测试且地址非空 | 地址为空，或本会话测试失败 |
| 网易云 | 同上 | 同上 | 同上 |
| 登录 | 现有已登录摘要 | 加载中 | 未配置 API / 未登录 / 异常（现有 `OnlineLoginStatus*`） |

- 「地址为空」：对应 `MusicApiBaseUrl` / `NetEaseApiUrl` trim 后为空。
- 测试态：沿用 `ShowUnifiedApiTestResult` / `UnifiedApiTestSucceeded` 与网易云对偶属性；未点过测试 → 灰（若地址非空）。
- 夸克 Cookie 未填：仅高级区内现有徽章，不进摘要三行、不触发展开。

## 5. 自动展开策略

### 5.1 纯函数

新增可单测策略（建议名 `OnlineAdvancedExpandPolicy`，放 App 服务或小静态类，**不**塞进巨型 ViewModel 逻辑散落处）：

```text
ExpandPlatform  = UnifiedUrlEmpty || NetEaseUrlEmpty
               || UnifiedTestFailed || NetEaseTestFailed

ExpandRecognition = MusicRecognitionTestFailed
  （空识曲 URL 且会回落到网易云默认 → 不算阻断；仅本会话识曲测试失败才展开）

ExpandGatherers = 存在 IsEnabled 且 LastProbeStatus 判定为失败的采集源
  （从未探测：null / 空 / 展示为「—」→ 不算）
```

采集源失败判定（对 `LastProbeStatus` 的 codec key，或 ViewModel 同步保留的原始 key）：

| 算失败 | 不算失败 |
|--------|----------|
| `Gatherer.Probe.Exception` | `Gatherer.Probe.Available` |
| `Gatherer.Probe.HttpStatus` | `Gatherer.Probe.PlatformDirect` |
| `Gatherer.Probe.UrlNotConfigured` | `Gatherer.Probe.Unknown` |
| `Gatherer.Probe.NotConfigured` | null / 空 / 未探测 |

实现时：`GathererItemViewModel` 需能暴露「原始 probe key」或 `IsProbeFailed`，避免只靠已本地化的 `StatusText` 字符串匹配。

### 5.2 何时写入 `IsExpanded`

| 时机 | 行为 |
|------|------|
| 进入在线源面板（`ShowAdvancedOnlinePanel` 变为 true） | 三块 `IsExpanded` ← 策略结果 |
| 本会话测试刚变为失败（统一/网易云/识曲） | 对应块 `IsExpanded = true` |
| 采集源「测试全部」完成后 | 按策略刷新 `ExpandGatherers`；若为 true 则展开 |
| 用户在本页手动收起 | 允许；**停留本页期间**不因同一阻断条件反复强行拉开 |
| 用户手动收起后，**新的**失败事件发生 | 再拉开对应块 |
| 离开在线源再进入 | 重新按 §5.1 计算（不读磁盘） |

「用户手动收起」用会话内标志（例如 `_userCollapsedPlatform` 等三枚）。**每次进入**在线源面板时清除这三枚标志，再按 §5.1 写入 `IsExpanded`，保证每次进入仍按阻断计算。

推荐实现：`IsExpanded` 双向绑定；策略属性 `ShouldExpand*` 只在上述时机赋值到 `IsExpanded`，避免与用户拖拽头部打架时用单向强制覆盖每一帧。

## 6. 文案与样式

- 折叠标题复用现有组标题 l10n：`OnlinePlatformApisTitle` / `OnlineRecognitionGroupTitle` / `OnlineGathererSourcesTitle`
- 新增短文案：`Settings.Online.NeedsAttention` →「需处理」（及英文）
- 日常层组标题：连接状态 / 内置服务沿用现有 key
- 高级 Expander 边距跟随 `Expander.ant-collapse`；内容区内平台双列宽度保持当前 `SettingsGathererListWidth` / pair 样式，本轮不重开视觉语言大改

## 7. 测试

1. **单元**：`OnlineAdvancedExpandPolicy` 表驱动（空 URL、测试失败、识曲失败、采集源 enabled+失败 key、未探测、disabled+失败 key 不展开）
2. **Chrome**：更新 `Online_page_orders_*`  
   - 顺序：告警 → 连接状态 → 内置服务 → 平台 Expander → 识曲 Expander → 采集源 Expander  
   - 存在 `Expander` + `ant-collapse`；平台/识曲/采集源标题落在 Expander 内  
   - 仍不含页内 persist caption
3. **手工**：健康态三块折叠；人为测挂统一 API → 仅平台展开；手动收起后再点失败测试 → 再展开；夸克未填不展开；登录为空不展开平台

## 8. 成功标准

- 默认一屏内可回答「能不能连、要不要自启」，无需展开高级
- 阻断时相关高级块自动露出；非阻断高级保持折叠
- 现有字段均可在高级中找到并保存；行为与保存模型不变
- 策略单测 + chrome 测试通过；发布后目视符合 §3

## 9. 实现落点（计划阶段用）

| 区域 | 文件（预期） |
|------|----------------|
| 策略 | 新 `OnlineAdvancedExpandPolicy.cs` + 测试 |
| VM | `SettingsPageViewModel` / `.Online.cs`：摘要色、三块 `IsExpanded`、会话收起标志、时机赋值 |
| 采集源 | `GathererItemViewModel`：probe key / `IsProbeFailed` |
| 视图 | `SettingsPageView.axaml` 重排在线源段 |
| 样式 | 必要时 `Settings.axaml` 微调 Expander 头内徽章；优先用现有 token |
| l10n | `SettingsLocalizationResources`（或既有资源表） |
| 测试 | `SettingsSectionChromeTests` + 新 policy 测试 |

废止本需求范围内 `2026-08-18-settings-online-polish.md` 的「不折叠」约束；该文其余已落地项仍有效。
