# 1.1 发版冗余说明（审计备忘，非用户文档）

发版不以「清干净每一处死代码」为门槛。以下供后续 PR 消化。

## 建议保留

- `docs/superpowers/**` — 设计/施工史，贡献者有用；访客看 `docs/README.md` 即可
- `DesignSystemPage*` — DEBUG / 预览用途，设置里已 `#if DEBUG` 门控
- NAudio / LibVLC / AVFoundation 多引擎 — 平台能力需要，不是重复实现

## 可后续清理（勿堵 1.1 发版）

| 项 | 说明 |
|----|------|
| `TrackRepository` 上 `[Obsolete]` API | 确认无调用后删除 |
| `MiguMusicProvider` / `BaseApiMusicProvider` 的 `NotImplementedException` 路径 | 实验平台诚实失败；可改为统一 `ServiceMessage` |
| `BundledNetEaseApiHost` 与统一 API 双栈 | 产品仍需要；文档已说明 |
| 根目录历史 `tmp-*` | 已清理并 gitignore |
| `docs/execution/design-system-audit-*.md` | 已批量删除并忽略；改跑脚本生成 |

## 不做大规模机械删除的原因

未提交改动面极大，盲目删类易破坏登录/发现/设置路径。发版优先：版本号、README、测试绿、朋友包可启动。
