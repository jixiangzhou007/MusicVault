# Phase 0 质量门策略（Quality Gate）

## 目标

确保主干分支维持稳定、可回归、可预测的工程质量，避免新增技术债务。

## 质量门定义

1. **编译零告警**：`dotnet build MusicVault.sln -c Release` 必须为 `0 warning / 0 error`。
2. **测试全通过**：`dotnet test MusicVault.sln -c Release` 必须全部通过。
3. **格式校验通过**：`dotnet format MusicVault.sln --verify-no-changes` 必须通过。
4. **新增告警禁入主干**：任何 PR 不得引入 warning 增量。

## CI 执行策略

- 在 `.github/workflows/ci.yml` 中显式加入 `dotnet format --verify-no-changes`。
- CI 失败即阻断合并，不允许以“后续修复”为理由跳过。

## 本地执行建议（提交前）

```powershell
dotnet build MusicVault.sln -c Release
dotnet test MusicVault.sln -c Release
dotnet format MusicVault.sln --verify-no-changes
```

## 例外策略

- 若第三方依赖升级短期内引入不可控 warning，可临时记录在阶段文档并指定最晚清理时间。
- 例外必须经维护者确认，且不得影响主流程构建与测试通过。
