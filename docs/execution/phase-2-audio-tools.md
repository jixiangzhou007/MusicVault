# Phase 2：整理工具 IA 与去重闭环

## 阶段目标

1. 顶栏与子页按任务拆为 4 Tab（工作清单 / 补标签 / 整理文件 / 去重）
2. 主区改为左清单、右工具（清单为主区域）
3. 去重：重复组列表 + 默认仅删索引批量移除 + 删文件二次确认

## 任务清单

- [x] 四 Tab 与 MainWindow 顶栏同步
- [x] AudioToolsPageView 左右布局
- [x] DuplicateGroupItemViewModel + 扫描结果列表
- [x] 从库移除重复项（默认索引）+ 危险删文件
- [x] 构建 / 测试 / 发布验收

---

## 修改文件清单

| 路径 |
|------|
| `src/MusicVault.App/ViewModels/Pages/AudioToolsPageViewModel.cs` |
| `src/MusicVault.App/ViewModels/Pages/DuplicateGroupItemViewModel.cs`（新建） |
| `src/MusicVault.App/ViewModels/MainWindowViewModel.cs` |
| `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml` |
| `src/MusicVault.App/Views/MainWindow.axaml` |
| `tests/MusicVault.Tests/DuplicateGroupRulesTests.cs`（新建） |
| `docs/execution/phase-2-audio-tools.md` |

## 关键变更点

1. **四 Tab IA**：顶栏「工作清单 / 补标签 / 整理文件 / 去重」；默认进入「工作清单」说明页。
2. **左右布局**：左侧工作清单占主区域；右侧 300px 工具栏（作用范围 + 当前 Tab 工具）。
3. **补标签 Tab**：查找替换 + 元数据刮削合并；刮削选项改为 WrapPanel 适配窄栏。
4. **去重闭环**：扫描后展示重复组列表（保留较大文件）；「从库移除重复项」默认仅索引 + 确认弹窗。
5. **删文件**：「删除文件并移除」独立危险按钮 + `confirmIsDanger` 二次确认。
6. **保留策略**：`DuplicateGroupRules.PickKeeper` 按 `FileSizeBytes` 降序。
7. **清空/重载清单**时同步清空重复扫描结果。

## 风险与回滚点

| 风险 | 回滚 |
|------|------|
| 顶栏 Tab 变多，小屏需横向滚动 | 恢复 2 Tab 与 `SetOrganizeCategory(0/1)` |
| 去重键仅标题+艺术家+大小，非 AcoustId 指纹 | 增强 `FindDuplicatesInTracks`（Services 层，另阶段） |
| 批量删重复中途失败部分成功 | 已 Toast 成功/失败计数；可 Phase 3 加事务日志 |
| 右栏 300px 窄屏挤压 | 调 `ColumnDefinitions` 比例或最小宽度 |

## 验收命令及结果

### 编译

```powershell
dotnet build "c:\Users\54560\MusicVault\src\MusicVault.App\MusicVault.App.csproj" -c Release
```

**结果：成功**（0 错误）

### 测试

```powershell
dotnet test "c:\Users\54560\MusicVault\tests\MusicVault.Tests\MusicVault.Tests.csproj" -c Release
```

**结果：成功** — 284 通过（含 2 条 `DuplicateGroupRulesTests`）

### 发布

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

**结果：成功** — `publish\win-x64-vol\MusicVault.exe` 已启动

## 手动验收建议

1. 顶栏可见 4 个 Tab，切换右侧工具区随之变化。
2. 去重 Tab → 扫描 → 列表显示各组保留/移除行。
3. 「从库移除重复项」→ 确认文案含「不删除硬盘」。
4. 「删除文件并移除」→ 红色/危险确认，且仅在有文件路径时生效。

---

**Phase 2 状态：待你确认。确认后可进入 Phase 3（曲库「加入整理队列」、刮削套餐、任务向导等）。**
