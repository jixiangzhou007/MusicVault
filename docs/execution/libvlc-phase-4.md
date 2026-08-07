# LibVLC 引擎 Phase 4（诊断与可观测性）

在 Phase 3（macOS 旁路捆绑 / Linux 规划 / Windows 保持 NAudio 默认）之后的硬化阶段。

## 目标

| 项 | 结论 |
|----|------|
| **启动快照** | `playback-engine.txt` 结构化字段，兼容旧 tab 格式 |
| **设置页** | 「当前引擎」下显示原生路径或回退原因 |
| **诊断导出** | `ExportBundle` 附带 `playback-engine.txt` |
| **测试** | 快照读写、工厂回退、有原生时 LibVLC 尝试 |
| **Windows 默认** | **仍不改为 LibVLC** |

## 快照格式

路径：`%LocalAppData%\MusicVault\playback-engine.txt`

```text
utc=2026-07-16T08:00:00.0000000Z
engine=LibVLC|NAudio|AVFoundation|NAudioStub
prefer=true|false
nativeRoot=(none)|<绝对路径>
note=<回退原因，可为空>
```

旧格式 `时间戳\t引擎[\t备注]` 仍可被 `TryRead` 解析。

## UI

- **设置 → 播放与音频 → 播放**：「当前引擎」+ 灰色 caption  
  - LibVLC 成功：`原生库：…`  
  - 回退：`回退原因：…`  
  - 已请求但无目录：`已请求 LibVLC，但未找到原生库目录`

## 测试

```powershell
dotnet test tests/MusicVault.Tests/MusicVault.Tests.csproj --filter "FullyQualifiedName~PlaybackEngineSnapshot|FullyQualifiedName~AudioPlayerServiceFactory"
```

`Create_WhenNativePresent_*` 仅在发布目录旁路含 `libvlc/win-x64` 时断言 LibVLC；否则跳过断言细节。

## 交付清单

- [x] `PlaybackEngineSnapshot` + 工厂写入
- [x] 设置页详情 + 诊断导出
- [x] 单测
- [x] 启动写入 `[Engine]` 到 `playback.log`
- [x] `scripts/smoke-libvlc.ps1`（结构 + 发布包 + 相关单测）
- [x] 标签写入协调器注释对齐全引擎（SMTC 仍走 PlayerBar，与引擎无关）
- [ ] 人工：开关 LibVLC → 重启 → 详情与 `playback-engine.txt` 一致
- [ ] 人工：播放 / Seek / EQ / 播放中写标签 / SMTC
- [ ] macOS 实机（Phase 3 遗留）

## 冒烟脚本

```powershell
.\scripts\smoke-libvlc.ps1
# 仅结构检查：
.\scripts\smoke-libvlc.ps1 -SkipTests
```

## 相关

- Phase 3：`docs/execution/libvlc-phase-3.md`
- 故障排查导出：`PlaybackDiagnostics.ExportBundle`
