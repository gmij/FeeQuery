using FeeQuery.Core.Interfaces;
using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Models;
using FeeQuery.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace FeeQuery.Core.Services;

/// <summary>
/// 余额预警服务
/// </summary>
public class BalanceAlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationService _notificationService;
    private readonly ILogger<BalanceAlertService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<INotificationProvider> _notificationProviders;
    private readonly IAlertChecker _balanceAlertChecker;
    private readonly IAlertNotificationBuilder _notificationBuilder;

    public BalanceAlertService(
        IUnitOfWork unitOfWork,
        NotificationService notificationService,
        ILogger<BalanceAlertService> logger,
        IConfiguration configuration,
        IEnumerable<INotificationProvider> notificationProviders,
        IAlertChecker balanceAlertChecker,
        IAlertNotificationBuilder notificationBuilder)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
        _configuration = configuration;
        _notificationProviders = notificationProviders;
        _balanceAlertChecker = balanceAlertChecker;
        _notificationBuilder = notificationBuilder;
    }

    /// <summary>
    /// 检查指定账号的余额预警
    /// </summary>
    public async Task CheckBalanceAlertsAsync(
        int cloudAccountId,
        decimal availableBalance,
        decimal? creditLimit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _unitOfWork.CloudAccounts.GetByIdAsync(cloudAccountId, cancellationToken);
            if (account == null)
            {
                return;
            }

            // 准备检查上下文
            var context = new AlertCheckContext
            {
                Balance = new AccountBalance
                {
                    AvailableBalance = availableBalance,
                    CreditLimit = creditLimit,
                    Currency = "CNY"
                }
            };

            // 获取该账号的所有启用的余额预警规则
            var specificRules = await _unitOfWork.AlertRules
                .GetActiveRulesForAccountAsync(cloudAccountId, "balance", cancellationToken);

            // 如果有特定规则，只使用特定规则；否则查询全局规则
            List<AlertRule> alertRules;
            if (specificRules.Any())
            {
                alertRules = specificRules;
            }
            else
            {
                var allEnabledRules = await _unitOfWork.AlertRules.GetEnabledRulesAsync(cancellationToken);
                alertRules = allEnabledRules.Where(r => r.AlertType == "balance" && r.CloudAccountId == null).ToList();
            }

            _logger.LogInformation("检查账号 {AccountId} 的余额预警: 现金余额={Available}, 信用额度={Credit}, 找到启用规则数={RuleCount}",
                cloudAccountId, availableBalance, creditLimit, alertRules.Count);

            var activeRuleIds = alertRules.Select(r => r.Id).ToHashSet();

            // 步骤1: 检查未解决的预警，自动解决已恢复的预警
            await AutoResolveRecoveredAlertsAsync(cloudAccountId, context, account, alertRules, activeRuleIds, cancellationToken);

            // 步骤2: 检查是否需要发送重复通知
            await ProcessRepeatNotificationsAsync(cloudAccountId, context, account, alertRules, cancellationToken);

            // 步骤3: 检查是否触发新预警
            await CheckAndTriggerNewAlertsAsync(cloudAccountId, context, account, alertRules, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查账号 {AccountId} 余额预警失败", cloudAccountId);
            throw;
        }
    }

    /// <summary>
    /// 自动解决已恢复的预警
    /// </summary>
    private async Task AutoResolveRecoveredAlertsAsync(
        int cloudAccountId,
        AlertCheckContext context,
        CloudAccount account,
        List<AlertRule> alertRules,
        HashSet<int> activeRuleIds,
        CancellationToken cancellationToken)
    {
        var unresolvedAlerts = await _unitOfWork.AlertHistories
            .GetUnresolvedAlertsAsync(cloudAccountId, cancellationToken);

        _logger.LogInformation("自动解决检查: 账号 {AccountId} 有 {Count} 条未解决的预警",
            cloudAccountId, unresolvedAlerts.Count);

        foreach (var alert in unresolvedAlerts)
        {
            if (alert.AlertRule == null)
            {
                _logger.LogWarning("预警 {AlertId} 的关联规则为空，跳过", alert.Id);
                continue;
            }

            // 检查1：规则是否已经不再活跃（规则切换）
            if (!activeRuleIds.Contains(alert.AlertRuleId))
            {
                alert.Status = "resolved";
                alert.Remark = $"系统自动解决：预警规则已切换";
                _unitOfWork.AlertHistories.Update(alert);
                _logger.LogInformation("自动解决预警（规则切换）: AlertId={AlertId}", alert.Id);
                continue;
            }

            // 检查2：规则被禁用
            if (!alert.AlertRule.IsEnabled)
            {
                _logger.LogInformation("跳过已禁用规则的预警: AlertId={AlertId}", alert.Id);
                continue;
            }

            // 检查3：已确认的预警，若余额降至 0 或以下，且规则未配置永久静默，忽略静默期立即升级
            if (alert.Status == "acknowledged" &&
                alert.AlertRule?.AcknowledgeSnoozeDuration > 0 &&
                context.Balance?.AvailableBalance <= 0)
            {
                var escalationNote = $"系统升级通知：余额已降至 {context.Balance.AvailableBalance:F2} 元，忽略静默期";
                alert.Status = "notified";
                alert.Remark = string.IsNullOrEmpty(alert.Remark)
                    ? escalationNote
                    : $"{alert.Remark}\n{escalationNote}";
                // 将 LastNotificationAt 设为超过一个通知间隔之前，确保重复通知立即触发
                alert.LastNotificationAt = DateTime.UtcNow.AddHours(-2);
                _unitOfWork.AlertHistories.Update(alert);
                _logger.LogWarning("余额降至零或以下，升级已确认的预警: AlertId={AlertId}, Balance={Balance}",
                    alert.Id, context.Balance.AvailableBalance);
                continue;
            }

            // 检查4：使用 AlertChecker 检查是否已恢复
            bool isRecovered = await _balanceAlertChecker.IsRecoveredAsync(alert, context, cancellationToken);

            if (isRecovered)
            {
                alert.Status = "resolved";
                alert.Remark = $"系统自动解决：余额已恢复";
                _unitOfWork.AlertHistories.Update(alert);

                _logger.LogInformation("自动解决预警（余额恢复）: AlertId={AlertId}, AccountId={AccountId}",
                    alert.Id, cloudAccountId);

                // 发送预警解除通知
                try
                {
                    await SendRecoveryNotificationAsync(alert, account, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送预警解除通知失败: AlertId={AlertId}", alert.Id);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 发送恢复通知
    /// </summary>
    private async Task SendRecoveryNotificationAsync(
        AlertHistory alert,
        CloudAccount account,
        AlertCheckContext context,
        CancellationToken cancellationToken)
    {
        if (alert.AlertRule == null || context.Balance == null)
        {
            return;
        }

        var rule = alert.AlertRule;

        // 根据规则的 BalanceType 计算实际值，与触发通知保持一致
        var actualValue = rule.BalanceType == "total"
            ? context.Balance.AvailableBalance + (context.Balance.CreditLimit ?? 0)
            : context.Balance.AvailableBalance;

        var notificationContext = new AlertNotificationContext
        {
            Alert = alert,
            Rule = rule,
            Account = account,
            Balance = context.Balance,
            ActualValue = actualValue,
            ThresholdValue = rule.Threshold,
            BaseUrl = _configuration.GetValue<string>("FeeQuery:BaseUrl"),
            IsRecoveryNotification = true
        };

        string title = _notificationBuilder.BuildTitle(notificationContext);
        string content = _notificationBuilder.BuildContent(notificationContext);

        // 优先使用规则配置的 NotificationConfigIds，与触发通知走同一套渠道
        if (!string.IsNullOrEmpty(rule.NotificationConfigIds))
        {
            var configIds = rule.NotificationConfigIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                .Where(id => id > 0)
                .ToList();

            if (configIds.Any())
            {
                var allEnabledConfigs = await _unitOfWork.NotificationConfigs.GetEnabledConfigsAsync(cancellationToken);
                var notificationConfigs = allEnabledConfigs.Where(c => configIds.Contains(c.Id)).ToList();

                foreach (var config in notificationConfigs)
                {
                    try
                    {
                        await _notificationService.SendNotificationByConfigAsync(
                            config.Id,
                            title,
                            content,
                            cancellationToken);

                        _logger.LogInformation("已通过通知配置 {ConfigName} 发送恢复通知", config.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "通过通知配置 {ConfigName} 发送恢复通知失败", config.Name);
                    }
                }

                return;
            }
        }

        _logger.LogWarning("预警规则 {RuleName} 未配置任何通知渠道，恢复通知未发送", rule.Name);
    }

    /// <summary>
    /// 处理重复通知
    /// </summary>
    private async Task ProcessRepeatNotificationsAsync(
        int cloudAccountId,
        AlertCheckContext context,
        CloudAccount account,
        List<AlertRule> alertRules,
        CancellationToken cancellationToken)
    {
        // 获取所有已通知但未解决的预警
        // 获取已通知或已确认的预警，用于检查是否需要重复通知
        var allNotifiedAlerts = await _unitOfWork.AlertHistories.GetAlertsNeedingRepeatNotificationAsync(cancellationToken);
        var notifiedAlerts = allNotifiedAlerts.Where(h => h.CloudAccountId == cloudAccountId).ToList();

        foreach (var alert in notifiedAlerts)
        {
            if (alert.AlertRule == null) continue;

            // 检查是否启用了重复通知
            if (alert.AlertRule.RepeatNotificationInterval <= 0) continue;

            // 检查是否超过了最大重复次数
            if (alert.AlertRule.MaxRepeatCount.HasValue &&
                alert.RepeatNotificationCount >= alert.AlertRule.MaxRepeatCount.Value)
            {
                _logger.LogDebug("预警ID={AlertId}已达到最大重复通知次数{MaxCount}",
                    alert.Id, alert.AlertRule.MaxRepeatCount.Value);
                continue;
            }

            // 检查是否到了重复通知时间
            var lastNotificationTime = alert.LastNotificationAt ?? alert.NotifiedAt ?? DateTime.UtcNow;
            var nextNotificationTime = lastNotificationTime.AddMinutes(alert.AlertRule.RepeatNotificationInterval);

            if (DateTime.UtcNow >= nextNotificationTime)
            {
                // 发送重复通知
                try
                {
                    await SendAlertNotificationAsync(
                        alert,
                        alert.AlertRule,
                        account,
                        context,
                        cancellationToken,
                        isRepeat: true,
                        repeatCount: alert.RepeatNotificationCount + 1);

                    alert.RepeatNotificationCount++;
                    alert.LastNotificationAt = DateTime.UtcNow;

                    if (alert.Status == "acknowledged")
                    {
                        if (alert.AlertRule.ContinuousSnooze)
                        {
                            // 方案B：持续静默——重置 AcknowledgedAt，进入下一个静默周期
                            alert.AcknowledgedAt = DateTime.UtcNow;
                            _logger.LogInformation("持续静默模式：重置静默计时，AlertId={AlertId}", alert.Id);
                        }
                        else
                        {
                            // 方案A：静默到期后交还给 RepeatNotificationInterval 接管
                            alert.Status = "notified";
                            _logger.LogInformation("静默到期，重置为已通知状态，AlertId={AlertId}", alert.Id);
                        }
                    }

                    _unitOfWork.AlertHistories.Update(alert);

                    _logger.LogInformation("发送重复预警通知: AlertId={AlertId}, 第{Count}次通知",
                        alert.Id, alert.RepeatNotificationCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送重复通知失败: AlertId={AlertId}", alert.Id);
                }
            }
        }

        if (notifiedAlerts.Any())
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 检查并触发新预警
    /// </summary>
    private async Task CheckAndTriggerNewAlertsAsync(
        int cloudAccountId,
        AlertCheckContext context,
        CloudAccount account,
        List<AlertRule> alertRules,
        CancellationToken cancellationToken)
    {
        foreach (var rule in alertRules)
        {
            // 使用AlertChecker检查是否触发预警
            var checkResult = await _balanceAlertChecker.CheckAsync(account, rule, context, cancellationToken);

            if (!checkResult.IsTriggered)
            {
                continue;
            }

            // 检查是否已经有未处理的预警记录（避免重复创建）
            var existingAlert = await _unitOfWork.AlertHistories
                .GetActiveAlertByRuleAsync(rule.Id, cloudAccountId, cancellationToken);

            if (existingAlert != null)
            {
                _logger.LogDebug("账号 {AccountId} 规则 {RuleId} 已存在未解决的预警记录，跳过创建新预警", cloudAccountId, rule.Id);
                continue;
            }

            // 创建新的预警历史记录
            var history = new AlertHistory
            {
                AlertRuleId = rule.Id,
                CloudAccountId = cloudAccountId,
                TriggeredAt = DateTime.UtcNow,
                ActualAmount = checkResult.ActualValue,
                ThresholdAmount = checkResult.ThresholdValue,
                ExceedPercentage = checkResult.ExceedPercentage ?? 0,
                Status = "triggered",
                NotificationSent = false
            };

            await _unitOfWork.AlertHistories.AddAsync(history, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("触发余额预警: 账号 {AccountName} ({AccountId}), 当前余额: {Balance}, 阈值: {Threshold}",
                account.Name, cloudAccountId, checkResult.ActualValue, checkResult.ThresholdValue);

            // 发送通知
            try
            {
                await SendAlertNotificationAsync(history, rule, account, context, cancellationToken);

                history.NotificationSent = true;
                history.NotifiedAt = DateTime.UtcNow;
                history.LastNotificationAt = DateTime.UtcNow;
                history.Status = "notified";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送预警通知失败");
                history.NotificationSent = false;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// <summary>
    /// 发送预警通知
    /// </summary>
    private async Task SendAlertNotificationAsync(
        AlertHistory history,
        AlertRule rule,
        CloudAccount account,
        AlertCheckContext context,
        CancellationToken cancellationToken,
        bool isRepeat = false,
        int repeatCount = 0)
    {
        if (context.Balance == null)
        {
            return;
        }

        var notificationContext = new AlertNotificationContext
        {
            Alert = history,
            Rule = rule,
            Account = account,
            Balance = context.Balance,
            ActualValue = history.ActualAmount,
            ThresholdValue = history.ThresholdAmount,
            ExceedPercentage = history.ExceedPercentage,
            BaseUrl = _configuration.GetValue<string>("FeeQuery:BaseUrl"),
            IsRecoveryNotification = false
        };

        string title = _notificationBuilder.BuildTitle(notificationContext);
        if (isRepeat)
        {
            title += $" - 第{repeatCount}次提醒";
        }

        string content = _notificationBuilder.BuildContent(notificationContext);

        // 优先使用新的 NotificationConfigIds 字段
        if (!string.IsNullOrEmpty(rule.NotificationConfigIds))
        {
            var configIds = rule.NotificationConfigIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                .Where(id => id > 0)
                .ToList();

            if (configIds.Any())
            {
                var allEnabledConfigs = await _unitOfWork.NotificationConfigs.GetEnabledConfigsAsync(cancellationToken);
                var notificationConfigs = allEnabledConfigs.Where(c => configIds.Contains(c.Id)).ToList();

                foreach (var config in notificationConfigs)
                {
                    try
                    {
                        await _notificationService.SendNotificationByConfigAsync(
                            config.Id,
                            title,
                            content,
                            cancellationToken);

                        _logger.LogInformation("已通过通知配置 {ConfigName} 发送预警通知", config.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "通过通知配置 {ConfigName} 发送通知失败", config.Name);
                    }
                }

                return;
            }
        }

        _logger.LogWarning("预警规则 {RuleName} 未配置任何通知渠道", rule.Name);
    }

    /// <summary>
    /// 确认预警
    /// </summary>
    public async Task AcknowledgeAlertAsync(long alertId, string? acknowledgedBy = null, string? remark = null, CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.AlertHistories.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            throw new InvalidOperationException($"预警记录 {alertId} 不存在");
        }

        alert.Status = "acknowledged";
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = acknowledgedBy;
        if (!string.IsNullOrEmpty(remark))
        {
            alert.Remark = remark;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("预警记录 {AlertId} 已被确认", alertId);
    }

    /// <summary>
    /// 解决预警
    /// </summary>
    public async Task ResolveAlertAsync(long alertId, string? remark = null, CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.AlertHistories.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            throw new InvalidOperationException($"预警记录 {alertId} 不存在");
        }

        alert.Status = "resolved";
        if (!string.IsNullOrEmpty(remark))
        {
            alert.Remark = remark;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("预警记录 {AlertId} 已被解决", alertId);
    }

    /// <summary>
    /// 获取未处理的预警列表
    /// </summary>
    public async Task<List<AlertHistory>> GetUnresolvedAlertsAsync(int? cloudAccountId = null, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.AlertHistories.GetUnresolvedAlertsAsync(cloudAccountId, cancellationToken);
    }
}
