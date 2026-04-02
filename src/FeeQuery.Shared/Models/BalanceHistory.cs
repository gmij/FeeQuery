namespace FeeQuery.Shared.Models;

/// <summary>
/// 余额历史记录
/// 用于跟踪账户余额变化趋势
/// </summary>
public class BalanceHistory
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 云账号ID
    /// </summary>
    public int CloudAccountId { get; set; }

    /// <summary>
    /// 可用余额
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// 信用额度
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// 货币单位
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 记录时间（UTC）
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 同步来源（manual: 手动刷新, scheduled: 定时任务, initial: 初始化）
    /// </summary>
    public string Source { get; set; } = "scheduled";

    /// <summary>
    /// 同步状态（Success: 成功, Failed: 失败）
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// 错误信息（同步失败时记录）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 同步耗时（毫秒）
    /// </summary>
    public int? SyncDurationMs { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 关联的云账号
    /// </summary>
    public CloudAccount? CloudAccount { get; set; }
}
