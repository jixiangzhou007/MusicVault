# Phase 1：整理工具页基础体验

## 阶段目标

1. 工作清单默认空，仅用户主动「载入到工作清单」
2. 统一「作用范围」：仅已选 / 整个工作清单（禁止零勾选误操作；整清单二次确认）
3. 工作清单分批展示 + 滚动加载 + 回到顶部
4. 文件整理执行前二次确认（并提示先预览）

## 任务清单

- [x] 移除选库自动载入；顶栏按钮改为「载入到工作清单」
- [x] 作用范围分段控件 + 各操作按范围解析目标
- [x] ResultsListPager 分批 + LoadMore + OnlineListScrollInteraction
- [x] 查找替换 / 刮削 / 整理 / 去重 确认与禁用逻辑
- [x] 空状态与摘要文案更新
- [x] 构建与发布验收

---

## 修改文件清单

| 路径 |
|------|
| `src/MusicVault.App/ViewModels/Pages/AudioToolsPageViewModel.cs` |
| `src/MusicVault.App/ViewModels/Pages/OrganizeWorkSetScope.cs` |
| `src/MusicVault.App/ViewModels/Dialogs/BatchScrapeConfirmContext.cs` |
| `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml` |
| `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml.cs` |
| `src/MusicVault.App/Views/MainWindow.axaml` |
| `docs/execution/phase-1-audio-tools.md` |

## 关键变更点

1. **空清单 + 主动载入**：切换音乐库不再触发查询；仅顶栏「载入到工作清单」或页内 `ApplyWorkSetFilter` 载入（最多 5000 首），成功 Toast。
2. **作用范围**：工具区 `IntSegmentedBar`（仅已选 / 整个工作清单）；`TryResolveScopeRows` 统一解析刮削、替换、整理、去重目标。
3. **仅已选 + 零勾选**：批量写操作禁止并提示切换范围或勾选。
4. **整个清单**：查找替换、文件整理执行、刮削确认弹窗均二次确认；>500 首时附加大批量提示。
5. **清单性能**：全量缓冲在 `ResultsListPager`，UI 绑定 `BatchScrapeDisplayed`（50 首/批）；滚动触底加载、右下角回到顶部。
6. **文件整理安全**：无变更则提示；有变更须确认弹窗（建议先预览）再 `ExecuteBatchAsync`。
7. **顶栏文案**：「搜索」→「载入到工作清单」；空状态说明同步更新。

## 风险与回滚点

| 风险 | 说明 | 回滚 |
|------|------|------|
| 习惯变更 | 老用户依赖「选库即载入」 | 恢复 `OnSelectedLibraryItemChanged` 内 `ScheduleWorkSetLoad()` |
| 仅已选默认 | 未勾选时无法一键整库刮削 | 默认 `WorkSetScopeIndex = 1` 或恢复隐式「未勾选=全部」 |
| 清单序号 | 分批展示下行号仍按全清单顺序编号 | 若需仅显示批次序号，改 `RenumberBatchScrapeQueue` |
| 删除曲目 | 从缓冲移除后重建整表显示，可能丢失滚动位置 | 可改为仅删行并同步 pager（Phase 2） |

## 验收命令及结果

### 1. 编译

```powershell
dotnet build "c:\Users\54560\MusicVault\src\MusicVault.App\MusicVault.App.csproj" -c Release
```

**结果：成功**（0 错误；既有 Core/Services 警告未新增）

### 2. 发布并重启

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

**结果：成功** — 已停止旧进程 → 发布至 `publish\win-x64-vol` → 已启动 `MusicVault.exe`

### 3. 单元测试（可选）

```powershell
dotnet test "c:\Users\54560\MusicVault\tests\MusicVault.Tests\MusicVault.Tests.csproj" -c Release
```

**结果：成功** — 282 通过，0 失败（Release）

## 手动验收建议（请你确认）

1. 进入「整理」→ 切换音乐库 → **清单仍为空**。
2. 点击「载入到工作清单」→ 出现曲目 + Toast。
3. 作用范围「仅已选」且无勾选 → 刮削/替换/整理按钮不可用或提示。
4. 切换「整个工作清单」→ 执行替换/整理/刮削 → **出现确认弹窗**。
5. 大清单（>50）→ 摘要显示「已显示/共」、滚动加载更多、回到顶部按钮。

---

**Phase 1 状态：待你确认后进入 Phase 2（IA 拆分 / 去重闭环等）。**
