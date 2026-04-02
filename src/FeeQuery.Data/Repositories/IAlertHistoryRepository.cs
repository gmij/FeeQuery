using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 预警历史仓储接口
/// </summary>
public interface IAlertHistoryRepository : IRepository<AlertHistory>
{
    /// <summary>
    /// 获取未解决的预警
    /// </summary>
    Task<List<AlertHistory>> GetUnresolvedAlertsAsync(
        int? cloudAccountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定规则和账号的活跃预警
    /// </summary>
    Task<AlertHistory?> GetActiveAlertByRuleAsync(
        int ruleId,
        int cloudAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ID获取预警（包含关联数据）
    /// </summary>
    Task<AlertHistory?> GetByIdWithDetailsAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取需要重复通知的预警
    /// </summary>
    Task<List<AlertHistory>> GetAlertsNeedingRepeatNotificationAsync(
        CancellationToken cancellationToken = default);
}
