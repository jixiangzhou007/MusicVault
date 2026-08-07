# MusicVault 手动回归清单

发布前在 Windows 上执行 `scripts/publish-with-api.ps1`，用 `publish/win-x64-vol/MusicVault.exe` 验证。

## P0 — 播放与启动

- [ ] 冷启动 3 秒内主界面可点击（无长时间白屏）
- [ ] 双击本地曲目可出声；进度条走动
- [ ] 关闭再开：仅恢复上一首 UI/进度，不卡死
- [ ] 播放失败时底栏有明确错误文案（非静默）
- [ ] 「偏好」→ 导出诊断包，含 `playback.log` 尾部

## P0 — 播放历史

- [ ] 首页右侧标题为「播放历史」
- [ ] 连续点播：历史列表增长；同一曲不重复（单曲循环除外）
- [ ] **重启应用**后仍显示最近播放历史（最多 **30** 首）
- [ ] 清空历史后重启，历史列表为空

## P0 — 平台能力

- [ ] 发现页显示各平台能力芯片（完整/可下载/仅搜索/未配置/已关闭）
- [ ] 「偏好」测试统一 API / 网易云 API 有成功或失败提示

## P0 — 错误可见

- [ ] 下载完成但刷新曲库失败时，顶栏状态条有警告
- [ ] 「偏好」底部可见最近事件列表

## P1 — 传输

- [ ] 顶栏「传输」：表头 + 虚拟列表与曲库一致
- [ ] 「重试全部失败」「清除已完成」可用

## P1 — 发现 / 导航

- [ ] 发现右侧为传输速览；「查看全部」跳转传输详页
- [ ] 顶栏无「概览」；音乐库概览在「偏好」

## P2 — 下载控制

- [ ] 传输页进行中任务可 **暂停 / 继续 / 取消**（单行按钮）
- [ ] 工具栏 **全部暂停 / 全部继续 / 取消进行中** 可用
- [ ] 曲库浏览条输入后约 0.35s 自动筛选（无需每次点「搜索」）
- [ ] `dotnet test tests/MusicVault.Tests` 通过

## P3 — 发现与偏好

- [ ] 「偏好」切换界面字号后保存，列表/按钮文字明显变大或恢复
- [ ] 发现页顶部：**功能说明 + 合规提示** 合并为同一介绍区（两行，无独立横幅）
- [ ] 解析歌单/专辑后出现 **全部加入下载**，状态栏显示 `批量入队 x/y…`
- [ ] 传输页失败行可 **复制失败原因**（剪贴板图标）

## P4 — 向导、历史与基准

- [ ] 发现页「全部加入下载」先弹出 **确认向导**（显示总数 / 可入队数 / 音质）
- [ ] 解析歌单/专辑后向导标题含 **平台 · 类型**（如「网易云 · 歌单」）
- [ ] 首页播放历史：**双击** 或选中行可切歌；**清空** 按钮可用（保留当前播放）
- [ ] `dotnet test tests/MusicVault.Tests` 含 `DownloadQueueServiceLifecycleTests`
- [ ] `powershell -File scripts/benchmark-library.ps1` 通过（5000 曲搜索 &lt; 3s）
- [ ] 「关于」中 macOS 播放能力说明已更新

## P4 — 播放栏

- [ ] 底栏 **播放/暂停、上一首/下一首、循环/随机** 可点击
- [ ] 底栏 **音量、队列、歌词、均衡器** 可点击（均衡器打开时除外）
- [ ] 拖动进度条后松开，其余按钮仍可正常点击

## P5 — 发现批量入队与对比度

- [ ] 发现页解析歌单/专辑（≥2 首）后 **自动弹出** 批量入队确认向导（默认开启）
- [ ] 偏好 → 下载策略 → **解析后向导** 开关可关闭自动弹出
- [ ] 结果列表工具栏 **全部加入下载（当前音质）** 可用，确认后状态栏显示 `批量入队 x/y…`
- [ ] 设置/曲库 browse 标签等 **浅蓝底** 选中态文字清晰（非蓝底蓝字）
- [ ] 设计预览页色板含 **OnAccentSoft** 示例
- [ ] **偏好 → 界面语言 → English** 后，在线源页字段/按钮/表头/传输速览切换为英文
- [ ] 切换 English 后，**播放栏** ToolTip（队列/歌词/循环/随机/音量）与「Not playing」空闲标题正确
- [ ] 切换 English 后，**曲库 → 歌曲** 侧栏/搜索/整理/我喜欢、列表摘要与空状态为英文
- [ ] 切换 English 后，**专辑 / 艺术家** 页眉说明、搜索栏、空状态、播放全部/资料更新等高频按钮为英文

## P4 — 列表连续播放

- [ ] 在「歌曲 / 我喜欢 / 专辑 / 艺术家 / 播放列表」**双击**某首：底栏 **下一首** 在该列表内顺序前进（非播放历史）
- [ ] 开启 **随机** 后下一首从**当前列表**随机选取
- [ ] 切换到另一列表并双击播放后，下一首跟随**新列表**（不沿用旧列表）
- [ ] 从首页 **播放历史** 点播后，上一首/下一首回到历史模式（不再沿用旧列表）

## P6e — 服务层错误消息本地化

- [ ] 切换 **English** 后，传输页失败行 ToolTip / 复制错误内容应为英文（如 `Previous download incomplete; retry available`）
- [ ] 发现页搜索空关键词：错误提示随语言切换（`Discover.KeywordRequired`）
- [ ] 发现页搜索有结果时，状态栏 diagnostics 随语言切换（如 `NetEase 5 tracks` / `Unified API (go-music-api) 3 tracks`）
- [ ] **偏好 → 高级**：统一 API / 网易云 API「测试连接」失败文案随语言切换
- [ ] **偏好 → 更新与诊断**：API 探针 Detail、识曲环境测试、登录诊断随语言切换
- [ ] 歌单页 **榜单** 加载失败空态描述随语言切换
- [ ] 艺术家详情资料加载失败/未找到：状态文案随语言切换
- [ ] **整理 → 批量刮削**：失败行 ToolTip / 结果弹窗 Reason 随语言切换（如 `No usable online song match found`）
- [ ] **平台登录抽屉**：初始提示、Cookie 摘要、扫码状态随语言切换
- [ ] Provider 下载失败（酷狗/酷我/QQ 等）异常经传输或发现页展示时为英文
- [ ] `dotnet test tests/MusicVault.Tests --filter ServiceMessageCodec` 通过

## P6d — macOS 发布 / 能力文案（V6d）

- [ ] Windows：`powershell -File scripts/verify-macos-publish.ps1` 通过
- [ ] macOS：`./scripts/publish-macos.sh osx-arm64` 后 `publish/osx-arm64/` 含 `MusicVault` 与 `libMusicVaultPlayer.dylib`
- [ ] **偏好 → 播放设置 → 平台能力**：macOS 缺 dylib 时本地播放为「不可用」+ 说明；有 dylib 为「部分」；在线试听详情含 macOS 外部 API 提示
- [ ] **关于** 对话框 macOS 文案与设置页能力矩阵一致（非旧版「尚未接入播放」）
- [ ] 切换 English 后，能力状态 pill 颜色正确（Full / Partial / Unavailable）

## P6c — AcoustID 测试 / 扫描反馈（V6c）

- [ ] **偏好 → 数据与备份 → AcoustID**：点击「测试 lookup」，选择本地音频；未启用/缺 Key/缺 fpcalc 时 pill 显示对应错误；就绪时成功 pill 显示 AcoustID，无匹配时显示「未匹配」
- [ ] 启用 lookup 后扫描曲库：进度条旁/页头状态在 lookup 进行中显示 `AcoustID 匹配数/尝试数`（如 `3/12`）
- [ ] 扫描完成 toast 摘要含 `AcoustID lookup x/y`（仅当 y > 0）
- [ ] `dotnet test tests/MusicVault.Tests --filter AcoustIdFingerprintLookup` 通过

## P6b — 工具栏 A11y / 批量勾选（V6b）

- [ ] Narrator 聚焦 **Transfers / Tracks / Home / Discover / Playlists** 页头工具栏按钮，应朗读与 ToolTip/按钮文案一致
- [ ] 侧栏 **播放队列** 批量模式：全选/取消/移除按钮可识别；行内勾选框朗读「选择 {曲名}」
- [ ] 元数据侧栏：**保存全部**、收藏、打开文件夹按钮可识别
- [ ] 传输列表行勾选框、专辑详情批量下载勾选框可识别

## P6a — 传输 / 在线曲目 i18n（V6a）

- [ ] **偏好 → 外观 → 语言** 切换 English 后保存，打开 **Transfers**：列表状态 chip、行内 ToolTip、清除/取消 Popconfirm 均为英文
- [ ] 发现页 / 歌单在线曲目：收藏 ToolTip、试听缓存标签、专辑详情「Downloaded / Not downloaded」随语言切换
- [ ] 清除单条下载记录对话框（Modal）标题与按钮文案已本地化

## P6 — 无障碍 / 字号 / AcoustID（V5）

- [ ] **偏好 → 外观 → 界面字号** 切换「较大 / 特大」后保存，页头标题与工具栏按钮同步变大
- [ ] 屏幕阅读器 / Narrator：顶栏导航 Tab、底栏播放控制、进度条可读（进度条 HelpText 含当前时间）
- [ ] 设置页左侧导航项、Ant 卡片 Tab 可被朗读器识别
- [ ] 曲库 / 歌单 / 队列 / 传输列表行内操作按钮（收藏、加歌单、删除等）可被朗读器识别（`AutomationProperties.Name` 与 ToolTip 一致）
- [ ] **偏好 → 数据与备份 → AcoustID 指纹 lookup**：开关 + API Key 保存后状态文案正确（未启用 / 缺 Key / 缺 fpcalc / 已就绪）
- [ ] （可选）安装 [Chromaprint fpcalc](https://acoustid.org/chromaprint)，在设置页启用 lookup 并填写 API Key（或设置 `MUSICVAULT_ACOUSTID_API_KEY`）后，扫描曲库可为无标签曲目写入 AcoustID，首页「相似」推荐增多
- [ ] `dotnet test tests/MusicVault.Tests --filter AcoustIdFingerprintLookup` 通过
