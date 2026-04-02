using Microsoft.EntityFrameworkCore.Storage;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 工作单元接口 - 统一管理事务和Repository生命周期
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // ========== Repository属性（懒加载） ==========

    /// <summary>
    /// 云账号仓储
    /// </summary>
    ICloudAccountRepository CloudAccounts { get; }

    /// <summary>
    /// 预警规则仓储
    /// </summary>
    IAlertRuleRepository AlertRules { get; }

    /// <summary>
    /// 预警历史仓储
    /// </summary>
    IAlertHistoryRepository AlertHistories { get; }

    /// <summary>
    /// 余额历史仓储
    /// </summary>
    IBalanceHistoryRepository BalanceHistories { get; }

    /// <summary>
    /// 账单记录仓储
    /// </summary>
    IBillingRecordRepository BillingRecords { get; }

    /// <summary>
    /// 同步配置仓储
    /// </summary>
    ISyncConfigurationRepository SyncConfigurations { get; }

    /// <summary>
    /// 通知配置仓储
    /// </summary>
    INotificationConfigRepository NotificationConfigs { get; }

    // ========== 事务管理 ==========

    /// <summary>
    /// 保存所有更改
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始事务
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交事务
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚事务
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
