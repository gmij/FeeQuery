using FeeQuery.Data;
using FeeQuery.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services;

/// <summary>
/// 余额定时同步服务
/// 每分钟扫描一次到期账号并直接执行同步；新增账号时将 NextSyncAt 设为当前时间，下次扫描即自动被纳入
/// </summary>
public class BalanceSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BalanceSyncBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public BalanceSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BalanceSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("余额同步服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncDueAccountsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步扫描时发生错误");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("余额同步服务已停止");
    }

    /// <summary>
    /// 扫描所有到期账号并逐一执行余额同步
    /// </summary>
    private async Task SyncDueAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var globalConfig = await context.SyncConfigurations
            .FirstOrDefaultAsync(c => c.CloudAccountId == null, cancellationToken);

        if (globalConfig == null || !globalConfig.IsEnabled)
        {
            _logger.LogDebug("全局同步配置未找到或已禁用，跳过本轮扫描");
            return;
        }

        var now = DateTime.UtcNow;
        var enabledAccounts = await context.CloudAccounts
            .Where(a => a.IsEnabled)
            .ToListAsync(cancellationToken);

        if (enabledAccounts.Count == 0) return;

        int synced = 0;
        foreach (var account in enabledAccounts)
        {
            var accountConfig = await context.SyncConfigurations
                .FirstOrDefaultAsync(c => c.CloudAccountId == account.Id && c.IsEnabled, cancellationToken);

            var config = accountConfig ?? globalConfig;

            if (!config.SyncBalance) continue;

            var isDue = config.LastSyncAt == null
                || now >= config.LastSyncAt.Value.AddMinutes(config.IntervalMinutes);

            if (!isDue) continue;

            // 乐观更新，防止重启或长时同步期间重复触发
            config.LastSyncAt = now;
            config.NextSyncAt = now.AddMinutes(config.IntervalMinutes);
            await context.SaveChangesAsync(cancellationToken);

            await SyncAccountAsync(account.Id, "scheduled", cancellationToken);
            synced++;
        }

        if (synced > 0)
            _logger.LogInformation("本轮同步完成，共同步 {Count} 个账号", synced);
    }

    /// <summary>
    /// 执行单个账号的余额同步（独立 Scope，DbContext 互不干扰）
    /// </summary>
    private async Task SyncAccountAsync(int accountId, string triggeredBy, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var balanceService = scope.ServiceProvider.GetRequiredService<BalanceService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        _logger.LogInformation("开始同步账号 {AccountId} 的余额（触发原因：{Reason}）", accountId, triggeredBy);

        var history = await balanceService.RefreshAndSaveBalanceAsync(accountId, triggeredBy, cancellationToken);

        // 用实际完成时间更新同步配置
        var syncConfig = await unitOfWork.SyncConfigurations.FirstOrDefaultAsync(c => c.CloudAccountId == accountId)
                      ?? await unitOfWork.SyncConfigurations.FirstOrDefaultAsync(c => c.CloudAccountId == null);

        if (syncConfig != null)
        {
            syncConfig.LastSyncAt = DateTime.UtcNow;
            syncConfig.NextSyncAt = DateTime.UtcNow.AddMinutes(syncConfig.IntervalMinutes);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (history != null)
        {
            var status = history.Status == "Success" ? "成功" : "失败";
            _logger.LogInformation("账号 {AccountId} 余额同步{Status}", accountId, status);
        }
    }
}
