# Phase 4 · 平台能力护栏与验收基线

## 目标

将“平台能力提示 + 功能禁用策略”固化为长期护栏，确保后续迭代不会回归为“可点不可用”。

## 当前护栏范围（已落地）

- 设置页「播放设置」平台能力状态块：
  - 本地播放
  - 在线试听
  - 均衡器
  - 内置 API
- 不支持能力统一策略：
  - 禁用入口控件
  - 展示明确提示文案
  - 保存配置时避免写入无效启用状态
- 已执行的禁用项：
  - 均衡器（设置页开关/入口 + 播放栏入口）
  - 内置 go-music-api 自启/更新（设置页相关开关/按钮）

## 平台行为基线

### Windows 10/11 x64

- 本地播放：完整
- 在线试听：完整
- 均衡器：完整
- 内置 go-music-api 自启/更新：完整

### macOS 12+

- 本地播放：部分（AVFoundation 最小闭环）
- 在线试听：部分（依赖网络与音源）
- 均衡器：不可用（显式禁用 + 提示）
- 内置 go-music-api 自启/更新：不可用（显式禁用 + 提示）

## 验收清单（每次迭代至少执行一次）

1. 结构与绑定 smoke：
   - `.\scripts\smoke-ui-navigation.ps1`
2. 构建与测试：
   - `dotnet build MusicVault.sln -c Release`
   - `dotnet test MusicVault.sln -c Release --no-build`
3. 格式校验：
   - `dotnet format MusicVault.sln --verify-no-changes --verbosity minimal`

## 变更守则

- 新增平台相关能力时，必须同时提交：
  1) 能力判断（ViewModel 或 Service 层）  
  2) UI 禁用与提示文案  
  3) smoke 断言（至少 1 条）
- 若能力从“不可用/部分”升级为“完整”，必须同步更新：
  - `docs/execution/platform-capability-matrix.md`
  - 本文档对应条目
- 禁止仅做后端兜底而不做 UI 可见提示。

## 后续建议

- 将 `smoke-ui-navigation.ps1` 的能力断言拆分为独立函数，便于新增能力项时复用。
- 在 macOS CI 环境补一条“设置页能力状态快照”检查，减少人工验收成本。
