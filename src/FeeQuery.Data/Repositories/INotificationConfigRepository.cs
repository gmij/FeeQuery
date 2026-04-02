using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 通知配置仓储接口
/// </summary>
public interface INotificationConfigRepository : IRepository<NotificationConfig>
{
    /// <summary>
    /// 根据类型获取通知配置
    /// </summary>
    Task<List<NotificationConfig>> GetByTypeAsync(
        string providerType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取默认通知配置
    /// </summary>
    Task<NotificationConfig?> GetDefaultConfigByTypeAsync(
        string providerType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有启用的通知配置
    /// </summary>
    Task<List<NotificationConfig>> GetEnabledConfigsAsync(
        CancellationToken cancellationToken = default);
}
