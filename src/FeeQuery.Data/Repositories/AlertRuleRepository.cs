using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 预警规则仓储实现
/// </summary>
public class AlertRuleRepository : Repository<AlertRule>, IAlertRuleRepository
{
    public AlertRuleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<AlertRule>> GetActiveRulesForAccountAsync(
        int cloudAccountId,
        string? alertType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(r => r.CloudAccount)
            .Where(r => r.CloudAccountId == cloudAccountId && r.IsEnabled);

        if (!string.IsNullOrEmpty(alertType))
        {
            query = query.Where(r => r.AlertType == alertType);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<AlertRule>> GetEnabledRulesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.CloudAccount)
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);
    }
}
