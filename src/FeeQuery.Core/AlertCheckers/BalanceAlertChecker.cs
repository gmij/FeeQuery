using FeeQuery.Core.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.AlertCheckers;

/// <summary>
/// 余额预警检查器
/// </summary>
public class BalanceAlertChecker : IAlertChecker
{
    private readonly ILogger<BalanceAlertChecker> _logger;

    public string AlertType => "balance";

    public BalanceAlertChecker(ILogger<BalanceAlertChecker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查余额是否触发预警
    /// </summary>
    public Task<AlertCheckResult> CheckAsync(
        CloudAccount account,
        AlertRule rule,
        AlertCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Balance == null)
        {
            _logger.LogWarning("余额预警检查失败: 账号 {AccountId} 的余额信息为空", account.Id);
            return Task.FromResult(new AlertCheckResult { IsTriggered = false });
        }

        var balance = context.Balance;
        var result = new AlertCheckResult();

        // 根据BalanceType选择要检查的余额
        // available: 仅现金余额, total: 总可用余额（现金+信用额度）
        decimal balanceToCheck = rule.BalanceType == "total"
            ? balance.AvailableBalance + (balance.CreditLimit ?? 0)
            : balance.AvailableBalance;

        // 根据比较类型进行判断
        switch (rule.ComparisonOperator)
        {
            case "less_than":
                result.IsTriggered = balanceToCheck < rule.Threshold;
                break;
            case "less_than_or_equal":
                result.IsTriggered = balanceToCheck <= rule.Threshold;
                break;
            case "greater_than":
                result.IsTriggered = balanceToCheck > rule.Threshold;
                break;
            case "greater_than_or_equal":
                result.IsTriggered = balanceToCheck >= rule.Threshold;
                break;
            case "equal":
                result.IsTriggered = Math.Abs(balanceToCheck - rule.Threshold) < 0.01m;
                break;
            default:
                _logger.LogWarning("不支持的比较类型: {ComparisonType}", rule.ComparisonOperator);
                result.IsTriggered = false;
                break;
        }

        result.ActualValue = balanceToCheck;
        result.ThresholdValue = rule.Threshold;

        // 计算超出百分比（如果是小于类型的预警）
        if (result.IsTriggered && rule.ComparisonOperator.Contains("less"))
        {
            if (rule.Threshold > 0)
            {
                result.ExceedPercentage = ((rule.Threshold - balanceToCheck) / rule.Threshold) * 100;
            }
        }

        if (result.IsTriggered)
        {
            _logger.LogInformation(
                "余额预警触发: 账号 {AccountId} ({AccountName}), 余额类型={BalanceType}, 当前余额 {ActualValue} {ComparisonType} 阈值 {ThresholdValue}",
                account.Id, account.Name, rule.BalanceType, balanceToCheck, rule.ComparisonOperator, rule.Threshold);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 检查余额预警是否已恢复
    /// </summary>
    public Task<bool> IsRecoveredAsync(
        AlertHistory alert,
        AlertCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Balance == null)
        {
            return Task.FromResult(false);
        }

        var balance = context.Balance;

        // 根据BalanceType选择要检查的余额
        // available: 仅现金余额, total: 总可用余额（现金+信用额度）
        decimal balanceToCheck = 0;

        // 恢复条件: 余额回到阈值之上（对于LessThan类型的预警）
        // 或余额回到阈值之下（对于GreaterThan类型的预警）
        bool isRecovered = false;

        if (alert.AlertRule != null)
        {
            var rule = alert.AlertRule;

            // 根据规则的BalanceType选择余额
            balanceToCheck = rule.BalanceType == "total"
                ? balance.AvailableBalance + (balance.CreditLimit ?? 0)
                : balance.AvailableBalance;

            switch (rule.ComparisonOperator)
            {
                case "less_than":
                case "less_than_or_equal":
                    // 余额恢复到阈值以上
                    isRecovered = balanceToCheck >= rule.Threshold;
                    break;
                case "greater_than":
                case "greater_than_or_equal":
                    // 余额降低到阈值以下
                    isRecovered = balanceToCheck <= rule.Threshold;
                    break;
            }
        }

        if (isRecovered)
        {
            _logger.LogInformation(
                "余额预警已恢复: AlertId={AlertId}, 余额类型={BalanceType}, 当前余额={CurrentBalance}",
                alert.Id, alert.AlertRule?.BalanceType, balanceToCheck);
        }

        return Task.FromResult(isRecovered);
    }
}
