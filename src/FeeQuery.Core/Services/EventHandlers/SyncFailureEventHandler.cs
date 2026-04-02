using FeeQuery.Data;
using FeeQuery.Shared.Events;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services.EventHandlers;

/// <summary>
/// 同步失败事件处理器 - 创建预警记录并发送钉钉通知
/// </summary>
public class SyncFailureEventHandler : IEventHandler<BalanceRefreshedEvent>
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<SyncFailureEventHandler> _logger;

    public SyncFailureEventHandler(
        ApplicationDbContext context,
        NotificationService notificationService,
        ILogger<SyncFailureEventHandler> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(BalanceRefreshedEvent @event, CancellationToken cancellationToken = default)
    {
        // 只处理失败的余额刷新
        if (@event.IsSuccess)
        {
            return;
        }

        try
        {
            _logger.LogInformation("处理同步失败事件: AccountId={AccountId}, 错误={Error}",
                @event.CloudAccountId, @event.ErrorMessage);

            // 获取账号信息
            var account = await _context.CloudAccounts.FindAsync(@event.CloudAccountId);
            if (account == null)
            {
                _logger.LogWarning("账号不存在: AccountId={AccountId}", @event.CloudAccountId);
                return;
            }

            // 查找或创建"同步失败"预警规则
            var syncFailureRule = await GetOrCreateSyncFailureRuleAsync(@event.CloudAccountId, cancellationToken);

            // 创建预警历史记录
            var alertHistory = new AlertHistory
            {
                AlertRuleId = syncFailureRule.Id,
                CloudAccountId = @event.CloudAccountId,
                TriggeredAt = @event.RefreshedAt,
                ActualAmount = 0, // 同步失败时没有实际金额
                ThresholdAmount = 0, // 同步失败没有阈值概念
                ExceedPercentage = 0,
                Status = "triggered",
                NotificationSent = false,
                Remark = @event.ErrorMessage
            };

            _context.AlertHistories.Add(alertHistory);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已创建同步失败预警记录: HistoryId={HistoryId}, AccountId={AccountId}",
                alertHistory.Id, @event.CloudAccountId);

            // 发送钉钉通知
            try
            {
                var sourceText = @event.Source switch
                {
                    "manual" => "手动同步",
                    "scheduled" => "定时同步",
                    "initial" => "初始化同步",
                    _ => @event.Source
                };

                var title = $"【余额同步失败】{account.ProviderName} - {account.Name}";
                var content = $"**账号信息：**\n\n" +
                              $"- 厂商：{account.ProviderName}\n" +
                              $"- 账号：{account.Name}\n" +
                              $"- 账号ID：{account.Id}\n" +
                              $"- 同步方式：{sourceText}\n\n" +
                              $"**错误信息：**\n\n{@event.ErrorMessage}\n\n" +
                              $"**同步时间：**{@event.RefreshedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n\n" +
                              $"请检查账号凭证配置或网络连接。";

                var notificationSent = await _notificationService.SendNotificationAsync("dingtalk", title, content, cancellationToken);

                // 更新通知发送状态
                alertHistory.NotificationSent = notificationSent;
                alertHistory.NotifiedAt = notificationSent ? DateTime.UtcNow : null;
                alertHistory.Status = notificationSent ? "notified" : "triggered";
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("同步失败通知发送{Status}: AccountId={AccountId}",
                    notificationSent ? "成功" : "失败", @event.CloudAccountId);
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "发送同步失败通知时出错: AccountId={AccountId}", @event.CloudAccountId);
            }
        }
        catch (Exception ex)
        {
            // 处理失败不影响其他流程
            _logger.LogError(ex, "处理同步失败事件失败: AccountId={AccountId}", @event.CloudAccountId);
        }
    }

    /// <summary>
    /// 获取或创建同步失败预警规则
    /// </summary>
    private async Task<AlertRule> GetOrCreateSyncFailureRuleAsync(int cloudAccountId, CancellationToken cancellationToken)
    {
        // 查找该账号的同步失败规则
        var rule = await _context.AlertRules
            .FirstOrDefaultAsync(r => r.AlertType == "sync_failure" && r.CloudAccountId == cloudAccountId, cancellationToken);

        if (rule == null)
        {
            // 创建新的同步失败预警规则
            rule = new AlertRule
            {
                Name = $"同步失败预警",
                CloudAccountId = cloudAccountId,
                AlertType = "sync_failure",
                Threshold = 0,
                IsEnabled = true,
                NotificationConfigIds = "",
                Remark = "系统自动创建的同步失败预警规则"
            };

            _context.AlertRules.Add(rule);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已创建同步失败预警规则: RuleId={RuleId}, AccountId={AccountId}",
                rule.Id, cloudAccountId);
        }

        return rule;
    }
}
