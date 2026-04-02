using FeeQuery.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.NotificationBuilders;

/// <summary>
/// 余额预警通知构建器
/// </summary>
public class BalanceAlertNotificationBuilder : IAlertNotificationBuilder
{
    private readonly ILogger<BalanceAlertNotificationBuilder> _logger;

    public string AlertType => "balance";

    public BalanceAlertNotificationBuilder(ILogger<BalanceAlertNotificationBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 构建通知标题
    /// </summary>
    public string BuildTitle(AlertNotificationContext context)
    {
        if (context.IsRecoveryNotification)
        {
            return $"【已恢复】{context.Account.ProviderName} - {context.Account.Name} 余额预警已恢复";
        }

        return $"【余额预警】{context.Account.ProviderName} - {context.Account.Name}";
    }

    /// <summary>
    /// 构建通知内容（纯文本）
    /// </summary>
    public string BuildContent(AlertNotificationContext context)
    {
        if (context.IsRecoveryNotification)
        {
            return BuildRecoveryContent(context);
        }

        // 触发时计费余额的标签（区分规则基于可用余额还是总余额）
        var triggerLabel = context.Rule.BalanceType == "total" ? "触发时计费余额" : "触发时可用余额";

        var content = $"云厂商: {context.Account.ProviderName}\n" +
                     $"账号名称: {context.Account.Name}\n" +
                     $"预警规则: {context.Rule.Name}\n";

        // 当前实时余额（最关键，放在最前）
        if (context.Balance != null)
        {
            content += $"当前可用余额: {context.Balance.AvailableBalance:F2} 元\n";
            if (context.Balance.CreditLimit.HasValue && context.Balance.CreditLimit > 0)
            {
                content += $"信用额度: {context.Balance.CreditLimit:F2} 元\n";
            }
        }

        // 用当前可用余额实时计算偏离度，紧跟可用余额展示，无余额信息时回退到触发时快照值
        decimal? exceedPercentage = null;
        if (context.Balance != null && context.ThresholdValue > 0)
        {
            exceedPercentage = ((context.ThresholdValue - context.Balance.AvailableBalance) / context.ThresholdValue) * 100;
        }
        else if (context.ExceedPercentage.HasValue)
        {
            exceedPercentage = context.ExceedPercentage;
        }

        if (exceedPercentage.HasValue)
        {
            content += $"低于阈值: -{exceedPercentage.Value:F1}%\n";
        }

        // 触发时的历史快照与阈值对比
        content += $"预警阈值: {context.ThresholdValue:F2} 元\n" +
                  $"{triggerLabel}: {context.ActualValue:F2} 元\n";

        content += $"触发时间: {context.Alert.TriggeredAt:yyyy-MM-dd HH:mm:ss}\n";

        return content;
    }

    /// <summary>
    /// 构建富文本内容（Markdown）
    /// </summary>
    public string? BuildRichContent(AlertNotificationContext context)
    {
        if (context.IsRecoveryNotification)
        {
            return BuildRecoveryRichContent(context);
        }

        var triggerLabel = context.Rule.BalanceType == "total" ? "触发时计费余额" : "触发时可用余额";

        var markdown = $"## 🔴 余额预警\n\n" +
                      $"**云厂商**: {context.Account.ProviderName}\n\n" +
                      $"**账号名称**: {context.Account.Name}\n\n" +
                      $"**预警规则**: {context.Rule.Name}\n\n" +
                      $"---\n\n";

        // 当前实时余额（最关键）
        if (context.Balance != null)
        {
            markdown += $"### 当前余额状况\n\n" +
                       $"- **当前可用余额**: `{context.Balance.AvailableBalance:F2}` 元\n";
            if (context.Balance.CreditLimit.HasValue && context.Balance.CreditLimit > 0)
            {
                markdown += $"- **信用额度**: `{context.Balance.CreditLimit:F2}` 元\n";
            }
        }

        // 用当前可用余额实时计算偏离度，紧跟可用余额展示，无余额信息时回退到触发时快照值
        decimal? exceedPercentage = null;
        if (context.Balance != null && context.ThresholdValue > 0)
        {
            exceedPercentage = ((context.ThresholdValue - context.Balance.AvailableBalance) / context.ThresholdValue) * 100;
        }
        else if (context.ExceedPercentage.HasValue)
        {
            exceedPercentage = context.ExceedPercentage;
        }

        if (exceedPercentage.HasValue)
        {
            markdown += $"- **低于阈值**: `-{exceedPercentage.Value:F1}%`\n";
        }

        markdown += "\n";

        // 触发时的历史快照与阈值对比
        markdown += $"### 预警触发详情\n\n" +
                   $"- **预警阈值**: `{context.ThresholdValue:F2}` 元\n" +
                   $"- **{triggerLabel}**: `{context.ActualValue:F2}` 元\n";

        markdown += $"\n---\n\n" +
                   $"**触发时间**: {context.Alert.TriggeredAt:yyyy-MM-dd HH:mm:ss}\n\n";

        return markdown;
    }

    /// <summary>
    /// 构建操作按钮
    /// </summary>
    public List<AlertActionButton>? BuildActions(AlertNotificationContext context)
    {
        if (string.IsNullOrEmpty(context.BaseUrl))
        {
            return null;
        }

        var actions = new List<AlertActionButton>
        {
            new AlertActionButton
            {
                Text = "查看详情",
                Url = $"{context.BaseUrl}/alerts/{context.Alert.Id}",
                Style = "primary"
            },
            new AlertActionButton
            {
                Text = "查看账号",
                Url = $"{context.BaseUrl}/accounts/{context.Account.Id}",
                Style = "default"
            }
        };

        return actions;
    }

    /// <summary>
    /// 构建恢复通知内容
    /// </summary>
    private string BuildRecoveryContent(AlertNotificationContext context)
    {
        return $"云厂商: {context.Account.ProviderName}\n" +
               $"账号名称: {context.Account.Name}\n" +
               $"预警规则: {context.Rule.Name}\n" +
               $"当前余额: {context.ActualValue:F2} 元\n" +
               $"预警阈值: {context.ThresholdValue:F2} 元\n" +
               $"恢复时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n" +
               $"原预警触发时间: {context.Alert.TriggeredAt:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// 构建恢复通知的富文本内容
    /// </summary>
    private string BuildRecoveryRichContent(AlertNotificationContext context)
    {
        return $"## ✅ 余额预警已恢复\n\n" +
               $"**云厂商**: {context.Account.ProviderName}\n\n" +
               $"**账号名称**: {context.Account.Name}\n\n" +
               $"**预警规则**: {context.Rule.Name}\n\n" +
               $"---\n\n" +
               $"### 恢复信息\n\n" +
               $"- **当前余额**: `{context.ActualValue:F2}` 元\n" +
               $"- **预警阈值**: `{context.ThresholdValue:F2}` 元\n" +
               $"- **恢复时间**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n" +
               $"- **原预警时间**: {context.Alert.TriggeredAt:yyyy-MM-dd HH:mm:ss}\n";
    }
}
