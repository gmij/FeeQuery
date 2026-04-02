using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 账单记录仓储实现
/// </summary>
public class BillingRecordRepository : Repository<BillingRecord>, IBillingRecordRepository
{
    public BillingRecordRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<BillingRecord>> GetRecordsByAccountAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.CloudAccountId == cloudAccountId &&
                       r.BillingDate >= startDate &&
                       r.BillingDate <= endDate)
            .OrderByDescending(r => r.BillingDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<DateTime, decimal>> GetDailySummaryAsync(
        int cloudAccountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var records = await _dbSet
            .Where(r => r.CloudAccountId == cloudAccountId &&
                       r.BillingDate >= startDate &&
                       r.BillingDate <= endDate)
            .GroupBy(r => r.BillingDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalAmount = g.Sum(r => r.Amount)
            })
            .ToListAsync(cancellationToken);

        return records.ToDictionary(r => r.Date, r => r.TotalAmount);
    }
}
