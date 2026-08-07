# Phase 3：曲库打通、刮削套餐与任务向导

## 阶段目标

1. 全部歌曲页可将曲目加入整理工作清单并跳转整理页
2. 刮削「推荐 / 全部字段」套餐 + 高级选项折叠
3. 整理页任务向导条（可点击跳转 Tab）
4. 记住上次整理模板与刮削套餐（settings.json）

## 任务清单

- [x] AppSettings 整理工具偏好字段
- [x] AudioTools 入队 API + 向导 + 套餐 + 模板恢复
- [x] MainWindow / TracksPage 打通
- [x] 曲目列表右键菜单「加入整理队列」
- [x] 构建 / 测试 / 发布验收

---

## 修改文件清单

| 路径 |
|------|
| `src/MusicVault.App/Services/AppSettingsService.cs` |
| `src/MusicVault.App/ViewModels/Pages/OrganizeEnqueueEventArgs.cs`（新） |
| `src/MusicVault.App/ViewModels/Pages/OrganizeWorkSetMerge.cs`（新） |
| `src/MusicVault.App/ViewModels/Pages/AudioToolsPageViewModel.cs` |
| `src/MusicVault.App/ViewModels/Pages/TracksPageViewModel.cs` |
| `src/MusicVault.App/ViewModels/MainWindowViewModel.cs` |
| `src/MusicVault.App/Views/Pages/AudioToolsPageView.axaml` |
| `src/MusicVault.App/Views/Pages/TracksPageView.axaml` |
| `src/MusicVault.App/Views/Pages/TracksPageView.axaml.cs` |
| `src/MusicVault.App/Views/TrackListPageOptions.cs` |
| `src/MusicVault.App/Views/TrackListInteraction.cs` |
| `tests/MusicVault.Tests/OrganizeWorkSetMergeTests.cs`（新） |
| `docs/execution/phase-3-audio-tools.md` |

## 关键变更点

1. **曲库 → 整理**：`EnqueueTracksAsync` 支持追加合并（按曲目 Id 去重）；`MainWindowViewModel.OpenOrganizeWithTracksAsync` 导航并入队。
2. **全部歌曲页**：顶栏「整理」菜单（当前列表加入 / 打开整理工具）；右键「加入整理队列」；超 500/5000 首二次确认。
3. **来源标注**：`WorkSetSourceHint` 写入摘要（如「来自「全部歌曲」」）。
4. **刮削套餐**：推荐（不含作曲）/ 全部字段；推荐下可勾选「显示高级字段」。
5. **偏好持久化**：`OrganizeScrapePresetIndex`、`OrganizeShowAdvancedScrape`、文件夹/文件名模板索引写入 `settings.json`。
6. **任务向导**：清单上方四步按钮 + 动态提示；刮削/整理/去重成功后标记对应步骤。
7. **单元测试**：`OrganizeWorkSetMergeTests` 验证入队去重合并。

## 风险与回滚点

| 风险 | 回滚 |
|------|------|
| 右键菜单改为 flyout（仅 Tracks 页） | `ShowOrganizeContextMenu = false` |
| 当前列表入队最多 5000 首 | 调低 `MaxOrganizeEnqueueCount` |
| settings 新字段旧版忽略 | 删除 AppSettings 新属性即可 |
| 向导步骤仅会话内标记 | 清空 `Wizard*Done` 相关逻辑 |

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

**结果：成功** — 285 通过（+1 `OrganizeWorkSetMergeTests`）

### 发布

```powershell
.\scripts\publish-with-api.ps1 -Restart
```

**结果：成功**

## 手动验收建议

1. 全部歌曲 → 右键单曲「加入整理队列」→ 跳转整理页且清单有歌、摘要含来源。
2. 「整理」→「当前列表加入整理」→ 追加到已有清单（不重复 Id）。
3. 补标签 Tab → 切换「推荐/全部字段」→ 重启应用后选项仍在。
4. 清单上方向导按钮可切换 Tab；完成刮削/整理/扫描后提示文案变化。

---

**Phase 3 状态：待你确认。**
