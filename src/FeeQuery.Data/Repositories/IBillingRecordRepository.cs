using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 账单记录仓储接口
/// </summary>
public interface IBillingRecordRepository : IRepository<BillingRecord>
{
    /// <summary>
    /// 获取指定账号在时间范围内的账单记录
    /// </summary>
    Task<List<BillingRecord>> GetRecordsByAccountAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定账号的账单统计（按日期聚合）
    /// </summary>
    Task<Dictionary<DateTime, decimal>> GetDailySummaryAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
