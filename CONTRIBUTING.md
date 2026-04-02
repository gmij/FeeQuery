# 贡献指南

感谢你对 FeeQuery 的贡献！以下是参与项目的完整指南。

## 开发环境准备

**必需**：
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

**可选**��
- Visual Studio 2022 17.x 或 JetBrains Rider 2024.x
- Docker Desktop（用于容器测试）

**克隆仓库并还原依赖**：

```bash
git clone https://github.com/your-org/feequery.git
cd feequery
dotnet restore
```

**运行开发版本**：

```bash
dotnet run --project src/FeeQuery.Web
# 访问 http://localhost:5243
```

## 分支策略

| 分支 | 用途 |
|------|------|
| `main` | 稳定版本，只接受经过审查的 PR |
| `develop` | 日常开发，新功能从此分支出发 |

**工作流程**：

1. Fork 仓库，从 `develop` 分支创建功能分支
2. 分支命名：`feat/your-feature`、`fix/issue-description`、`docs/update-readme`
3. 开发完成后提交 PR 到 `develop`

## 提交规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/) 规范：

```
feat: 添加微软 Azure 云厂商适配器
fix: 修复腾讯云余额查询在子账号场景下的解析错误
docs: 更新 README 中的快速开始步骤
refactor: 重构通知提供者基类，提取公共发送逻辑
test: 为 CredentialEncryptionService 添加降级兼容测试
```

## 添加新的云厂商适配器

1. 在 `src/FeeQuery.Providers/` 下创建新的类库项目：

   ```bash
   cd src
   dotnet new classlib -n FeeQuery.Providers.YourProvider -f net10.0
   dotnet sln ../FeeQuery.sln add FeeQuery.Providers.YourProvider
   ```

2. 实现 `ICloudProvider` 接口（位于 `src/FeeQuery.Shared/Interfaces/`）：
   - `ProviderName`：厂商显示名称（中文）
   - `ProviderCode`：厂商标识（英文小写，如 `alibaba`）
   - `GetCredentialFields()`：返回该厂商所需的凭证字段列表
   - `ValidateCredentialsAsync()`：验证凭证有效性
   - `GetAccountBalanceAsync()`：查询账户余额
   - `GetBillingDataAsync()`：查询账单数据（可选）

3. 在类上添加 `[Service]` 特性，源生成器会自动完成依赖注入注册

4. 更新 README.md 的厂商支持列表

5. 添加基本单元测试（使用 Mock 的 HTTP 响应，不调用真实 API）

详细说明参见 [开发指南](docs/zh/development-guide.md)。

## 添加新的通知渠道

1. 在 `src/FeeQuery.Notifications/` 下创建新的类库项目
2. 实现 `INotificationProvider` 接口（位于 `src/FeeQuery.Shared/Interfaces/`）
3. 在类上添加 `[Service]` 特性
4. 在 `src/FeeQuery.Web/` 中添加对应的配置表单组件

## 代码规范

- 所有代码、注释、提交信息使用**中文**（与项目保持一致）
- 遵循 .NET 命名规范（PascalCase 类型名，camelCase 参数名）
- 凭证相关代码：永远不输出到日志，必须通过 `CredentialEncryptionService` 处理
- 不在代码中硬编码任何 API 端点 URL、密钥或账号信息

## PR 提交要求

- 所有 PR 需通过 CI 检查（`dotnet build` + `dotnet test`）
- 新功能需包含对应的单元测试
- 修改 API 密钥处理逻辑的 PR 需额外说明安全影响
- 在 PR 描述中填写变更说明和测试方式

## 问题反馈

- 提交 Bug 时请使用 [Bug 报告模板](.github/ISSUE_TEMPLATE/bug_report.yml)
- **请务必脱敏**：不要在 Issue 或 PR 中粘贴真实的 API Key 或账号密码
