# MusicVault 在线更新（GitHub + Velopack）

本仓库已内置 Velopack。把项目放到 GitHub 并按本流程发版后，用户即可在应用内检查/下载更新。

## 两条分发线（不要混）

| 产物 | 用途 | 能否应用内更新 |
|------|------|----------------|
| `publish/win-x64-vol` zip | 便携解压即用（含 API） | **否**（需手动下新 zip） |
| Velopack `Setup.exe` + `releases/` | 安装到本机 | **是**（走 GitHub Release） |

在线更新只服务 **Velopack 安装版**。便携包仍可用 Release 附件分发。

## 一次性：把仓库推上 GitHub

```powershell
# 1) 初始化并做首次提交（若尚未是 git 仓库）
.\scripts\github-bootstrap.ps1

# 2) 在 GitHub 网页新建空仓库 MusicVault（不要勾选自动加 README）
#    推荐公开仓库名：guohuiyuan/MusicVault
#    （与 AppUpdateDefaults.DefaultGithubRepoUrl 一致；若不同请改该常量）

# 3) 安装 GitHub CLI 后登录并推送（或网页创建后按脚本打印的 git remote 命令）
#    https://cli.github.com/
gh auth login
gh repo create MusicVault --public --source=. --remote=origin --push
```

若仓库地址不是 `https://github.com/guohuiyuan/MusicVault`，请修改：

- `src/MusicVault.App/Services/AppUpdateDefaults.cs` 中的 `DefaultGithubRepoUrl`
- 下文所有示例 URL

## 发版（本地）

```powershell
# 安装 Velopack CLI（只需一次）
dotnet tool install -g vpk

# 发布主程序目录（自动更新包默认用 publish\win-x64）
.\scripts\publish-windows.ps1 -NoRestart

# 打 Velopack 包（版本默认读 Directory.Build.props）
.\scripts\velopack-release.ps1

# 上传到 GitHub Release（需已安装并登录 gh）
.\scripts\publish-github-release.ps1
```

`publish-github-release.ps1` 会：

1. 确保已有 `releases/`（必要时调用 `velopack-release.ps1`）
2. 创建 `vX.Y.Z` tag / Release（若不存在）
3. 上传 Velopack 产物（`*.nupkg`、`RELEASES`、`releases.json`、Setup 等）
4. 打印用户应使用的更新源：`https://github.com/<owner>/MusicVault`

可选：同时把便携 zip（`publish\win-x64-vol`）作为附件上传，方便新用户首次安装。

## 发版（GitHub Actions）

推送版本 tag 即可触发 `.github/workflows/release.yml`：

```powershell
git tag v1.0.11
git push origin v1.0.11
```

Workflow 会 `dotnet publish` → `vpk pack` → 把产物挂到该 tag 的 GitHub Release。

## 用户侧

1. 使用 Release 里的 **Setup.exe**（Velopack）安装，不要只用便携 zip 指望自动更新。
2. 设置 → 软件更新：
   - **更新源 URL** 可留空（已内置 GitHub 默认仓库），或填 `https://github.com/guohuiyuan/MusicVault`
   - 检查频率：每日 / 每周 / 每月
3. 也可设环境变量：`MUSICVAULT_UPDATE_URL=https://github.com/guohuiyuan/MusicVault`

## 更新源 URL 两种写法

| 写法 | 示例 | 说明 |
|------|------|------|
| GitHub 仓库 | `https://github.com/guohuiyuan/MusicVault` | 推荐；应用走 Velopack `GithubSource` |
| 静态目录 | `https://cdn.example.com/musicvault` | 目录内需有 `releases.json` / `RELEASES` |

## 发版检查清单

- [ ] `Directory.Build.props` 版本已 bump
- [ ] `CHANGELOG.md` 已写本版说明
- [ ] CI / 本地测试通过
- [ ] tag `vX.Y.Z` 已推送或本地 `publish-github-release.ps1` 已跑通
- [ ] Release 附件含 Velopack 产物（至少 `MusicVault-*-full.nupkg` 与 `RELEASES`）
- [ ] 用上一版 Velopack 安装的客户端点「检查更新」能发现新版本

## 相关文档

- [RELEASE.md](../RELEASE.md) — 发布总览
- [GITHUB_RELEASE.md](GITHUB_RELEASE.md) — Release 正文模板
- [REPOSITORY.md](REPOSITORY.md) — 勿提交 `publish/` / `releases/`
- [LEGAL.md](LEGAL.md) — 开源与合规
