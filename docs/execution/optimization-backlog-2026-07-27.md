# MusicVault 优化顺序清单

日期：2026-07-27（UI P0 追加 2026-07-29）  
原则：**紧急度 × 体验收益**；识曲交互重写挂起，不进本清单实施项。  
状态：工程 P0–P3 已落地；**UI 气质 P0（方案 A）已落地**；余下 2.1 LibVLC 人工冒烟 / 2.2 macOS 实机

## 排序规则

| 优先级 | 含义 |
|--------|------|
| **P0** | 主路径天天碰到，或改不动就会拖死后续体验工作 |
| **P1** | 高频触达，体感明显 |
| **P2** | 稳定/诚实，支撑信任 |
| **P3** | 结构还债，降低长期成本 |
| **延后** | 明确不做或另开专项 |

---

## UI P0 — 内容优先沉浸（方案 A，2026-07-29）

设计：`docs/superpowers/specs/2026-07-29-ui-p0-immersive-polish-design.md`

| # | 事项 | 验收 | 状态 |
|---|------|------|------|
| U0.1 | **发现页 chrome 收敛** — 默认单行搜索；更多展开歌手/模式/登录；传输侧栏默认收起 | 首屏以结果区为主 | ✅ |
| U0.2 | **首页空态** — 氛围视觉 +「去发现」主 CTA；次要入口弱化；去 Steps | 空首页一眼能点主 CTA | ✅ |
| U0.3 | **全屏弱化工具感** — 更大封面、更强氛围底、字号控件降权 | 封面/歌词为主舞台 | ✅ |

---

## P0 — 紧急 × 高体验（当前迭代）

| # | 事项 | 类型 | 验收 | 状态 |
|---|------|------|------|------|
| 0.1 | **发现搜索：失败/部分失败可读** — banner 点名失败源；全失败时空结果也给 warning；回退直连可见 | 体验 | 一源挂、一源通 → 有结果 + 能看出哪源挂；全挂 → 不空口「无结果」 | ✅ |
| 0.2 | **SearchPageViewModel 拆分第一刀** — 抽出 `DiscoverSearchFeedbackBuilder` + 单测 | 结构 | 搜索反馈逻辑可单测；VM 不再堆纯文案拼装 | ✅ |
| 0.3 | **发现结果操作路径缩短** — 试听/入队分级反馈（状态色 + Toast）、试听行高亮、入队展开传输侧栏 | 体验 | 双击试听、入队后状态一眼可读 | ✅ |

---

## P1 — 高体验主路径

| # | 事项 | 类型 | 验收 | 状态 |
|---|------|------|------|------|
| 1.1 | **播放栏** — 恢复提示修复、传输键禁用、开播 busy、静音、文案去歧义 | 体验 | 日常播放主流程顺滑 | ✅ |
| 1.2 | **曲库列表** — 情境空状态 CTA、搜索优先文案、LoadMore 不重绑滚动 | 体验 | 无歌有引导；有歌滚动不抖 | ✅ |
| 1.3 | **传输页失败可读** — 行内短原因、常见错误语义化、筛选含错误、取消可恢复 | 体验 | 失败行能看懂、能重试 | ✅ |
| 1.4 | **PlayerBarViewModel 瘦身** — 抽出 `PlayerBarViewModel.Transport.cs` | 结构 | 传输控制独立 partial | ✅ |

---

## P2 — 稳定与诚实

| # | 事项 | 类型 | 验收 | 状态 |
|---|------|------|------|------|
| 2.1 | LibVLC Windows 人工冒烟清单闭环 | 稳定 | 文档勾选通过 | ⏳ 自动冒烟已过；人工项仍待勾选（见 `libvlc-phase-4.md`） |
| 2.2 | LibVLC macOS 实机（有 VLC.app） | 稳定 | 实机可播 | ⏸ 需 Mac |
| 2.3 | 在线源/Cookie 可用性在发现或设置侧更可见 | 诚实 | 用户少猜「为什么没结果」 | ✅ 发现 Alert + 空态 CTA；设置在线源登录摘要 / 夸克 Cookie pill / 诊断 |
| 2.4 | 发布门禁：设计系统 audit + Headless 冒烟保持硬门槛 | 工程 | CI/发布脚本不回退 | ✅ 修复 audit/导航冒烟回归（能力矩阵绑定 + Token 化） |

---

## P3 — 可维护性还债

| # | 事项 | 类型 | 验收 | 状态 |
|---|------|------|------|------|
| 3.1 | SettingsPageViewModel 按域拆分 | 结构 | 单文件显著变短 | ⏳ 主 ~1446；Appearance / Playback / Download / Online / Updates / Backup + **Localization / Persistence / Insights / Overview**；属性与导航壳仍在主文件 |
| 3.2 | 收窄 AppServices 扇出（页面注入 Facade/Application） | 结构 | 新页不再整包 `_services` | ✅ 第一刀：`AppLibraryFacade`+WebDav；`LibrariesPageViewModel`/`EqualizerViewModel` 收窄；`NasLibraryUiHelper` 走 Facade；约定见 [app-services-injection.md](../engineering/app-services-injection.md) |
| 3.3 | LocalizationResources 按分区拆文件 | 结构 | 合并冲突下降 | ✅ 主表 ~1450；Settings/Discover/Library/Transfers/Metadata 独立文件 + TryGet 回退 |
| 3.4 | 巨型页关键行为单测（入队确认、切歌等） | 测试 | 回归有网 | ✅ 入队确认弹窗、空搜 hint、静音纯逻辑、切歌边角、行级入队文案、预估体积 |

---

## 延后（挂起 / 不做本波）

| 事项 | 原因 |
|------|------|
| **听歌识曲交互重写**（QQ 音乐式沉浸） | 用户明确挂起 |
| 再扩平台源 / 新指纹服务 | 主路径未稳前不加面 |
| 大范围换肤 / 重做设计系统 | Token 与门禁已够；缺的是流程 |
| 全面 Features 层大迁移 | 随拆 VM 顺带，不单独立项 |

---

## 建议迭代节奏

```
迭代 1（当前）  P0.1 + P0.2 → 视情况 P0.3
迭代 2          P1.1 + P1.4
迭代 3          P1.2 + P1.3
迭代 4          P2.* + P3 择一
```

每迭代只承诺：**1 个体感目标 + 1 个结构目标**。

---

## 相关文档

- 架构评价结论见对话 2026-07-27；路线图总览：`docs/ROADMAP.md`
- 搜索韧性：`docs/superpowers/specs/2026-07-17-production-search-phase-a-design.md` / `phase-b-design.md`
