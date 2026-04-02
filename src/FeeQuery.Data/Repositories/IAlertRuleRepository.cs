using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 预警规则仓储接口
/// </summary>
public interface IAlertRuleRepository : IRepository<AlertRule>
{
    /// <summary>
    /// 获取指定账号的启用预警规则
    /// </summary>
    Task<List<AlertRule>> GetActiveRulesForAccountAsync(
        int cloudAccountId,
        string? alertType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有启用的预警规则
    /// </summary>
    Task<List<AlertRule>> GetEnabledRulesAsync(
        CancellationToken cancellationToken = default);
}
