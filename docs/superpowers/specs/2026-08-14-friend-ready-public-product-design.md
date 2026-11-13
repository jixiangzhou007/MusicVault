# 朋友可用的公开产品（方案 1）

日期：2026-08-14  
状态：已并入项目级规范；本文保留为实现切片  
项目级规范：[docs/PRODUCT_DESIGN.md](../../PRODUCT_DESIGN.md)（意图、IA、旅程、分发、批次）  
前置：双核（本地听与管 + 跨平台在线听 / 导歌单）。下载不合规、尽力而为；Mac 以在线播放为合格线。

## 目标

让朋友解压后**不用你远程教学**就能完成第一次成功：

- 选「我已有歌曲」：添加文件夹后 **1 分钟内出声**，不要求网络。
- 选「我想找歌」：能搜到结果并入队；失败时有下一步，不是空白页。
- 不要求读 README、不要求配环境变量、不要求自己编译。

对外身份：个人本地音乐库。发现/下载是向导里的一条路，不是安装包封面故事。

## 范围

1. **首次向导**：欢迎 → 选路 → 只问这条路需要的东西。
2. **空首页主按钮**：跟随向导选择，不再一律「去发现」。
3. **两个安装包**：Windows x64、macOS Apple Silicon。不是 x86，不是 iOS。
4. **对外文案**：README / GitHub Release / 关于页与真实能力对齐。

## 非目标（本规范不实施）

- iOS、Windows 32 位、Windows ARM、macOS Intel
- Mac 捆绑 `go-music-api` / 你架公共 API 服务器（那是方案 3 / 方案 2）
- 设置页 7 项导航重构、自动保存大改
- 识曲交互重写、新平台源、新远程协议、Linux
- Apple 公证/签名（第一版用「右键打开」说明书）
- 把发现从产品里删掉

## 用户与成功标准

| 谁 | 系统 | 第一次成功 |
|----|------|------------|
| 朋友 A | Windows 10/11 x64 | 已有文件夹 → 播；或打开就搜到歌 |
| 朋友 B | Mac M 系列 | 已有文件夹 → 播；搜歌可用（源少于 Windows） |

你自己继续当高级用户。本规范不削弱已有整理/NAS/设置能力，只改**第一次到达**和**发给谁什么包**。

## 方案摘要

同一套向导，能力按平台分级：

- **Windows + 找歌**：静默启用捆绑 API（有 `api/` 时），朋友看不到 exe 名。
- **Mac + 找歌**：走现有酷狗/酷我直连；可展开填统一 API 地址（可选）。
- **两端 + 已有歌**：只选文件夹；下载目录用默认路径，不单独占一步。

## 向导流程

版权声明保持现状（向导之前）。已有曲库则跳过向导（现逻辑保留）。

```
欢迎
  → 选路：我已有歌曲 | 我想找歌
      → 已有歌曲：选文件夹 → 开始
      → 我想找歌：确认搜索方式（平台文案不同）→ 开始
```

步数固定为 3（欢迎 / 选路 / 路径详情）。删除当前第 3 步「下载目录」、第 4 步「内置 API 开关」作为独立步。

### 步 0 · 欢迎

- 一句话：把歌曲放进自己的曲库，整理后播放。
- 不讲 API、不讲平台列表。
- 可「跳过向导」：写入 `FirstRunWizardCompleted=true`，`FirstRunOnboardingPath=Unset`。

### 步 1 · 选路（必选）

两个竖排选项卡片（对话框现宽 520–600，不要改成新设计体系）：

| 选项 | 存盘值 | 说明（朋友能看懂的） |
|------|--------|----------------------|
| 我已有歌曲 | `LocalLibrary` | 选择电脑上的音乐文件夹，马上播放 |
| 我想找歌 | `Discover` | 搜索并下载到本机曲库 |

未选中时「下一步」禁用。选中态用现有 `AccentSoft` / 主色边，禁止新圆角规范。

### 步 2 · 路径详情

**LocalLibrary**

- 选文件夹（现有文件夹选择器）。
- 允许跳过本步：路径仍为 `LocalLibrary`，空首页主按钮是「添加音乐文件夹」。
- 选了有效文件夹则 `LibraryManager.AddOrGetLibraryAsync`（现逻辑）。

**Discover · Windows 且安装包带 `api/`**

- 正文：「打开后即可搜索下载，一般不用再配置。」
- `AutoStartBundledMusicApi` / `AutoStartBundledNetEaseApi` 在有对应捆绑物时默认为 true，**界面不出现 exe / Node 名称**。
- 不测连接也能点「开始」；健康检查失败不挡完成。

**Discover · Mac，或 Windows 无捆绑 API**

- 正文：「将使用在线搜索（部分平台可用）。网易云等需要搜索服务地址，可稍后在设置里填。」
- 默认折叠：「我有服务地址」（写入现有统一 API 设置字段）。
- 不自动启动不存在的捆绑进程。

**两端共通、静默完成**

- 下载目录：沿用现默认（桌面 `MusicDownload` 或已有设置），不单独提问。
- `FirstRunWizardCompleted=true`。

## 设置与空首页

在 `AppSettings`（`settings.json`）新增：

```text
FirstRunOnboardingPath: Unset | LocalLibrary | Discover
```

缺省 / 旧用户无此字段 = `Unset`。

空首页**主按钮**规则（只改主 CTA，次要入口仍保留添加文件夹 / 发现 / 设置）：

| `FirstRunOnboardingPath` | 已有音乐库？ | 主按钮 | 命令 |
|--------------------------|--------------|--------|------|
| `LocalLibrary` | 否 | 添加音乐文件夹 | 现 `OpenLibrariesCommand` |
| `LocalLibrary` | 是 | 查看曲库 | 现 `OpenAllTracksCommand` |
| `Discover` | （任意） | 去发现 | 现 `OpenOnlineSourcesCommand` |
| `Unset` | 否 | 添加音乐文件夹 | `OpenLibrariesCommand` |
| `Unset` | 是 | 查看曲库 | `OpenAllTracksCommand` |

`Unset` 走本地优先：公开产品默认故事是「先有自己的文件」，避免跳过向导的朋友被直接丢进发现页。

## 安装包

只发两个资产，文件名带版本号：

| 资产 | 内容 | 如何产出 |
|------|------|----------|
| `MusicVault-X.Y.Z-win-x64.zip` | 现有 `publish/win-x64-vol`（含 `api/`） | 本机 `publish-with-api.ps1` 或 Release 工作流 |
| `MusicVault-X.Y.Z-osx-arm64.zip` | `publish/osx-arm64`，**必须含** `libMusicVaultPlayer.dylib` | Mac 或 GitHub `macos-latest` 跑 `publish-macos.sh osx-arm64` |

禁止：

- 一个万能包
- iOS 包
- win-x86 / osx-x64 作为第一版资产（Intel Mac 朋友口头说明暂不支持）

Mac 包合格线：朋友双击能启动 + 本地文件能出声。LibVLC / VLC.app 为增强（格式更全），**不作为**第一版 CI 硬门槛；GitHub `macos-latest` 没有 VLC.app，以 AVFoundation dylib 为准。

Windows Release 工作流保持现有 Velopack。Mac zip 作为同一 Release 的第二资产追加；没有 Mac runner 成功产物时，**仍可只发 Windows**，不要挡 Windows 朋友。

发给 Mac 朋友的三行说明（写入 Release 说明，不进向导）：

1. 下载 `osx-arm64` 包，不要下 Windows 包。
2. 若提示「无法打开」：访达中右键 → 打开。
3. Intel Mac（2019 及更早）第一版不支持。

## 对外文案（必须改，避免超卖）

| 位置 | 改成 |
|------|------|
| README 推荐平台 | Windows 10/11 x64 完整；macOS Apple Silicon 可播、可搜（源较少） |
| README 功能表 P0 网易云 | 完整（Windows 捆绑 API）；Mac 需自填或直连其它源 |
| `docs/GITHUB_RELEASE.md` | 两个资产；删掉「macOS 播放器占位无法出声」若与现状不符 |
| 关于 / 平台能力 | 与 `platform-capability-matrix.md` 一致：Mac 无内置 API |
| CONTRIBUTING Issues 链接 | 改成真实仓库，去掉 `your-org` 占位 |

口号保留「下载即整理，整理即播放」。封面故事顺序改为：本地曲库 → 整理播放 → 可选发现。

## 错误与降级

- 向导保存失败：留在对话框，现有 `Wizard.FirstRun.SaveFailed`。
- 找歌之后发现页全源失败：沿用现有失败 banner + 空态 CTA（去设置），本规范不重做搜索韧性。
- Mac 无 dylib：关于页已有说明；**发给朋友的 zip 不得出现这种情况**，发布检查清单增加「zip 内含 dylib」。
- 杀毒误报捆绑 API：Release 说明保留 Defender 排除提示。

## 测试

- 向导步状态机：选路未选不能下一步；两条路径的 `LastStepIndex=2`；跳过整段向导写入 `Unset`。
- 完成 LocalLibrary 后设置字段与库记录。
- 完成 Discover 后 Windows 有捆绑时自动启动开关为 true；无捆绑时不为不存在的 exe 置 true。
- 空首页主按钮文案/命令随 `FirstRunOnboardingPath` + 是否有库变化（Headless 或 VM 单测）。
- 现有 `MainWindowHeadlessSmokeTests`、向导相关测试不回退。

不在本规范要求：Mac 实机 UI 自动化（无稳定环境时用发布清单人工勾选）。

## 实现批次（确认规范后按此拆计划）

**批次 1 — 朋友在 Windows 上能自己走完（优先）**  
向导三步 + 设置字段 + 空首页主按钮 + README/Release 文案（Windows 包）。打完即可把 Windows zip 发给 Windows 朋友。

**批次 2 — Mac 包可发给人**  
`publish-macos.sh` 产物进 Release；清单勾选 dylib；三行打开说明；Intel / iOS 明确不支持。

**批次 3 — Mac 找歌更诚实**  
向导 Discover 步的 Mac/无 API 文案 + 可选 API 地址折叠。不捆绑 darwin API。

批次 1 不依赖 Mac 机器。批次 2 需要 macOS runner 或一台 Mac。

## 主要改动面（供计划引用）

- `FirstRunWizardModalViewModel.cs` / `FirstRunWizardModalView.axaml` — 步数与选路
- `AppSettingsService.cs` — `FirstRunOnboardingPath`
- `HomePageViewModel.cs` / `HomePageView.axaml` — 主 CTA
- 向导 i18n 键（zh-CN / en-US）
- `README.md`、`docs/GITHUB_RELEASE.md`、`docs/ROADMAP.md` 下一阶段、`CONTRIBUTING.md`
- `.github/workflows/release.yml` — 可选追加 macos 资产（批次 2）

不改：`SearchPageViewModel` 主搜索逻辑、整理工具、NAS/WebDAV、设置 IA。
