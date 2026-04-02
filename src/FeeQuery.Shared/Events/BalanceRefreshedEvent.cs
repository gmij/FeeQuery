namespace FeeQuery.Shared.Events;

/// <summary>
/// 余额刷新完成事件
/// </summary>
public class BalanceRefreshedEvent
{
    /// <summary>
    /// 云账号ID
    /// </summary>
    public int CloudAccountId { get; set; }

    /// <summary>
    /// 可用余额（现金余额）
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// 信用额度
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// 总可用额度（现金+信用）
    /// </summary>
    public decimal TotalAvailableAmount => AvailableBalance + (CreditLimit ?? 0);

    /// <summary>
    /// 货币
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 刷新来源（manual, scheduled, initial）
    /// </summary>
    public string Source { get; set; } = "manual";

    /// <summary>
    /// 刷新时间
    /// </summary>
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
