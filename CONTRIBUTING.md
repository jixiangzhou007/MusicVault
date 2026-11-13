# 贡献指南

感谢你对 MusicVault 的关注！

## 准备工作

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 克隆仓库：`git clone <repo-url> && cd MusicVault`
3. 还原依赖：`dotnet restore`
4. 构建：`dotnet build`
5. 运行测试：`dotnet test`

## 开发流程

1. **Fork & Branch** — 从 `main` 创建特性分支（`feature/xxx` 或 `fix/xxx`）
2. **Code** — 遵循项目现有风格（见 `DESIGN_SYSTEM.md`、`.editorconfig`）
3. **Format** — `dotnet format MusicVault.sln`（CI 强制检查）
4. **Design Audit** — `.\scripts\audit-design-system.ps1`（UI 变更时）
5. **Test** — `dotnet test` 确保全部通过
6. **PR** — 提交 Pull Request，附简要说明

## 代码规范

- **C# 12** + **.NET 8**（`LangVersion=12`、`Nullable=enable`）
- 异步方法以 `Async` 结尾
- XML 文档注释（`<summary>`）用于公共 API
- 不可变更新优先使用 `Track.With()` 方法
- 服务层字符串走 `ServiceMessages` 注册表
- 日志使用 `ILogger<T>` + `MusicVaultLog` 强类型事件

### 设计系统

UI 变更**必须**遵守 [docs/DESIGN_SYSTEM.md](docs/DESIGN_SYSTEM.md)：
- 禁止 View 层硬编码 `#` 色值
- 禁止 `FontSize="12"` 等魔法数字
- 使用 Token + class 驱动样式

## 项目结构

```
src/
├── MusicVault.Core/      # 领域模型、枚举、接口（无 I/O）
├── MusicVault.Data/      # SQLite 仓储、数据库初始化与迁移
├── MusicVault.Services/  # 业务逻辑、Features、平台 Provider
└── MusicVault.App/       # Avalonia UI、ViewModel、主题
tests/
└── MusicVault.Tests/     # 单元测试 + Headless UI 测试
```

依赖方向：`App → Services → Data / Core`，Core 不依赖上层。

## 运行

```powershell
dotnet run --project src\MusicVault.App
```

## 问题反馈

- 使用 [GitHub Issues](https://github.com/jixiangzhou007/MusicVault/issues)
- Bug 报告请附：版本号、操作系统、复现步骤、诊断日志（`%LOCALAPPDATA%\MusicVault\logs\`）
- 功能建议请使用 Feature Request 模板

## 许可证

源代码：[MIT License](LICENSE)。贡献即同意以 MIT 协议授权。
