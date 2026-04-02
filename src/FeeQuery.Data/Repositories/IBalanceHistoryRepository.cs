using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 余额历史仓储接口
/// </summary>
public interface IBalanceHistoryRepository : IRepository<BalanceHistory>
{
    /// <summary>
    /// 获取指定账号的最新余额记录
    /// </summary>
    Task<BalanceHistory?> GetLatestByAccountAsync(
        int cloudAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定账号在时间范围内的余额历史
    /// </summary>
    Task<List<BalanceHistory>> GetHistoryByAccountAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有账号的最新余额记录
    /// </summary>
    Task<List<BalanceHistory>> GetLatestBalancesAsync(
        CancellationToken cancellationToken = default);
}
