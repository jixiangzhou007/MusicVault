# LibVLC 播放引擎 POC

## 状态

实验性开关已接入，**默认关闭**。开启后需**重启应用**才会切换到 LibVLC。

## 如何试用（Windows）

1. **偏好 → 播放 → 播放** 标签页
2. 打开 **「实验性 LibVLC 引擎」**
3. 点击 **保存设置**
4. **完全退出并重启** MusicVault
5. 能力芯片应显示 **「实验 · LibVLC」**
6. 用 MP3 / FLAC / APE / WMA 等本地曲目冒烟：播放、暂停、Seek、切歌、均衡器

## 架构

```
IAudioPlayerService
├── NAudioPlayerService          （Windows 默认）
├── MacAvFoundationPlayerService （macOS 默认）
└── LibVlcPlayerService          （settings.json → UseExperimentalLibVlcEngine）
```

- 启动时 `PlaybackEngineBootstrap` 在 DI 注册前读取 `settings.json`
- `AudioPlayerServiceFactory` 按偏好创建引擎；LibVLC 初始化失败时回退 NAudio / AVFoundation

## 已知限制（POC）

| 项 | 说明 |
|----|------|
| 发布体积 | `scripts/publish-with-api.ps1` 旁路复制后**剔除视频/GUI 等插件**（保留 access/codec/demux/audio_*） |
| 单文件发布 | `PublishSingleFile` **不会**自动带上 `libvlc/`；由发布脚本复制 `libvlc/win-x64` |
| 许可证 | LibVLC 为 LGPL-2.1+，见 `THIRD_PARTY_LICENSES.md` 与偏好→介绍开源致谢 |
| 在线预览 | LibVLC 路径支持 URL 直链试听；与 NAudio「先下临时 mp3」行为不同 |
| macOS | 已引用 `VideoLAN.LibVLC.Mac` 包，开关在 macOS 可见，需实机验证 |
| Linux | 无 NuGet 原生包，仍回退占位 NAudio stub |

## 验收清单

- [x] 开关默认关，不影响现有用户
- [x] `dotnet test --filter PlaybackEngineBootstrap`
- [x] 发布脚本复制 `libvlc/`
- [x] 开关即保存 +「当前引擎」显示
- [x] 开启后标记文件 `playback-engine.txt` = LibVLC（2026-07-15）
- [x] 发布音频向插件精简（~75MB）
- [x] `EncounteredError` → UI `PlaybackFailed` + `playback.log`
- [x] 扫描/播放扩展名补充 `.opus`
- [ ] 本地冒烟：播放 / Seek / 切歌 / 均衡器（人工）
- [ ] 播放中编辑标签 → 保存成功（人工；协调器已覆盖 LibVLC）
- [ ] Windows SMTC 仍正常（人工；SMTC 与引擎解耦）
- [x] `scripts/smoke-libvlc.ps1` 结构与单测冒烟

## 下一步（若 POC 通过）

1. ~~插件 cherry-pick 压缩发布体积~~  
2. ~~macOS 缺省优先 LibVLC；Windows 可选；能力页 + LGPL~~ → 见 `libvlc-phase-2.md`  
3. 人工冒烟：播放 / Seek / EQ / 标签写入 / SMTC  
4. ~~Phase 3：macOS 捆绑 / Linux 方案 / Windows 默认评估~~ → 见 `libvlc-phase-3.md`
