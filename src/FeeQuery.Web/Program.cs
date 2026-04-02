using FeeQuery.Core.AlertCheckers;
using FeeQuery.Core.Factories;
using FeeQuery.Core.Interfaces;
using FeeQuery.Core.NotificationBuilders;
using FeeQuery.Core.Services;
using FeeQuery.Core.Services.EventHandlers;
using FeeQuery.Data;
using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Events;
using FeeQuery.Shared.Extensions;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog 作为日志后端（应用内部仍通过 ILogger<> 接口使用，与日志框架解耦）
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// 配置数据库
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

// 目前使用SQLite，如需切换到其他数据库，需安装相应的NuGet包
// 如：Microsoft.EntityFrameworkCore.SqlServer、Npgsql.EntityFrameworkCore.PostgreSQL、Pomelo.EntityFrameworkCore.MySql
if (dbProvider == "Sqlite")
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString), ServiceLifetime.Scoped);
}
else
{
    throw new InvalidOperationException($"当前仅支持SQLite数据库。如需使用{dbProvider}，请安装相应的NuGet包并取消注释相关代码。");
}

// 注册HttpClientFactory（火山引擎需要）
builder.Services.AddHttpClient();

// 【源生成器自动注册】云厂商和通知提供者
// 无需反射，无需强制加载程序集，编译时生成注册代码
builder.Services.AddGeneratedCloudProviders();
builder.Services.AddGeneratedNotificationProviders();

// 注册云厂商工厂
builder.Services.AddSingleton<ICloudProviderFactory, CloudProviderFactory>();

// 注册Data Protection（用于凭证加密），根据 Security:EncryptionProvider 配置��择密钥存储方式：
//   DataProtection（默认）：密钥持久化到数据库，备份数据库即备份密钥
//   Aes：使用 AES-256，密钥由 Security:AesKey 配置项提供
var encryptionProvider = builder.Configuration.GetValue<string>("Security:EncryptionProvider") ?? "DataProtection";
if (encryptionProvider.Equals("DataProtection", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<ApplicationDbContext>()
        .SetApplicationName("FeeQuery");
}
else
{
    // AES 模式：DataProtection 仍需注册（框架依赖），但不用于凭证加密
    builder.Services.AddDataProtection().SetApplicationName("FeeQuery");
}

// 注册事件总线
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// 注册事件处理器
builder.Services.AddScoped<IEventHandler<BalanceRefreshedEvent>, BalanceRefreshedEventHandler>();
builder.Services.AddScoped<IEventHandler<BalanceRefreshedEvent>, SyncFailureEventHandler>();

// 注册Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 注册Alert Checkers和Notification Builders
builder.Services.AddScoped<IAlertChecker, BalanceAlertChecker>();
builder.Services.AddScoped<IAlertNotificationBuilder, BalanceAlertNotificationBuilder>();

// 注册业务服务
builder.Services.AddScoped<FeeQuery.Data.Repositories.CloudAccountRepository>();
builder.Services.AddScoped<FeeQuery.Core.Services.CredentialEncryptionService>();
builder.Services.AddScoped<FeeQuery.Core.Services.CloudAccountService>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<BalanceAlertService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BackupService>();

// 注册 Web 服务
builder.Services.AddScoped<FeeQuery.Web.Services.ErrorHandlingService>();

// 注册后台服务
builder.Services.AddHostedService<BalanceSyncBackgroundService>();

// 添加 AntDesign 服务
builder.Services.AddAntDesign();

// 添加 Controllers 支持（用于API端点）
builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// 记录已注册的提供者（便于调试和确认）
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

// 初始化数据库并创建默认配置
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // 应用数据库迁移
        logger.LogInformation("正在应用数据库迁移...");
        context.Database.Migrate();
        logger.LogInformation("数据库迁移完成");

        // 创建默认的SyncConfiguration（如果不存在）
        var existingConfig = context.SyncConfigurations.FirstOrDefault(c => c.CloudAccountId == null);
        if (existingConfig == null)
        {
            logger.LogInformation("创建默认的全局同步配置...");
            var defaultConfig = new FeeQuery.Shared.Models.SyncConfiguration
            {
                CloudAccountId = null, // 全局配置
                IntervalMinutes = 30, // 默认30分钟同步一次
                SyncBalance = true,
                IsEnabled = true,
                LastSyncAt = null
            };
            context.SyncConfigurations.Add(defaultConfig);
            context.SaveChanges();
            logger.LogInformation("默认同步配置创建成功：每30分钟同步一次余额");
        }
        else
        {
            logger.LogInformation("全局同步配置已存在，间隔：{Interval}分钟，状态：{Status}",
                existingConfig.IntervalMinutes,
                existingConfig.IsEnabled ? "启用" : "禁用");
        }

        // 更新旧的预警规则，为空的 BalanceType 设置默认值
        var rulesWithoutBalanceType = context.AlertRules.Where(r => r.BalanceType == "" || r.BalanceType == null).ToList();
        if (rulesWithoutBalanceType.Any())
        {
            logger.LogInformation("更新 {Count} 条旧预警规则的余额类型为默认值...", rulesWithoutBalanceType.Count);
            foreach (var rule in rulesWithoutBalanceType)
            {
                rule.BalanceType = "available"; // 默认检查现金余额
            }
            context.SaveChanges();
            logger.LogInformation("预警规则更新完成");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "数据库初始化失败");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();  // 映射 API Controllers
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
