# 发布准入与演练清单（Phase 3）

## 0) 一键演练（含自动记录）

- 推荐命令：`.\scripts\release-drill.ps1`
- 输出报告：`docs\execution\release-drill-*.md`
- 步骤日志：`docs\execution\release-drill-artifacts\<RunId>\*.log`
- 报告包含：环境信息、关键日志路径状态、失败步骤摘要
- 可选参数：
  - 仅校验不发布：`.\scripts\release-drill.ps1 -SkipPublish`
  - 跳过 smoke：`.\scripts\release-drill.ps1 -SkipSmoke`
  - 严格 smoke（禁止与 `-SkipSmoke` 同时使用）：`.\scripts\release-drill.ps1 -StrictSmoke`
- 内置步骤含 **design system audit**（``.\scripts\audit-design-system.ps1 -FailOnViolation``），与 CI 门禁一致

## 1) 发布前检查

- [ ] 工作区可构建：`dotnet build MusicVault.sln -c Release`
- [ ] 回归测试通过：`dotnet test MusicVault.sln -c Release`
- [ ] 格式校验通过：`dotnet format MusicVault.sln --verify-no-changes`
- [ ] 设计体系审计通过：`.\scripts\audit-design-system.ps1 -FailOnViolation`
- [ ] 设置页/设计系统 smoke：`.\scripts\smoke-settings-designsystem.ps1`
- [ ] 主导航/设置入口 smoke：`.\scripts\smoke-ui-navigation.ps1`
- [ ] Headless UI 冒烟：`.\scripts\smoke-ui-headless.ps1`
- [ ] 法律与许可证文件存在：`LICENSE` / `NOTICE` / `THIRD_PARTY_LICENSES.md`

## 2) Windows 发布演练（标准流程）

> 按项目规则，发布前必须先停止运行中的应用实例，避免文件占用。

1. 停止运行实例（可选显式执行）  
   `.\scripts\MusicVaultAppLifecycle.ps1 -Action Stop`

2. 发布并自动重启  
   `.\scripts\publish-with-api.ps1 -Restart`

3. 验证输出目录  
   - `publish\win-x64-vol\MusicVault.exe`
   - `publish\win-x64-vol\api\go-music-api.exe`
   - `publish\win-x64-vol\api\netease-cloud-music-api\`

4. 验证应用启动  
   - 主窗口可打开
   - 设置页可进入
   - 发现页可发起搜索（若 API 可用）

## 3) 发布后冒烟检查

- [ ] 顶栏导航可切换（曲库/发现/传输/工具/设置）
- [ ] 设置页“设计系统预览”可打开
- [ ] 下载队列可新增、暂停、继续、取消
- [ ] 构建产物包含法律文档（`docs/LEGAL.md` 已复制）

## 4) 失败回滚策略

1. 发布失败：保留当前已运行版本，不替换输出目录。
2. 启动失败：检查 `%LOCALAPPDATA%\MusicVault\startup-error.log`。
3. API 异常：先验证 `publish\win-x64-vol\api\` 内容完整，再回退到上个可用包。

## 4.1) 平台能力护栏（持续生效）

- 参考：`docs/execution/phase-4-capability-guardrails.md`
- 要求：不可用能力必须“禁用 + 文案提示”，禁止“可点不可用”。

## 5) 发布记录模板

建议每次发布记录以下信息：

- 发布时间：
- 分支 / 提交：
- 构建命令与结果：
- 测试命令与结果：
- smoke 结果：
- 发布包路径：
- 回滚包路径：

