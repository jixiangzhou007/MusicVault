# 仓库与构建产物管理

## 不应提交到 Git 的内容

| 路径 | 原因 |
|------|------|
| `bin/`、`obj/` | 编译中间文件 |
| `publish/` | 本地发布输出，体积大，含 `node_modules` |
| `.vs/` | IDE 缓存 |
| `tmp-probe/` | 临时探测项目 |
| `**/node_modules/` | npm 依赖（由发布脚本在 `publish/` 下生成） |

根目录 `.gitignore` 已包含上述规则。

## 若 `publish/` 曾被误提交

```powershell
git rm -r --cached publish/
git commit -m "chore: stop tracking publish output"
```

历史大文件需使用 `git filter-repo` 或 BFG 清理（自行评估；会改写历史）。

## 推荐工作流

1. 开发： `dotnet build` / `dotnet run --project src/MusicVault.App`
2. 本地验证发布： `.\scripts\publish-with-api.ps1 -Restart`
3. 打包给朋友 / Release：压缩 `publish\win-x64-vol\`，**不要**把该目录 push 到 GitHub
4. 在线更新：把 `releases/`（Velopack）挂到 GitHub Release，见 [ONLINE_UPDATE.md](ONLINE_UPDATE.md)
5. 开源协作：只提交 `src/`、`tests/`、`scripts/`、`docs/` 与许可证文件

## 克隆体积

新贡献者 `git clone` 应只需源码（通常 &lt; 5 MB）。若 clone 过慢，检查是否误提交了 `publish/`、`releases/` 或二进制大文件。
