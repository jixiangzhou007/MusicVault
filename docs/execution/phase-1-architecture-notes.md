# Phase 1 架构瘦身说明

## 目标

降低 `MainWindowViewModel` 与服务层耦合，收敛壳层编排职责，保持现有用户行为不变。

## 拆分前问题

- `MainWindowViewModel` 同时承担：
  - 导航编排（分区切换、页面映射、初始化分发）
  - 顶栏显示规则（状态条/二级浏览条）
  - 元数据侧栏恢复策略（跨分区关闭与恢复）
- `MainWindowViewModel` 直接绑定 `AppServices` 大量具体服务属性（下载、扫描、文件写入协调等）。

## 本阶段改动

### 1) 导航编排拆分（T-005）

- 新增 `ShellNavigationCoordinator`：
  - 负责导航前置动作（进入艺术家/专辑/全部歌曲时的附加逻辑）
  - 负责分区到页面的映射
  - 负责分区初始化入口委派
- `MainWindowViewModel` 中 `NavigateTo` / `InitializePageAsync` 改为调用协调器。

### 2) 壳层状态管理拆分（T-006）

- 新增 `ShellHeaderStateStore`：
  - 统一计算 `ShowHeaderStatusBadge` / `ShowMusicBrowseBar` / `ShowOrganizeBrowseBar` / `IsLibraryNavSelected`。
- 新增 `MetadataSidebarStateStore`：
  - 统一维护元数据侧栏的“离开关闭、返回恢复、手动关闭清除恢复标记”状态机。
- `MainWindowViewModel` 删除对应内联状态字段与判断逻辑。

### 3) AppServices Facade 化（T-007）

- `AppServices` 新增聚合访问器：
  - `Playback`（播放器 + 文件写入协调）
  - `Library`（曲库仓储/扫描/管理）
  - `Discover`（发现检索与资料刷新）
  - `Transfers`（下载队列与台账）
- `MainWindowViewModel` 优先改用 Facade（如 `services.Playback.FileWriteCoordinator`、`_services.Transfers.Downloads`、`_services.Library.Scanner`）。
- 旧公开属性保留，保证现有页面与服务构造不受破坏。

### 4) 编排测试补充（T-008）

- 新增测试：
  - `ShellNavigationCoordinatorTests`
  - `MetadataSidebarStateStoreTests`
  - `ShellHeaderStateStoreTests`
- 覆盖导航切换 Hook、侧栏恢复状态机、壳层显示规则关键路径。

## 移出职责清单（从 MainWindowViewModel）

1. 分区导航前置编排规则（迁移到 `ShellNavigationCoordinator`）
2. 分区初始化分发流程（迁移到 `ShellNavigationCoordinator` 委派入口）
3. 顶栏显示判定规则（迁移到 `ShellHeaderStateStore`）
4. 元数据侧栏恢复状态机（迁移到 `MetadataSidebarStateStore`）

## 风险与兼容策略

- 风险：导航编排拆分可能引入页面切换行为漂移。
  - 策略：保留原行为分支，使用定向测试验证 Hook 与初始化调用。
- 风险：Facade 切换期间引用路径混用。
  - 策略：旧属性不删除，增量迁移调用点，避免一次性破坏。

