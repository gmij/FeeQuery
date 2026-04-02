# FeeQuery - 多云费用查询与预警平台

> 统一管理多家主流云厂商账号，实现费用集中追踪、余额监控和预警通知。

[![CI](https://github.com/your-org/feequery/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/feequery/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)

## 功能特性

- **多云账号统一管理**：一个界面管理所有云厂商账号
- **余额实时监控**：定时同步各云账号余额，支持自定义同步间隔
- **阈值预警通知**：余额低于阈值时自动触发通知
- **凭证安全加密**：所有 API 密钥通过 .NET Data Protection API 加密存储
- **预警历史记录**：完整记录每次预警触发情况
- **Docker 一键部署**：开箱即用，支持 Docker Compose

### 支持的云厂商和 AI 模型服务商

| 厂商 | 类型 | 凭证字段 |
|------|------|---------|
| 阿里云 | 公有云 | AccessKeyId / AccessKeySecret |
| 腾讯云 | 公有云 | SecretId / SecretKey |
| 华为云 | 公有云 | AccessKey / SecretAccessKey |
| 百度云 | 公有云 | AccessKeyId / SecretAccessKey |
| 火山引擎（字节跳动）| 公有云 | AccessKeyId / SecretAccessKey |
| MiniMax | AI 模型 | GroupId / ApiKey（含透支金额检测）|
| SiliconFlow | AI 模型 | ApiKey |
| 万界坊州 | AI 模型 | ApiKey |

### 支持的通知渠道

| 渠道 | 配置方式 |
|------|---------|
| 钉钉机器人 | WebhookUrl + Secret（关键字/加签两种模式）|
| SMTP 邮件 | SmtpServer / 端口 / 账号密码 |

## 快速开始

### 方式一：Docker Compose（推荐）

**前置要求**：Docker 和 Docker Compose

```bash
git clone https://github.com/your-org/feequery.git
cd feequery
docker-compose up -d
```

访问 [http://localhost:8080](http://localhost:8080)

### 方式二：本地运行

**前置要求**：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
git clone https://github.com/your-org/feequery.git
cd feequery
dotnet run --project src/FeeQuery.Web
```

访问 [http://localhost:5243](http://localhost:5243)

## 配置说明

### 数据库

默认使用 SQLite（无需额外安装），数据文件存储在 `data/feequery.db`。

支持通过环境变量切换到其他数据库：

```bash
# SQL Server
ConnectionStrings__DefaultConnection="Server=.;Database=FeeQuery;Trusted_Connection=True"
DatabaseProvider=SqlServer

# PostgreSQL
ConnectionStrings__DefaultConnection="Host=localhost;Database=feequery;Username=postgres;Password=xxx"
DatabaseProvider=PostgreSQL

# MySQL
ConnectionStrings__DefaultConnection="Server=localhost;Database=feequery;User=root;Password=xxx"
DatabaseProvider=MySQL
```

### Data Protection 密钥路径（重要）

凭证加密密钥默认存储在 `data/keys/` 目录。使用 Docker 时，确保该目录已持久化挂载（`docker-compose.yml` 默认已正确配置）。

**备份时请务必包含 `data/keys/` 目录**，否则已加密的云账号凭证将无法解密。

可通过环境变量自定义密钥路径：
```bash
DataProtection__KeysPath=/your/custom/keys/path
```

### Docker Compose 环境变量

`docker-compose.yml` 支持以下环境变量覆盖默认配置：

```yaml
environment:
  - ConnectionStrings__DefaultConnection=Data Source=/app/data/feequery.db
  - DatabaseProvider=Sqlite
  - DataProtection__KeysPath=/app/data/keys
  - ASPNETCORE_ENVIRONMENT=Production
  - TZ=Asia/Shanghai
```

## 安全说明

- 所有云账号 API 密钥通过 .NET Data Protection API 加密后存储，数据库中不保存明文凭证
- 凭证信息不会输出到任何日志
- 建议生产环境启用 HTTPS（参见 [DOCKER.md](DOCKER.md)）
- 请定期轮换云厂商访问密钥
- 请参阅 [SECURITY.md](SECURITY.md) 了解安全漏洞报告方式

## 解决方案结构

```
FeeQuery/
├── src/
│   ├── FeeQuery.Web/              # Blazor Server 前端 + 启动入口
│   ├── FeeQuery.Core/             # 核心业务逻辑（预警、同步、通知）
│   ├── FeeQuery.Data/             # 数据访问层（EF Core + 迁移）
│   ├── FeeQuery.Shared/           # 共享接口和模型
│   ├── FeeQuery.Providers/        # 云厂商适配器（每家一个独立项目）
│   ├── FeeQuery.Notifications/    # 通知渠道实现（钉钉、SMTP）
│   └── FeeQuery.SourceGenerators/ # 编译时自动注册生成器
└── tests/
    └── FeeQuery.Tests/            # 单元测试和集成测试
```

## 技术栈

| 组件 | 技术 |
|------|------|
| 后端框架 | .NET 10 + Blazor Server |
| UI 组件库 | AntDesign Blazor (AntBlazor) |
| ORM | Entity Framework Core 10 |
| 数据库 | SQLite（默认）/ SQL Server / PostgreSQL / MySQL |
| 日志 | Serilog |
| 容器 | Docker + Docker Compose |

## 开发文档

- [开发指南](docs/zh/development-guide.md) - 添加新云厂商、新通知渠道
- [错误处理](docs/zh/error-handling.md) - 异常处理规范
- [通知提供者架构](docs/zh/notification-architecture.md) - 通知系统设计
- [Docker 部署](DOCKER.md) - 详细部署说明

## 贡献

欢迎提交 Issue 和 Pull Request！请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证

本项目采用 [LICENSE](LICENSE) 许可证。
