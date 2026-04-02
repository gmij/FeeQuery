namespace FeeQuery.Shared.Models;

/// <summary>
/// 预警规则
/// </summary>
public class AlertRule
{
    /// <summary>
    /// 规则ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 规则名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 关联的云账号ID（null表示对所有账号生效）
    /// </summary>
    public int? CloudAccountId { get; set; }

    /// <summary>
    /// 预警阈值
    /// </summary>
    public decimal Threshold { get; set; }

    /// <summary>
    /// 预警��型（balance: 余额预警, billing: 费用预警）
    /// </summary>
    public string AlertType { get; set; } = "billing";

    /// <summary>
    /// 统计周期类型（如 "daily", "monthly", "custom"）
    /// 仅用于费用预警
    /// </summary>
    public string PeriodType { get; set; } = "daily";

    /// <summary>
    /// 比较运算符（用于余额预警：less_than: 小于阈值, less_than_or_equal: 小于等于阈值）
    /// </summary>
    public string ComparisonOperator { get; set; } = "less_than";

    /// <summary>
    /// 余额类型（仅用于余额预警）
    /// available: 现金余额, total: 总可用余额（现金+信用额度）
    /// </summary>
    public string BalanceType { get; set; } = "available";

    /// <summary>
    /// 重复通知间隔（分钟），0表示不重复通知
    /// 例如：360表示6小时后再次通知，1440表示24小时后再次通知
    /// </summary>
    public int RepeatNotificationInterval { get; set; } = 0;

    /// <summary>
    /// 最大重复通知次数，null表示无限制
    /// 例如：3表示最多重复通知3次
    /// </summary>
    public int? MaxRepeatCount { get; set; } = null;

    /// <summary>
    /// 确认后静默时长（分钟）
    /// 0 = 永久静默，直到余额恢复或手动解决
    /// 大于0 = 静默指定分钟后恢复重复通知
    /// 默认 480（8小时）
    /// </summary>
    public int AcknowledgeSnoozeDuration { get; set; } = 480;

    /// <summary>
    /// 是否持续静默
    /// false（默认）= 方案A：静默到期后重置为已通知，交还给 RepeatNotificationInterval 接管
    /// true = 方案B：静默到期后发一次通知，然后自动进入下一个静默周期
    /// </summary>
    public bool ContinuousSnooze { get; set; } = false;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 通知配置ID列表（多个ID用逗号分隔，如 "1,2,3"）
    /// 优先使用此字段，关联到 NotificationConfig 表
    /// </summary>
    public string NotificationConfigIds { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的云账号
    /// </summary>
    public CloudAccount? CloudAccount { get; set; }
}
