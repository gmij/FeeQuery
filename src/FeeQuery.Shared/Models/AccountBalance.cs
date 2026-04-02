namespace FeeQuery.Shared.Models;

/// <summary>
/// 账户余额信息
/// </summary>
public class AccountBalance
{
    /// <summary>
    /// 可用余额（现金+代金券余额，不包含信用额度）
    /// 当值为负数时，表示已透支信用额度，负数值即为透支金额
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// 信用额度总量（可透支的最大额度）
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// 是否已透支（当AvailableBalance为负数时为true）
    /// </summary>
    public bool IsOverdrawn => AvailableBalance < 0;

    /// <summary>
    /// 已透支金额（当IsOverdrawn为true时，返回透支金额的绝对值）
    /// </summary>
    public decimal OverdrawnAmount => IsOverdrawn ? Math.Abs(AvailableBalance) : 0;

    /// <summary>
    /// 实际可用总额度（现金余额 + 剩余信用额度）
    /// </summary>
    public decimal TotalAvailable
    {
        get
        {
            if (IsOverdrawn && CreditLimit.HasValue)
            {
                // 已透支情况：剩余可用 = 信用额度 - 已用信用额度
                return CreditLimit.Value - OverdrawnAmount;
            }
            else if (CreditLimit.HasValue)
            {
                // 未透支情况：总可用 = 现金余额 + 信用额度
                return AvailableBalance + CreditLimit.Value;
            }
            else
            {
                // 无信用额度：总可用 = 现金余额
                return AvailableBalance;
            }
        }
    }

    /// <summary>
    /// 货币单位
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 查询时间
    /// </summary>
    public DateTime QueryTime { get; set; } = DateTime.UtcNow;
}
