using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 通知配置仓储实现
/// </summary>
public class NotificationConfigRepository : Repository<NotificationConfig>, INotificationConfigRepository
{
    public NotificationConfigRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<NotificationConfig>> GetByTypeAsync(
        string providerType,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.ChannelType == providerType)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationConfig?> GetDefaultConfigByTypeAsync(
        string providerType,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.ChannelType == providerType && c.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<NotificationConfig>> GetEnabledConfigsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.IsEnabled)
            .ToListAsync(cancellationToken);
    }
}
