using FeeQuery.Data;
using FeeQuery.Shared.Events;
using FeeQuery.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services.EventHandlers;

/// <summary>
/// 余额刷新事件处理器 - 触发预警检查和同步失败恢复通知
/// </summary>
public class BalanceRefreshedEventHandler : IEventHandler<BalanceRefreshedEvent>
{
    private readonly ApplicationDbContext _context;
    private readonly BalanceAlertService _alertService;
    private readonly Core.Services.NotificationService _notificationService;
    private readonly ILogger<BalanceRefreshedEventHandler> _logger;

    public BalanceRefreshedEventHandler(
        ApplicationDbContext context,
        BalanceAlertService alertService,
        Core.Services.NotificationService notificationService,
        ILogger<BalanceRefreshedEventHandler> logger)
    {
        _context = context;
        _alertService = alertService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(BalanceRefreshedEvent @event, CancellationToken cancellationToken = default)
    {
        // 只处理成功的余额刷新
        if (!@event.IsSuccess)
        {
            _logger.LogDebug("跳过失败的余额刷新事件: AccountId={AccountId}", @event.CloudAccountId);
            return;
        }

        try
        {
            _logger.LogInformation("处理余额刷新事件: AccountId={AccountId}, 现金余额={Available}, 信用额度={Credit}",
                @event.CloudAccountId, @event.AvailableBalance, @event.CreditLimit);

            // 检查余额预警（传入现金余额和信用额度，让预警服务根据规则配置决定使用哪个）
            await _alertService.CheckBalanceAlertsAsync(
                @event.CloudAccountId,
                @event.AvailableBalance,
                @event.CreditLimit,
                cancellationToken);

            _logger.LogDebug("余额预警检查完成: AccountId={AccountId}", @event.CloudAccountId);

            // 检查并解决同步失败预警
            await ResolveSyncFailureAlertsAsync(@event.CloudAccountId, cancellationToken);
        }
        catch (Exception ex)
        {
            // 预警检查失败不影响其他流程
            _logger.LogError(ex, "处理余额刷新事件失败: AccountId={AccountId}", @event.CloudAccountId);
        }
    }

    /// <summary>
    /// 解决同步失败预警（同步成功后自动解决）
    /// </summary>
    private async Task ResolveSyncFailureAlertsAsync(int cloudAccountId, CancellationToken cancellationToken)
    {
        try
        {
            // 查找该账号未解决的同步失败预警
            var unresolvedSyncFailures = await _context.AlertHistories
                .Include(h => h.AlertRule)
                .Include(h => h.CloudAccount)
                .Where(h => h.CloudAccountId == cloudAccountId &&
                           h.Status != "resolved" &&
                           h.AlertRule!.AlertType == "sync_failure")
                .ToListAsync(cancellationToken);

            if (!unresolvedSyncFailures.Any())
            {
                return;
            }

            _logger.LogInformation("发现 {Count} 条未解决的同步失败预警，准备自动解决: AccountId={AccountId}",
                unresolvedSyncFailures.Count, cloudAccountId);

            foreach (var alert in unresolvedSyncFailures)
            {
                // 更新预警状态为已解决
                alert.Status = "resolved";
                alert.Remark = $"{alert.Remark}\n\n系统自动解决：同步已恢复正常（解决时间：{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss}）";
                _context.AlertHistories.Update(alert);

                _logger.LogInformation("自动解决同步失败预警: AlertId={AlertId}, AccountId={AccountId}",
                    alert.Id, cloudAccountId);

                // 发送恢复通知
                try
                {
                    if (alert.CloudAccount != null)
                    {
                        var title = $"【同步恢复通知】{alert.CloudAccount.ProviderName} - {alert.CloudAccount.Name}";
                        var content = $"**账号信息：**\n\n" +
                                      $"- 厂商：{alert.CloudAccount.ProviderName}\n" +
                                      $"- 账号：{alert.CloudAccount.Name}\n" +
                                      $"- 账号ID：{alert.CloudAccount.Id}\n\n" +
                                      $"**恢复信息：**\n\n" +
                                      $"同步已恢复正常，之前的问题已解决。\n\n" +
                                      $"**原始错误：**\n\n{alert.Remark?.Split("\n\n")[0]}\n\n" +
                                      $"**恢复时间：**{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

                        await _notificationService.SendNotificationAsync("dingtalk", title, content, cancellationToken);

                        _logger.LogInformation("已发送同步恢复通知: AccountId={AccountId}, AlertId={AlertId}",
                            cloudAccountId, alert.Id);
                    }
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "发送同步恢复通知失败: AlertId={AlertId}", alert.Id);
                    // 通知发送失败不影响预警状态更新
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("已保存同步失败预警解决状态: AccountId={AccountId}", cloudAccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解决同步失败预警时出错: AccountId={AccountId}", cloudAccountId);
            // 不抛出异常，避免影响其他流程
        }
    }
}
