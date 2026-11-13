# 发现页 QQ 乐馆壳重设计（深色 · 乙：同框双态）

日期：2026-11-11  
状态：已确认（用户 2026-11-11）  
对照：既有 `SearchPageView` / `DiscoverShellIndex`、spec `2026-11-11-discover-online-listen-redesign.md`（推荐\|搜索分段）、竞品 QQ 音乐 PC 乐馆气质  
设计图：`.cursor/projects/d-MusicVault/assets/discover-b-recommend.jpg`、`discover-b-search.jpg`、`discover-b-zones.jpg`

## 1. 意图

把发现做成**深色 MusicVault 底色上的 QQ 式在线听主场**：推荐流内容先行，搜索是页内常驻细条工具，而不是对等分段或设置台。

已确认：

- 学 QQ：**壳层 + 视觉**（大 Banner、快捷入口、横滑大封面）  
- 色调：**深色 MusicVault token**（不抄 QQ 青绿品牌色）  
- 结构：**乙 · 同框双态**（细搜常驻；未提交=推荐流，已提交=结果表）  
- 取消页顶「推荐 | 搜索」分段  

## 2. 不做

- 新推荐算法、新平台、新远程协议  
- 识曲采集交互重写  
- 会员墙、社交、皮肤、AI 伴听  
- 改全局顶栏 Tab 数量  
- 本轮不强制恢复右侧传输通栏  
- 浅色/青绿 QQ 皮肤  

## 3. 相对上一版合同

| `2026-11-11-discover-online-listen-redesign` | 本轮 |
|-----------------------------------------------|------|
| 顶部分段推荐 \| 搜索对等 | **取消分段**；细搜常驻 |
| 推荐态无搜框 | 推荐流**上方仍有**细搜条 |
| 切分段不清查询 | **清除/Esc** 结束会话并回推荐流 |
| 默认进推荐分段 | 默认推荐流（无活跃搜索会话） |

实现上：可复用/演化 `DiscoverShellIndex` 为「有无活跃搜索会话」驱动，或重新用 `HasActiveSearchSession` 作为唯一壳开关；**不再**展示分段控件。

## 4. 信息架构

| 区域 | 行为 |
|------|------|
| 细搜条（吸顶） | 歌名 + 平台 + 搜索 + 清除 + 更多；推荐流与结果态共用 |
| 推荐流 | Banner → 快捷入口 → 歌单横滑 → 榜单轨；（主 CTA 降为芯片/附属） |
| 结果态 | 同一细搜条 +（可选）失败条 + 结果表 |
| 更多 | 艺人附加、模式（搜索/解析/识曲）、登录胶囊；默认收起 |
| 播放 | 封面/行/识曲 → 同一 `PlayerBar` |

状态机：

```
冷启动 / Clear / Esc
        → RecommendFeed
提交非空搜索（成功开始会话）
        → SearchResults
```

## 5. 推荐流组件合同

1. **Banner**：今日焦点视觉块；数据可先映射现有精选歌单/榜封面；点击走现有打开或试听。空/失败：短文案+重试，不整页变设置。  
2. **快捷入口**：榜单、歌单、识曲、粘贴（未就绪灰显）。榜单/歌单 → 滚到本页区块或打开歌单页；识曲/粘贴 → 展开更多并切 `DiscoverModeIndex`。  
3. **歌单墙 / 榜单轨**：复用现有 browse hub 数据源与打开逻辑；视觉改为更大封面、更明显分区标题与「全部/更多」。  
4. **续听 CTA**：不再用大块独占主按钮；放入 Banner 旁或搜条下芯片。  
5. **无 Hero 大搜、无登录胶囊矩阵、无模式表单**出现在推荐流默认首屏。  

## 6. 搜索态合同

1. 细搜条保持；结果表为主舞台。  
2. 统一源失败条 + Cookie/登录类长告警：**仅结果态**可见。  
3. 识曲成功：留在结果相关上下文；保留播放/下一首能力。  
4. 空结果：短引导；不做新热搜引擎。  
5. Clear/Esc：`HasActiveSearchSession=false`，回到推荐流。  

## 7. 视觉原则

- 背景/字色/控件：**现有深色 Design Token**。  
- 气质：分区标题清晰、封面偏大、横滑轨、圆角卡片；避免工具表单感。  
- 强调色：沿用 MusicVault 蓝系，不用 QQ 绿作为品牌识别。  

## 8. 迁移边界

- 优先改 [`SearchPageView.axaml`](src/MusicVault.App/Views/Pages/SearchPageView.axaml) + [`SearchPageViewModel.cs`](src/MusicVault.App/ViewModels/Pages/SearchPageViewModel.cs)。  
- [`DiscoverBrowseHubState`](src/MusicVault.App/Services/DiscoverBrowseHubState.cs)：改为「会话驱动」可见性（推荐流 ↔ 结果），删除/停用 shell 分段 API 的 UI 绑定。  
- Banner / 快捷入口：可先用现有数据拼装；无数据时隐藏整块。  
- 更新 [`PRODUCT_DESIGN.md`](docs/PRODUCT_DESIGN.md) §7.4。  
- 上一版分段相关 smoke 断言改为：无会话=推荐流；有会话=结果；Clear 回推荐流。  

## 9. 验收

1. 冷启动进发现：首屏是 Banner/封面流，不像探针台；可见细搜条。  
2. 搜索提交后：结果表；失败条可出现。  
3. 清除后回到推荐流。  
4. 识曲/粘贴不在推荐流默认露出。  
5. 顶栏 Tab 数不变；`publish-with-api.ps1 -Restart` 后肉眼过两态。  

## 10. 风险

| 风险 | 缓解 |
|------|------|
| Banner 无稳定数据 | 无数据隐藏；不阻塞歌单/榜单 |
| 细搜条与旧 Hero/Compact 双壳 | 只保留一套吸顶细条 |
| 用户找不到识曲 | 快捷入口 + 更多；不回工具台首屏 |
