using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 云账号数据仓储
/// </summary>
public class CloudAccountRepository : Repository<CloudAccount>, ICloudAccountRepository
{
    public CloudAccountRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据厂商代码获取账号列表
    /// </summary>
    public async Task<List<CloudAccount>> GetByProviderCodeAsync(
        string providerCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.ProviderCode == providerCode)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有启用的账号
    /// </summary>
    public async Task<List<CloudAccount>> GetEnabledAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.IsEnabled)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据ID获取账号（包含关联数据）
    /// </summary>
    public async Task<CloudAccount?> GetByIdWithDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <summary>
    /// 重写GetAllAsync，添加默认排序
    /// </summary>
    public override async Task<List<CloudAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
