using FeeQuery.Shared.Models;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 云账号仓储接口 - 扩展业务查询方法
/// </summary>
public interface ICloudAccountRepository : IRepository<CloudAccount>
{
    /// <summary>
    /// 根据厂商代码获取账号列表
    /// </summary>
    Task<List<CloudAccount>> GetByProviderCodeAsync(
        string providerCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有启用的账号
    /// </summary>
    Task<List<CloudAccount>> GetEnabledAccountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ID获取账号（包含关联数据）
    /// </summary>
    Task<CloudAccount?> GetByIdWithDetailsAsync(
        int id,
        CancellationToken cancellationToken = default);
}
