using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 同步配置仓储实现
/// </summary>
public class SyncConfigurationRepository : Repository<SyncConfiguration>, ISyncConfigurationRepository
{
    public SyncConfigurationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SyncConfiguration?> GetByAccountIdAsync(
        int cloudAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.CloudAccountId == cloudAccountId, cancellationToken);
    }

    public async Task<List<SyncConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.CloudAccount)
            .Where(c => c.IsEnabled && c.CloudAccount != null && c.CloudAccount.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SyncConfiguration>> GetConfigurationsDueForSyncAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbSet
            .Include(c => c.CloudAccount)
            .Where(c => c.IsEnabled &&
                       c.CloudAccount != null &&
                       c.CloudAccount.IsEnabled &&
                       c.NextSyncAt.HasValue &&
                       c.NextSyncAt.Value <= now)
            .ToListAsync(cancellationToken);
    }
}
