using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 同步配置仓储接口
/// </summary>
public interface ISyncConfigurationRepository : IRepository<SyncConfiguration>
{
    /// <summary>
    /// 根据云账号ID获取同步配置
    /// </summary>
    Task<SyncConfiguration?> GetByAccountIdAsync(
        int cloudAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有启用的同步配置
    /// </summary>
    Task<List<SyncConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取需要执行的同步配置（按下次执行时间筛选）
    /// </summary>
    Task<List<SyncConfiguration>> GetConfigurationsDueForSyncAsync(
        CancellationToken cancellationToken = default);
}
