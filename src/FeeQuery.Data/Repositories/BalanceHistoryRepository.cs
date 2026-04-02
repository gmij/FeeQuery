using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 余额历史仓储实现
/// </summary>
public class BalanceHistoryRepository : Repository<BalanceHistory>, IBalanceHistoryRepository
{
    public BalanceHistoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<BalanceHistory?> GetLatestByAccountAsync(
        int cloudAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.CloudAccountId == cloudAccountId)
            .OrderByDescending(h => h.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<BalanceHistory>> GetHistoryByAccountAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.CloudAccountId == cloudAccountId &&
                       h.RecordedAt >= startDate &&
                       h.RecordedAt <= endDate)
            .OrderByDescending(h => h.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BalanceHistory>> GetLatestBalancesAsync(
        CancellationToken cancellationToken = default)
    {
        // 获取每个账号的最新余额记录
        return await _dbSet
            .GroupBy(h => h.CloudAccountId)
            .Select(g => g.OrderByDescending(h => h.RecordedAt).First())
            .ToListAsync(cancellationToken);
    }
}
