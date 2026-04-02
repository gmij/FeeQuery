using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 预警历史仓储实现
/// </summary>
public class AlertHistoryRepository : Repository<AlertHistory>, IAlertHistoryRepository
{
    public AlertHistoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<AlertHistory>> GetUnresolvedAlertsAsync(
        int? cloudAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(h => h.AlertRule)
            .Include(h => h.CloudAccount)
            .Where(h => h.Status != "resolved");

        if (cloudAccountId.HasValue)
        {
            query = query.Where(h => h.CloudAccountId == cloudAccountId.Value);
        }

        return await query
            .OrderByDescending(h => h.TriggeredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AlertHistory?> GetActiveAlertByRuleAsync(
        int ruleId,
        int cloudAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(h => h.AlertRule)
            .Include(h => h.CloudAccount)
            .Where(h => h.AlertRuleId == ruleId &&
                       h.CloudAccountId == cloudAccountId &&
                       h.Status != "resolved")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AlertHistory?> GetByIdWithDetailsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(h => h.AlertRule)
            .Include(h => h.CloudAccount)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<List<AlertHistory>> GetAlertsNeedingRepeatNotificationAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbSet
            .Include(h => h.AlertRule)
            .Include(h => h.CloudAccount)
            .Where(h => h.Status != "resolved" &&
                       // acknowledged 状态：按规则配置的静默时长决定是否重新通知
                       // AcknowledgeSnoozeDuration = 0 表示永久静默，直到余额恢复或手动解决
                       !(h.Status == "acknowledged" &&
                         h.AlertRule != null &&
                         (h.AlertRule.AcknowledgeSnoozeDuration == 0 ||
                          (h.AcknowledgedAt.HasValue &&
                           h.AcknowledgedAt.Value.AddMinutes(h.AlertRule.AcknowledgeSnoozeDuration) > now))) &&
                       h.AlertRule != null &&
                       h.AlertRule.IsEnabled &&
                       h.AlertRule.RepeatNotificationInterval > 0 &&
                       h.LastNotificationAt.HasValue &&
                       h.LastNotificationAt.Value.AddMinutes(h.AlertRule.RepeatNotificationInterval) <= now)
            .ToListAsync(cancellationToken);
    }
}
