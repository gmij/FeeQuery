using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 工作单元实现 - 延迟初始化Repository，统一事务管理
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;

    // 懒加载Repository字段（避免不必要的实例化）
    private ICloudAccountRepository? _cloudAccounts;
    private IAlertRuleRepository? _alertRules;
    private IAlertHistoryRepository? _alertHistories;
    private IBalanceHistoryRepository? _balanceHistories;
    private IBillingRecordRepository? _billingRecords;
    private ISyncConfigurationRepository? _syncConfigurations;
    private INotificationConfigRepository? _notificationConfigs;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    // ========== Repository属性（懒加载实现） ==========

    public ICloudAccountRepository CloudAccounts =>
        _cloudAccounts ??= new CloudAccountRepository(_context);

    public IAlertRuleRepository AlertRules =>
        _alertRules ??= new AlertRuleRepository(_context);

    public IAlertHistoryRepository AlertHistories =>
        _alertHistories ??= new AlertHistoryRepository(_context);

    public IBalanceHistoryRepository BalanceHistories =>
        _balanceHistories ??= new BalanceHistoryRepository(_context);

    public IBillingRecordRepository BillingRecords =>
        _billingRecords ??= new BillingRecordRepository(_context);

    public ISyncConfigurationRepository SyncConfigurations =>
        _syncConfigurations ??= new SyncConfigurationRepository(_context);

    public INotificationConfigRepository NotificationConfigs =>
        _notificationConfigs ??= new NotificationConfigRepository(_context);

    // ========== 事务管理 ==========

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 自动更新UpdatedAt字段（如果实体有此属性）
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            // 使用反射检查是否有UpdatedAt属性
            var updatedAtProperty = entry.Entity.GetType().GetProperty("UpdatedAt");
            if (updatedAtProperty != null && entry.State == EntityState.Modified)
            {
                updatedAtProperty.SetValue(entry.Entity, DateTime.UtcNow);
            }

            // 新增时自动设置CreatedAt和UpdatedAt
            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entry.Entity.GetType().GetProperty("CreatedAt");
                createdAtProperty?.SetValue(entry.Entity, DateTime.UtcNow);
                updatedAtProperty?.SetValue(entry.Entity, DateTime.UtcNow);
            }
        }

        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _transaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("事务未开始，无法提交");
        }

        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        // 注意：不要Dispose _context，因为它是由DI容器管理的
    }
}
