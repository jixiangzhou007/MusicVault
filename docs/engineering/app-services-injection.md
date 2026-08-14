# AppServices 注入约定

`AppServices` 是兼容层容器，**新页 / 新 ViewModel 不要整包注入 `AppServices`**，除非确实跨多个域且短期无法拆。

## 优先注入

| 域 | 注入类型 | 典型用途 |
|----|----------|----------|
| 曲库 | `AppLibraryFacade`（`services.Library`） | 曲库页、NAS/WebDAV UI 辅助 |
| 播放 | `IAudioPlayerService` 或 `AppPlaybackFacade` | 均衡器、播放控件 |
| 发现 | `AppDiscoverFacade`（`services.Discover`） | 搜索/采集相关 |
| 传输 | `AppDownloadFacade`（`services.Transfers`） | 下载队列/历史 |
| 应用层 | `I*ApplicationService` | 已有 Application 契约时优先 |

## 已落地示例

- `LibrariesPageViewModel(AppLibraryFacade library, …)`
- `EqualizerViewModel(IAudioPlayerService player, …)`（不取整包 `AppServices`）
- `NasLibraryUiHelper` 参数为 `AppLibraryFacade`

## 改造存量页时

1. 先列出该 VM 实际用到的 `_services.X` 成员。
2. 若集中在单一 Facade，改为构造注入该 Facade（或 Facade 上的具体服务）。
3. 仅保留 `DataDirectory` / `Platform` 等 Facade 未覆盖的根级能力时，可继续拿 `AppServices`，或后续再抽薄接口。

## 反例

```csharp
// 避免：曲库页只碰库相关能力却注入整包
public LibrariesPageViewModel(AppServices services) { _services = services; }
```

```csharp
// 推荐
public LibrariesPageViewModel(AppLibraryFacade library, …) { _library = library; }
```
