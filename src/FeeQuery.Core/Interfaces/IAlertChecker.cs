using FeeQuery.Shared.Models;

namespace FeeQuery.Core.Interfaces;

/// <summary>
/// 预警检查器接口
/// </summary>
public interface IAlertChecker
{
    /// <summary>
    /// 预警类型（如 "balance", "billing"）
    /// </summary>
    string AlertType { get; }

    /// <summary>
    /// 检查是否触发预警
    /// </summary>
    Task<AlertCheckResult> CheckAsync(
        CloudAccount account,
        AlertRule rule,
        AlertCheckContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查预警是否已恢复
    /// </summary>
    Task<bool> IsRecoveredAsync(
        AlertHistory alert,
        AlertCheckContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 预警检查上下文
/// </summary>
public class AlertCheckContext
{
    /// <summary>
    /// 账户余额信息（用于余额预警）
    /// </summary>
    public AccountBalance? Balance { get; set; }

    /// <summary>
    /// 账单记录列表（用于费用预警）
    /// </summary>
    public List<BillingRecord>? BillingRecords { get; set; }

    /// <summary>
    /// 自定义扩展数据
    /// </summary>
    public Dictionary<string, object>? ExtensionData { get; set; }
}

/// <summary>
/// 预警检查结果
/// </summary>
public class AlertCheckResult
{
    /// <summary>
    /// 是否触发预警
    /// </summary>
    public bool IsTriggered { get; set; }

    /// <summary>
    /// 实际值
    /// </summary>
    public decimal ActualValue { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public decimal ThresholdValue { get; set; }

    /// <summary>
    /// 超出百分比（可选）
    /// </summary>
    public decimal? ExceedPercentage { get; set; }

    /// <summary>
    /// 附加描述信息
    /// </summary>
    public string? Description { get; set; }
}
