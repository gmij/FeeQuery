namespace FeeQuery.Shared.Models;

/// <summary>
/// 预警历史记录
/// </summary>
public class AlertHistory
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 预警规则ID
    /// </summary>
    public int AlertRuleId { get; set; }

    /// <summary>
    /// 云账号ID
    /// </summary>
    public int CloudAccountId { get; set; }

    /// <summary>
    /// 触发时间
    /// </summary>
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 实际费用
    /// </summary>
    public decimal ActualAmount { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public decimal ThresholdAmount { get; set; }

    /// <summary>
    /// 超出百分比
    /// </summary>
    public decimal ExceedPercentage { get; set; }

    /// <summary>
    /// 状态（如 "triggered", "notified", "acknowledged", "resolved"）
    /// </summary>
    public string Status { get; set; } = "triggered";

    /// <summary>
    /// 通知是否发送成功
    /// </summary>
    public bool NotificationSent { get; set; }

    /// <summary>
    /// 通知发送时间
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    /// <summary>
    /// 重复通知次数
    /// </summary>
    public int RepeatNotificationCount { get; set; } = 0;

    /// <summary>
    /// 最后一次通知时间（用于计算是否需要重复通知）
    /// </summary>
    public DateTime? LastNotificationAt { get; set; }

    /// <summary>
    /// 确认时间
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// 确认用户
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 关联的预警规则
    /// </summary>
    public AlertRule? AlertRule { get; set; }

    /// <summary>
    /// 关联的云账号
    /// </summary>
    public CloudAccount? CloudAccount { get; set; }
}
