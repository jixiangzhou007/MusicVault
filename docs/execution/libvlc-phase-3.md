# LibVLC 引擎 Phase 3（打包与平台策略）

在 Phase 2（macOS 缺省 LibVLC / Windows opt-in）之后的发布与 Linux 规划。

## 目标

| 项 | 结论 |
|----|------|
| **macOS 捆绑** | `publish-macos.sh` 旁路复制完整原生；**优先 `/Applications/VLC.app`（或 `VLC_APP`）** |
| **Linux** | 暂不旁路打包；依赖发行版 `libvlc` + `LD_LIBRARY_PATH`；引擎入口仍为占位 |
| **Windows 新装默认** | **保持 NAudio**（opt-in LibVLC），待更长冒烟与回归数据再评估 |

## macOS 为何不用「纯 NuGet」

1. `VideoLAN.LibVLC.Mac` **无 3.0.21**；可用版本止于 **3.1.3.1**。
2. 该包仅含 `build/osx-x64/libvlc.dylib`，**无 `libvlccore` / plugins**，无法可靠初始化。
3. **Apple Silicon（osx-arm64）** 官方 NuGet 未提供原生；必须从 **VLC.app**（或等价 runtime）拷贝。

发布脚本布局（与 LibVLCSharp 探测一致）：

```text
publish/osx-arm64/libvlc/osx-arm64/lib/{libvlc,libvlccore}.dylib
publish/osx-arm64/libvlc/osx-arm64/plugins/   # 音频向裁剪
```

探测逻辑：`LibVlcPlayerSupport.TryResolveNativeRoot`（`osx-*` / `mac-*` / `lib` 子目录）。

### 本地发布步骤

```bash
# 先安装官方 VLC，再发布
./scripts/publish-macos.sh osx-arm64
# 或
VLC_APP=/path/to/VLC.app ./scripts/publish-macos.sh osx-x64
```

未找到 VLC.app 时脚本**不失败**，只警告并回退 AVFoundation。

## Linux 方案（规划，未落地运行时入口）

| 途径 | 说明 |
|------|------|
| **发行版包** | `apt install libvlc-dev vlc` / `dnf install vlc-devel`；进程继承系统 `.so` |
| **初始化** | LibVLCSharp：`Core.Initialize` 的 path 参数在 Linux **不受支持**，靠 `LD_LIBRARY_PATH` / rpath |
| **旁路捆绑** | 许可与 ABI 复杂；建议后续 Flatpak/AppImage 再议 |
| **代码状态** | `LibVlcPlayerSupport.IsPackagedForCurrentPlatform == false`；仍走 `NAudioStub` |

后续若做 Linux desktop 正式支持：先验证系统 LibVLC + `LibVlcPlayerService`，再打开 `IsPackagedForCurrentPlatform`。

## Windows 默认引擎评估

| 因素 | 现状 |
|------|------|
| 体积 | 音频裁剪后 libvlc ~ tens of MB，NAudio 更轻 |
| 稳定 / SMTC / 标签写 | NAudio 路径回归更久 |
| 格式面 | LibVLC 更全（APE/部分怪编解码） |
| 决策 | **新装仍默认 NAudio**；偏好中「可选 LibVLC」继续收集反馈 |

升默认的门槛（建议）：连续 2 周无关键 PlaybackFailed 热点；Seek/EQ/在线试听人工清单勾完；发布体积可接受。

## 交付清单

- [x] 修正 `VideoLAN.LibVLC.Mac` 版本为 `3.1.3.1`
- [x] App / Services 在 OSX 引用 Mac 包
- [x] `publish-macos.sh` 从 VLC.app 旁路捆绑 + 音频插件裁剪
- [x] `LibVlcPlayerSupport` 扩展 Mac 路径探测
- [x] Linux / Windows 默认策略文档化
- [x] NAudio / AVFoundation / Stub 对齐 `PlaybackFailed`（与 LibVLC 相同 UI 失败路径）
- [x] Phase 4：结构化 `playback-engine.txt`、设置页详情、诊断导出、单测（见 `libvlc-phase-4.md`）
- [ ] macOS 实机：`playback-engine.txt` = LibVLC；播放 / Seek / EQ
- [ ]（可选）Linux 冒烟 POC

## 相关

- Phase 1：`docs/execution/libvlc-poc.md`
- Phase 2：`docs/execution/libvlc-phase-2.md`
- 能力矩阵：`docs/execution/platform-capability-matrix.md`
