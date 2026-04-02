using FeeQuery.Core.AlertCheckers;
using FeeQuery.Core.Interfaces;
using FeeQuery.Core.NotificationBuilders;
using FeeQuery.Core.Services;
using FeeQuery.Data;
using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace FeeQuery.Tests;

/// <summary>
/// 余额预警集成测试
/// </summary>
public class BalanceAlertIntegrationTest : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BalanceAlertService _alertService;
    private readonly NotificationService _notificationService;
    private readonly ITestOutputHelper _output;

    public BalanceAlertIntegrationTest(ITestOutputHelper output)
    {
        _output = output;

        // 使用In-Memory数据库
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);

        // 创建日志记录器
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var notificationLogger = loggerFactory.CreateLogger<NotificationService>();
        var alertLogger = loggerFactory.CreateLogger<BalanceAlertService>();
        var checkerLogger = loggerFactory.CreateLogger<BalanceAlertChecker>();
        var builderLogger = loggerFactory.CreateLogger<BalanceAlertNotificationBuilder>();

        // 创建模拟的 IConfiguration
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"FeeQuery:BaseUrl", "https://test-domain.com"}
        };
        IConfiguration configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // 创建Alert Checker和Notification Builder
        var alertChecker = new BalanceAlertChecker(checkerLogger);
        var notificationBuilder = new BalanceAlertNotificationBuilder(builderLogger);

        // 创建服务（使用空的通知提供者列表）
        _notificationService = new NotificationService(
            _unitOfWork,
            new List<INotificationProvider>(),
            notificationLogger);
        _alertService = new BalanceAlertService(
            _unitOfWork,
            _notificationService,
            alertLogger,
            configuration,
            new List<INotificationProvider>(),
            alertChecker,
            notificationBuilder);
    }

    [Fact]
    public async Task TestBalanceAlertTriggering()
    {
        _output.WriteLine("开始测试余额预警触发...");

        // 1. 创建测试账号
        var account = new CloudAccount
        {
            Id = 1,
            Name = "测试账号",
            ProviderCode = "test",
            ProviderName = "测试云",
            EncryptedCredentials = "test",
            IsEnabled = true
        };
        _context.CloudAccounts.Add(account);
        await _context.SaveChangesAsync();

        // 2. 创建预警规则：余额低于100元时预警
        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "余额低于100元预警",
            AlertType = "balance",
            CloudAccountId = 1,
            Threshold = 100m,
            ComparisonOperator = "less_than",
            PeriodType = "daily",
            IsEnabled = true
        };
        _context.AlertRules.Add(alertRule);
        await _context.SaveChangesAsync();

        // 3. 触发预警检查（当前余额50元，低于阈值100元）
        await _alertService.CheckBalanceAlertsAsync(1, 50m);

        // 4. 验证预警历史记录是否创建
        var alertHistory = await _context.AlertHistories
            .FirstOrDefaultAsync(h => h.CloudAccountId == 1);

        Assert.NotNull(alertHistory);
        Assert.Equal(1, alertHistory.AlertRuleId);
        Assert.Equal(50m, alertHistory.ActualAmount);
        Assert.Equal(100m, alertHistory.ThresholdAmount);
        // 状态应该是"notified"，因为尝试发送通知后状态会更新
        Assert.Equal("notified", alertHistory.Status);

        _output.WriteLine($"✓ 预警记录已创建: ID={alertHistory.Id}, 状态={alertHistory.Status}");
        _output.WriteLine($"  实际余额: {alertHistory.ActualAmount:N2} 元");
        _output.WriteLine($"  预警阈值: {alertHistory.ThresholdAmount:N2} 元");
        _output.WriteLine($"  偏差: {alertHistory.ExceedPercentage:N2}%");

        // 5. 验证不会重复触发（24小时内）
        await _alertService.CheckBalanceAlertsAsync(1, 45m);
        var alertCount = await _context.AlertHistories.CountAsync(h => h.CloudAccountId == 1);
        Assert.Equal(1, alertCount); // 应该还是只有1条记录

        _output.WriteLine("✓ 防止24小时内重复触发测试通过");
    }

    [Fact]
    public async Task TestBalanceAlertNotTriggeredWhenAboveThreshold()
    {
        _output.WriteLine("开始测试余额高于阈值时不触发预警...");

        // 1. 创建测试账号
        var account = new CloudAccount
        {
            Id = 2,
            Name = "测试账号2",
            ProviderCode = "test",
            ProviderName = "测试云",
            EncryptedCredentials = "test",
            IsEnabled = true
        };
        _context.CloudAccounts.Add(account);
        await _context.SaveChangesAsync();

        // 2. 创建预警规则：余额低于100元时预警
        var alertRule = new AlertRule
        {
            Id = 2,
            Name = "余额低于100元预警",
            AlertType = "balance",
            CloudAccountId = 2,
            Threshold = 100m,
            ComparisonOperator = "less_than",
            PeriodType = "daily",
            IsEnabled = true
        };
        _context.AlertRules.Add(alertRule);
        await _context.SaveChangesAsync();

        // 3. 触发预警检查（当前余额150元，高于阈值100元）
        await _alertService.CheckBalanceAlertsAsync(2, 150m);

        // 4. 验证没有创建预警历史记录
        var alertHistory = await _context.AlertHistories
            .FirstOrDefaultAsync(h => h.CloudAccountId == 2);

        Assert.Null(alertHistory);
        _output.WriteLine("✓ 余额高于阈值时未触发预警，测试通过");
    }

    [Fact]
    public async Task TestGlobalAlertRule()
    {
        _output.WriteLine("开始测试全局预警规则...");

        // 1. 创建测试账号（没有账号级规则）
        var account = new CloudAccount
        {
            Id = 3,
            Name = "测试账号3",
            ProviderCode = "test",
            ProviderName = "测试云",
            EncryptedCredentials = "test",
            IsEnabled = true
        };
        _context.CloudAccounts.Add(account);
        await _context.SaveChangesAsync();

        // 2. 创建全局预警规则（CloudAccountId为null）
        var alertRule = new AlertRule
        {
            Id = 3,
            Name = "全局余额预警",
            AlertType = "balance",
            CloudAccountId = null, // 全局规则
            Threshold = 200m,
            ComparisonOperator = "less_than",
            PeriodType = "daily",
            IsEnabled = true
        };
        _context.AlertRules.Add(alertRule);
        await _context.SaveChangesAsync();

        // 3. 触发预警检查（当前余额150元，低于全局阈值200元）
        await _alertService.CheckBalanceAlertsAsync(3, 150m);

        // 4. 验证全局规则也能触发预警
        var alertHistory = await _context.AlertHistories
            .FirstOrDefaultAsync(h => h.CloudAccountId == 3);

        Assert.NotNull(alertHistory);
        Assert.Equal(3, alertHistory.AlertRuleId);
        _output.WriteLine("✓ 全局预警规则测试通过");
    }

    [Fact]
    public async Task TestBalanceAlertAutoResolveWithNotification()
    {
        _output.WriteLine("开始测试余额恢复后自动解除预警并发送通知...");

        // 1. 创建测试账号
        var account = new CloudAccount
        {
            Id = 4,
            Name = "测试账号4",
            ProviderCode = "test",
            ProviderName = "测试云",
            EncryptedCredentials = "test",
            IsEnabled = true
        };
        _context.CloudAccounts.Add(account);
        await _context.SaveChangesAsync();

        // 2. 创建预警规则：余额低于100元时预警
        var alertRule = new AlertRule
        {
            Id = 4,
            Name = "余额低于100元预警",
            AlertType = "balance",
            CloudAccountId = 4,
            Threshold = 100m,
            ComparisonOperator = "less_than",
            BalanceType = "available",
            PeriodType = "daily",
            IsEnabled = true
        };
        _context.AlertRules.Add(alertRule);
        await _context.SaveChangesAsync();

        // 3. 第一次检查：触发预警（当前余额50元，低于阈值100元）
        _output.WriteLine("第一次检查：余额50元，低于阈值100元，应该触发预警");
        await _alertService.CheckBalanceAlertsAsync(4, 50m);

        // 4. 验证预警历史记录是否创建
        var alertHistory = await _context.AlertHistories
            .FirstOrDefaultAsync(h => h.CloudAccountId == 4);

        Assert.NotNull(alertHistory);
        Assert.Equal(4, alertHistory.AlertRuleId);
        Assert.Equal(50m, alertHistory.ActualAmount);
        Assert.Equal(100m, alertHistory.ThresholdAmount);
        Assert.Equal("notified", alertHistory.Status);

        _output.WriteLine($"✓ 预警已触发: ID={alertHistory.Id}, 状态={alertHistory.Status}");
        _output.WriteLine($"  实际余额: {alertHistory.ActualAmount:N2} 元");
        _output.WriteLine($"  预警阈值: {alertHistory.ThresholdAmount:N2} 元");

        // 5. 第二次检查：余额恢复（当前余额150元，高于阈值100元）
        _output.WriteLine("\n第二次检查：余额150元，高于阈值100元，应该自动解除预警");
        await _alertService.CheckBalanceAlertsAsync(4, 150m);

        // 6. 重新加载预警历史记录，验证状态是否变为"resolved"
        await _context.Entry(alertHistory).ReloadAsync();

        Assert.Equal("resolved", alertHistory.Status);
        Assert.NotNull(alertHistory.Remark);
        Assert.Contains("系统自动解决", alertHistory.Remark);
        Assert.Contains("余额已恢复", alertHistory.Remark);

        _output.WriteLine($"✓ 预警已自动解除: ID={alertHistory.Id}, 状态={alertHistory.Status}");
        _output.WriteLine($"  备注: {alertHistory.Remark}");

        // 7. 验证不会创建新的预警记录
        var alertCount = await _context.AlertHistories.CountAsync(h => h.CloudAccountId == 4);
        Assert.Equal(1, alertCount);

        _output.WriteLine("✓ 余额恢复后自动解除预警测试通过");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
