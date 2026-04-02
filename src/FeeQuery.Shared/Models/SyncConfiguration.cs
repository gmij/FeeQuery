namespace FeeQuery.Shared.Models;

/// <summary>
/// 定时同步配置
/// 支持全局和账号级别的刷新策略
/// </summary>
public class SyncConfiguration
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 云账号ID（null表示全局配置）
    /// </summary>
    public int? CloudAccountId { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 同步间隔（分钟）
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 是否同步余额
    /// </summary>
    public bool SyncBalance { get; set; } = true;

    /// <summary>
    /// 是否同步费用
    /// </summary>
    public bool SyncBilling { get; set; } = false;

    /// <summary>
    /// 同步时间段（cron表达式，如 "0 */1 * * *" 表示每小时）
    /// 如果为空，则使用IntervalMinutes
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 最后同步时间
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// 下次同步时间
    /// </summary>
    public DateTime? NextSyncAt { get; set; }

    /// <summary>
    /// 同步失败重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

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
