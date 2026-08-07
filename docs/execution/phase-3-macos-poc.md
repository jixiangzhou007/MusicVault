# Phase 3 · macOS 播放 POC 方案对比

## 目标

明确 macOS 平台从“占位播放器”走向“可用播放链路”的技术路线，给出推荐方案与风险。

当前现状：

- `NAudioPlayerService` 在非 Windows 分支为占位实现（仅状态切换，不输出声音）。
- `IAudioPlayerService` 接口已稳定，调用方（PlayerBar/RightSidebar）可复用。

## 方案 A：NAudio 替代路线（跨平台托管解码）

### 思路

- 保持 `IAudioPlayerService` 不变，新增 `MacAudioPlayerService`。
- 使用 ffmpeg 解码 + 跨平台音频输出组件（例如 OpenAL/SDL 封装）实现播放。

### 优势

- 与现有 `AudioDecoderFactory` 逻辑接近，业务接线改动小。
- 可复用现有“解码优先 + 失败回退”策略。

### 风险

- 第三方跨平台音频输出库维护成本高，边缘设备兼容性不可控。
- 打包复杂度高（额外 native 依赖 + 签名/沙箱限制）。

### 结论

- 作为备选方案，不建议作为首选落地。

---

## 方案 B：AVFoundation 原生路线（推荐）

### 思路

- 在 macOS 下引入 `AVFoundation`（通过 `Objective-C` 绑定或 `Xamarin.Mac` 兼容层）实现原生音频播放。
- 维持 `IAudioPlayerService` 接口，实现 `MacAvFoundationPlayerService`。
- 保留 Windows `NAudioPlayerService` 不变。

### 优势

- 原生音频栈，稳定性、功耗和系统兼容性更好。
- 更符合 macOS 后续上架与签名要求。

### 风险

- 首次接入需要平台特定工程配置（运行时绑定、构建目标）。
- 均衡器（Equalizer）能力需阶段性降级或另行实现。

### 结论

- 推荐作为 Phase 4+ 正式路线。
- 建议按“先可播、后增强”推进（先实现播放/暂停/Seek，再补 EQ）。

---

## 建议实施步骤（两阶段）

### 阶段 1：可播最小闭环

1. 新增 `MacAvFoundationPlayerService`，实现 `Play/Pause/Resume/Stop/Seek`。
2. 在 DI 中根据平台注册 `IAudioPlayerService` 的具体实现。
3. 验证本地文件播放、预览播放、播放结束事件、位置更新。

验收标准：

- macOS 可正常播放本地 MP3/FLAC；
- `TrackEnded` 和 `PositionChanged` 事件行为与 Windows 一致。

### 阶段 2：体验增强

1. 补齐预览下载进度与中断恢复策略。
2. 评估均衡器接口在 macOS 的降级策略（功能开关/提示文案）。
3. 对齐设置页文案与能力提示。

验收标准：

- 设置页能准确展示 macOS 播放能力状态；
- 不支持项有明确提示，不出现“可点不可用”。

