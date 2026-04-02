# IoC 自动发现和注册文档

## 概述

基于微软依赖注入框架（Microsoft.Extensions.DependencyInjection），实现了服务的自动发现和注册功能，以及完整的生命周期管理。

## 核心组件

### 1. ServiceAttribute 特性

位置：`FeeQuery.Shared/Attributes/ServiceAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ServiceAttribute : Attribute
{
    /// <summary>
    /// 服务生命周期（Singleton, Scoped, Transient）
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// 服务接口类型（可选，默认自动推断）
    /// </summary>
    public Type? ServiceType { get; set; }

    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Lifetime = lifetime;
    }
}
```

**使用示例**：

```csharp
// 单例模式（默认）
[Service]
public class MyService : IMyService { }

// 明确指定生命周期
[Service(ServiceLifetime.Scoped)]
public class BalanceService { }

// 指定服务接口类型
[Service(ServiceLifetime.Singleton, ServiceType = typeof(ICloudProvider))]
public class AlibabaCloudProvider : ICloudProvider { }
```

### 2. DependencyInjectionExtensions 扩展方法

位置：`FeeQuery.Shared/Extensions/DependencyInjectionExtensions.cs`

提供了多种服务注册扩展方法：

#### 方法 1：自动发现带特性的服务

```csharp
public static IServiceCollection AddAutoDiscoveredServices(
    this IServiceCollection services,
    params Assembly[] assemblies)
```

**功能**：扫描指定程序集，自动注册所有标记了 `[Service]` 特性的类。

**使用方式**：

```csharp
// 扫描所有已加载的程序集
builder.Services.AddAutoDiscoveredServices();

// 扫描指定程序集
var assembly = typeof(AlibabaCloudProvider).Assembly;
builder.Services.AddAutoDiscoveredServices(assembly);
```

#### 方法 2：按接口自动注册

```csharp
public static IServiceCollection AddImplementationsOf<TInterface>(
    this IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Singleton,
    params Assembly[] assemblies)
```

**功能**：扫描并注册实现了指定接口的所有类。

**使用方式**：

```csharp
// 注册所有 ICloudProvider 实现为单例
builder.Services.AddImplementationsOf<ICloudProvider>();

// 注册所有 INotificationProvider 实现为作用域服务
builder.Services.AddImplementationsOf<INotificationProvider>(ServiceLifetime.Scoped);
```

#### 方法 3：专用快捷方法

```csharp
// 自动注册所有云厂商提供者
public static IServiceCollection AddCloudProviders(
    this IServiceCollection services,
    params Assembly[] assemblies)

// 自动注册所有通知提供者
public static IServiceCollection AddNotificationProviders(
    this IServiceCollection services,
    params Assembly[] assemblies)
```

**使用方式**：

```csharp
builder.Services.AddCloudProviders();        // 自动注册所有云厂商
builder.Services.AddNotificationProviders(); // 自动注册所有通知提供者
```

#### 方法 4：辅助方法

```csharp
// 从命名空间加载程序集
public static Assembly[] LoadAssembliesFromNamespace(string namespacePrefix)

// 记录已注册的服务到日志
public static void LogRegisteredServices(
    this IServiceCollection services,
    ILogger logger,
    Func<Type, bool>? serviceTypeFilter = null)
```

## 使用指南

### 步骤 1：标记服务类

在服务类上添加 `[Service]` 特性：

```csharp
using FeeQuery.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace FeeQuery.Providers.Alibaba;

[Service(ServiceLifetime.Singleton)]
public class AlibabaCloudProvider : ICloudProvider
{
    // 实现代码...
}
```

### 步骤 2：在 Program.cs 中自动注册

#### 旧方式（手动注册）❌

```csharp
// 需要手动注册每个提供者
builder.Services.AddSingleton<ICloudProvider, AlibabaCloudProvider>();
builder.Services.AddSingleton<ICloudProvider, BaiduCloudProvider>();
builder.Services.AddSingleton<ICloudProvider, HuaweiCloudProvider>();
builder.Services.AddSingleton<ICloudProvider, TencentCloudProvider>();
builder.Services.AddSingleton<ICloudProvider, VolcanoCloudProvider>();
builder.Services.AddSingleton<ICloudProvider, MiniMaxProvider>();

builder.Services.AddSingleton<INotificationProvider, SmtpNotificationProvider>();
builder.Services.AddSingleton<INotificationProvider, DingTalkNotificationProvider>();
```

#### 新方式（自动发现）✅

```csharp
using FeeQuery.Shared.Extensions;

// 【重要】强制加载提供者程序集（确保在自动发现时这些程序集已经加载到 AppDomain）
var forceLoadTypes = new Type[]
{
    typeof(FeeQuery.Providers.Alibaba.AlibabaCloudProvider),
    typeof(FeeQuery.Providers.Baidu.BaiduCloudProvider),
    typeof(FeeQuery.Providers.Huawei.HuaweiCloudProvider),
    typeof(FeeQuery.Providers.MiniMax.MiniMaxProvider),
    typeof(FeeQuery.Providers.Tencent.TencentCloudProvider),
    typeof(FeeQuery.Providers.Volcano.VolcanoCloudProvider),
    typeof(FeeQuery.Notifications.DingTalk.DingTalkNotificationProvider),
    typeof(FeeQuery.Notifications.Smtp.SmtpNotificationProvider)
};
// 使用类型数组来确保程序集被加载
foreach (var type in forceLoadTypes)
{
    _ = type.Assembly;
}

// 仅需两行代码自动注册
builder.Services.AddCloudProviders();         // 自动注册所有云厂商
builder.Services.AddNotificationProviders();  // 自动注册所有通知提供者
```

**⚠️ 重要说明：程序集加载**

由于 .NET 的延迟加载机制，即使项目引用了提供者程序集，它们也可能在应用启动时还未加载到 `AppDomain` 中。自动发现功能需要扫描已加载的程序集，因此必须在调用 `AddCloudProviders()` 和 `AddNotificationProviders()` 之前强制加载这些程序集。

上述代码通过创建类型数组并访问 `type.Assembly` 属性来触发程序集加载。如果不执行这一步，自动发现将无法找到任何提供者。

### 步骤 3：（可选）启用服务日志

在应用启动时记录已注册的提供者：

```csharp
var app = builder.Build();

// 记录已注册的提供者
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("========== 自动发现的服务提供者 ==========");

    // 记录云厂商提供者
    var cloudProviders = scope.ServiceProvider.GetServices<ICloudProvider>();
    logger.LogInformation("【云厂商提供者】已注册 {Count} 个:", cloudProviders.Count());
    foreach (var provider in cloudProviders)
    {
        logger.LogInformation("  ✓ {ProviderName} ({ProviderCode})",
            provider.ProviderName, provider.ProviderCode);
    }

    // 记录通知提供者
    var notificationProviders = scope.ServiceProvider.GetServices<INotificationProvider>();
    logger.LogInformation("【通知提供者】已注册 {Count} 个:", notificationProviders.Count());
    foreach (var provider in notificationProviders)
    {
        logger.LogInformation("  ✓ {ProviderType}", provider.ProviderType);
    }

    logger.LogInformation("==========================================");
}
```

**日志输出示例**：

```
========== 自动发现的服务提供者 ==========
【云厂商提供者】已注册 6 个:
  ✓ 阿里云 (alibaba)
  ✓ 百度云 (baidu)
  ✓ 华为云 (huawei)
  ✓ MiniMax (minimax)
  ✓ 腾讯云 (tencent)
  ✓ 火山引擎 (volcano)
【通知提供者】已注册 2 个:
  ✓ email
  ✓ dingtalk
==========================================
```

## 生命周期管理

### Singleton（单例）

**特点**：
- 应用程序生命周期内只创建一次
- 所有请求共享同一个实例
- 线程安全

**适用场景**：
- 云厂商适配器（无状态，可复用）
- 通知提供者（无状态，可复用）
- 工厂类
- 缓存服务

**示例**：

```csharp
[Service(ServiceLifetime.Singleton)]
public class AlibabaCloudProvider : ICloudProvider { }
```

### Scoped（作用域）

**特点**：
- 每个 HTTP 请求创建一个实例
- 同一请求内共享实例
- 请求结束后销毁

**适用场景**：
- 数据库上下文（DbContext）
- 仓储（Repository）
- 业务服务（Service）

**示例**：

```csharp
[Service(ServiceLifetime.Scoped)]
public class BalanceService { }

[Service(ServiceLifetime.Scoped)]
public class CloudAccountService { }
```

### Transient（瞬时）

**特点**：
- 每次请求都创建新实例
- 不共享状态

**适用场景**：
- 轻量级无状态服务
- 工具类
- 有状态的临时对象

**示例**：

```csharp
[Service(ServiceLifetime.Transient)]
public class RandomNumberGenerator { }
```

## 最佳实践

### 1. 明确服务生命周期

始终显式指定生命周期，不依赖默认值：

```csharp
// ✅ 好的做法
[Service(ServiceLifetime.Singleton)]
public class MyService { }

// ⚠️ 可以但不推荐（依赖默认值）
[Service]
public class MyService { }
```

### 2. 避免循环依赖

```csharp
// ❌ 错误：循环依赖
public class ServiceA
{
    public ServiceA(ServiceB serviceB) { }
}

public class ServiceB
{
    public ServiceB(ServiceA serviceA) { }
}

// ✅ 正确：使用接口打破循环
public interface IServiceA { }

public class ServiceA : IServiceA
{
    public ServiceA(IServiceB serviceB) { }
}

public class ServiceB : IServiceB
{
    public ServiceB(IServiceA serviceA) { }
}
```

### 3. Singleton 服务中避免注入 Scoped 服务

```csharp
// ❌ 错误：Singleton 注入 Scoped
[Service(ServiceLifetime.Singleton)]
public class MySingletonService
{
    public MySingletonService(ApplicationDbContext dbContext) { } // DbContext 是 Scoped
}

// ✅ 正确：使用 IServiceProvider 按需解析
[Service(ServiceLifetime.Singleton)]
public class MySingletonService
{
    private readonly IServiceProvider _serviceProvider;

    public MySingletonService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void DoWork()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // 使用 dbContext...
    }
}
```

### 4. 组织服务注册代码

将服务注册代码组织在扩展方法中：

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeeQueryServices(this IServiceCollection services)
    {
        // 自动注册提供者
        services.AddCloudProviders();
        services.AddNotificationProviders();

        // 手动注册核心服务
        services.AddScoped<BalanceService>();
        services.AddScoped<BalanceAlertService>();
        services.AddScoped<NotificationService>();

        return services;
    }
}

// 在 Program.cs 中使用
builder.Services.AddFeeQueryServices();
```

## 添加新服务提供者

### 示例：添加企业微信通知提供者

**步骤 1：创建项目和类**

```bash
cd src/FeeQuery.Notifications
mkdir WeChat
cd WeChat
dotnet new classlib -n FeeQuery.Notifications.WeChat -f net10.0
dotnet add reference ../../FeeQuery.Shared/FeeQuery.Shared.csproj
dotnet add package Microsoft.Extensions.Logging.Abstractions
```

**步骤 2：实现接口并添加特性**

```csharp
using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Notifications.WeChat;

[Service(ServiceLifetime.Singleton)]
public class WeChatNotificationProvider : INotificationProvider
{
    private readonly ILogger<WeChatNotificationProvider> _logger;

    public string ProviderType => "wechat";

    public WeChatNotificationProvider(ILogger<WeChatNotificationProvider> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendAsync(string title, string content, string configJson,
        CancellationToken cancellationToken = default)
    {
        // 实现企业微信发送逻辑
        return true;
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(
        string configJson, CancellationToken cancellationToken = default)
    {
        // 实现测试逻辑
        return (true, null);
    }
}
```

**步骤 3：无需修改 Program.cs**

由于使用了自动发现，新提供者会被自动注册！

```csharp
// Program.cs 不需要任何改动
builder.Services.AddNotificationProviders();  // 自动包含 WeChatNotificationProvider
```

## 技术细节

### 程序集扫描策略

自动发现会跳过以下程序集以提高性能：
- 系统程序集（`System.*`）
- 微软框架程序集（`Microsoft.*`）
- .NET 标准库（`netstandard.*`）

### 服务接口推断规则

当未指定 `ServiceType` 时，按以下优先级推断：

1. 查找与类名匹配的接口（如 `FooService` → `IFooService`）
2. 使用第一个非系统接口
3. 如果没有接口，直接注册实现类

**示例**：

```csharp
// 情况 1：匹配命名约定
[Service]
public class UserService : IUserService, IService  // 注册为 IUserService

// 情况 2：无命名约定
[Service]
public class Helper : IHelper, IDisposable  // 注册为 IHelper（第一个非系统接口）

// 情况 3：无接口
[Service]
public class Utility  // 直接注册为 Utility 类型
```

## 故障排除

### 问题 1：服务未被发现

**原因**：程序集未被加载

**解决方案**：确保项目引用了包含服务的程序集

```xml
<ItemGroup>
  <ProjectReference Include="..\FeeQuery.Notifications\WeChat\FeeQuery.Notifications.WeChat.csproj" />
</ItemGroup>
```

### 问题 2：生命周期冲突

**错误示例**：
```
InvalidOperationException: Cannot consume scoped service 'DbContext' from singleton 'MyService'.
```

**解决方案**：调整服务生命周期或使用 `IServiceProvider`

### 问题 3：循环依赖

**错误示例**：
```
InvalidOperationException: A circular dependency was detected.
```

**解决方案**：使用接口或延迟加载（`Lazy<T>`）

## 性能考虑

- ✅ 程序集扫描仅在启动时执行一次
- ✅ 系统程序集被自动跳过
- ✅ 使用反射缓存提高性能
- ⚠️ 大量服务注册会略微增加启动时间（通常可忽略）

## 优势总结

### 代码简洁性

**之前**：12 行手动注册代码
**现在**：2 行自动发现代码

**减少 83% 的注册代码！**

### 可维护性

- ✅ 添加新服务无需修改 `Program.cs`
- ✅ 服务定义和注册在同一位置
- ✅ 自动遵守约定，减少错误

### 可扩展性

- ✅ 支持插件化架构
- ✅ 第三方扩展只需添加特性
- ✅ 无需修改核心代码

## 参考资料

- [Microsoft.Extensions.DependencyInjection 官方文档](https://learn.microsoft.com/zh-cn/dotnet/core/extensions/dependency-injection)
- [依赖注入最佳实践](https://learn.microsoft.com/zh-cn/dotnet/core/extensions/dependency-injection-guidelines)
- [.NET 生命周期管理](https://learn.microsoft.com/zh-cn/dotnet/core/extensions/dependency-injection#service-lifetimes)

---

*文档版本: 1.0*
*最后更新: 2025-12-11*
