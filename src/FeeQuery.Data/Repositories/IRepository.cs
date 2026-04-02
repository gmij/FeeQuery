using System.Linq.Expressions;

namespace FeeQuery.Data.Repositories;

/// <summary>
/// 基础仓储接口 - 提供通用CRUD操作
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    // ========== 查询操作 ==========

    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    Task<TEntity?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查找实体
    /// </summary>
    Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取第一个实体（不存在返回null）
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="pageIndex">页码（从0开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="predicate">筛选条件（可选）</param>
    /// <param name="orderBy">排序（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据和总数</returns>
    Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken cancellationToken = default);

    // ========== 添加操作 ==========

    /// <summary>
    /// 添加实体（不调用SaveChanges，由UnitOfWork统一管理）
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加实体（不调用SaveChanges）
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    // ========== 更新操作 ==========

    /// <summary>
    /// 更新实体（不调用SaveChanges）
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// 批量更新实体（不调用SaveChanges）
    /// </summary>
    void UpdateRange(IEnumerable<TEntity> entities);

    // ========== 删除操作 ==========

    /// <summary>
    /// 删除实体（不调用SaveChanges）
    /// </summary>
    void Remove(TEntity entity);

    /// <summary>
    /// 批量删除实体（不调用SaveChanges）
    /// </summary>
    void RemoveRange(IEnumerable<TEntity> entities);

    // ========== 统计操作 ==========

    /// <summary>
    /// 统计数量
    /// </summary>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断是否存在
    /// </summary>
    Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
}
