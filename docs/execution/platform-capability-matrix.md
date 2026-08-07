# MusicVault 平台能力矩阵（Phase 3）

## 说明

- `完整`：默认可用且通过当前回归基线。
- `部分`：可用但存在平台能力缺口或降级行为。
- `不可用`：当前版本不支持。

## 能力矩阵

| 模块 | Windows 10/11 x64 | macOS 12+ | 备注 |
|---|---|---|---|
| 应用启动与导航 | 完整 | 完整 | Avalonia UI 跨平台一致 |
| 本地曲库扫描/索引 | 完整 | 完整 | SQLite + TrackRepository |
| 元数据读取/写入 | 完整 | 完整 | TagLib 路径一致 |
| 下载队列（暂停/继续/重试） | 完整 | 完整 | 依赖 API 与网络，不依赖平台音频 |
| 在线搜索与批量入队 | 完整 | 完整 | 依赖配置的 API 能力 |
| 内置播放器（本地播放） | 完整 | 部分→完整(可选) | macOS：AVFoundation 回退；捆绑 LibVLC（见 `libvlc-phase-3.md`）后格式面接近 Windows |
| 试听（在线预览） | 完整 | 部分 | 可走同一播放链路，受网络与音源可用性影响 |
| 同步歌词滚动 | 完整 | 部分 | 无真实播放时仅状态变化，不具听感同步 |
| 均衡器（10 段） | 完整 | 部分(LibVLC) | AVFoundation 下不可用；实际引擎为 LibVLC 时可开（以运行中 player 为准） |
| 内置 go-music-api 自启/更新 | 完整 | 不可用 | macOS 默认提示改用外部统一 API 地址 |
| 发布脚本（含 API 捆绑） | 完整 | 部分 | Windows：`publish-with-api.ps1`；macOS：`publish-macos.sh`（无 API 捆绑，需外部统一 API） |

## UI 能力提示策略（P2-1）

- 设置页「播放设置」提供平台能力状态块（本地播放 / 在线试听 / 均衡器 / 内置 API）。
- 不支持能力统一执行「禁用控件 + 说明文案」，避免“可点不可用”。
- 已落地禁用项：
  - 均衡器开关与入口（设置页 + 播放栏）
  - 内置 go-music-api 自动启动/更新相关开关与按钮

详见：`docs/execution/phase-4-capability-guardrails.md`

## 发布准入线（建议）

### Windows 发布必须项（Must Have）

1. `dotnet build MusicVault.sln -c Release` 通过；
2. `dotnet test MusicVault.sln -c Release` 通过；
3. `scripts/publish-with-api.ps1 -Restart` 执行成功并可启动；
4. 设置页与设计系统 smoke 脚本通过。

### macOS 发布必须项（Must Have）

1. `dotnet build MusicVault.sln -c Release` 在 macOS 通过；
2. `./scripts/publish-macos.sh osx-arm64` 成功，且输出目录含 `MusicVault` 与 `libMusicVaultPlayer.dylib`；
3. 若本机有 VLC.app：输出应含 `libvlc/osx-arm64/lib/libvlc.dylib`（或显式 `VLC_APP=...`）；
4. 设置页 / 「关于」平台能力说明与 `platform-capability-matrix.md` 一致；
5. Windows CI 可跑 `scripts/verify-macos-publish.ps1` 做脚本与文案对齐检查。

### 可延期项（Can Defer）

- macOS 无 VLC.app 时的便携 LibVLC 发行版自研打包；
- Linux 旁路 LibVLC / Flatpak；
- Windows 新装默认切到 LibVLC；
- 全量 UI 自动化回归。

