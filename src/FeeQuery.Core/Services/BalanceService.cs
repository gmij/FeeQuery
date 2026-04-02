using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Events;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services;

/// <summary>
/// 余额管理服务
/// </summary>
public class BalanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudProviderFactory _providerFactory;
    private readonly CredentialEncryptionService _encryptionService;
    private readonly ILogger<BalanceService> _logger;
    private readonly IEventBus? _eventBus;

    public BalanceService(
        IUnitOfWork unitOfWork,
        ICloudProviderFactory providerFactory,
        CredentialEncryptionService encryptionService,
        ILogger<BalanceService> logger,
        IEventBus? eventBus = null)
    {
        _unitOfWork = unitOfWork;
        _providerFactory = providerFactory;
        _encryptionService = encryptionService;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// 获取指定账号的当前余额
    /// </summary>
    public async Task<AccountBalance?> GetCurrentBalanceAsync(int cloudAccountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _unitOfWork.CloudAccounts
                .FirstOrDefaultAsync(a => a.Id == cloudAccountId && a.IsEnabled, cancellationToken);

            if (account == null)
            {
                _logger.LogWarning("账号 {AccountId} 不存在或已禁用", cloudAccountId);
                return null;
            }

            var provider = _providerFactory.GetProvider(account.ProviderCode);
            if (provider == null)
            {
                _logger.LogError("找不到云厂商适配器: {ProviderCode}", account.ProviderCode);
                return null;
            }

            if (string.IsNullOrEmpty(account.EncryptedCredentials))
            {
                _logger.LogError("账号 {AccountId} 的加密凭证为空", cloudAccountId);
                return null;
            }

            var credentialsDict = _encryptionService.Decrypt(account.EncryptedCredentials);
            var credentials = new CloudCredentials { Credentials = credentialsDict };
            var balance = await provider.GetAccountBalanceAsync(credentials, cancellationToken);

            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取账号 {AccountId} 余额失败", cloudAccountId);
            throw;
        }
    }

    /// <summary>
    /// 获取所有启用账号的当前余额
    /// </summary>
    public async Task<Dictionary<int, AccountBalance>> GetAllCurrentBalancesAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _unitOfWork.CloudAccounts
            .GetEnabledAccountsAsync(cancellationToken);

        var balances = new Dictionary<int, AccountBalance>();

        foreach (var account in accounts)
        {
            try
            {
                var balance = await GetCurrentBalanceAsync(account.Id, cancellationToken);
                if (balance != null)
                {
                    balances[account.Id] = balance;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账号 {AccountId} ({AccountName}) 余额失败", account.Id, account.Name);
            }
        }

        return balances;
    }

    /// <summary>
    /// 刷新并保存账号余额
    /// </summary>
    public async Task<BalanceHistory?> RefreshAndSaveBalanceAsync(int cloudAccountId, string source = "manual", CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var balance = await GetCurrentBalanceAsync(cloudAccountId, cancellationToken);
            stopwatch.Stop();

            if (balance == null)
            {
                // 创建失败记录
                var failedHistory = new BalanceHistory
                {
                    CloudAccountId = cloudAccountId,
                    AvailableBalance = 0,
                    Currency = "CNY",
                    RecordedAt = startTime,
                    Source = source,
                    Status = "Failed",
                    ErrorMessage = "无法获取账号余额，账号可能不存在或已禁用",
                    SyncDurationMs = (int)stopwatch.ElapsedMilliseconds
                };

                await _unitOfWork.BalanceHistories.AddAsync(failedHistory, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return failedHistory;
            }

            var history = new BalanceHistory
            {
                CloudAccountId = cloudAccountId,
                AvailableBalance = balance.AvailableBalance,
                CreditLimit = balance.CreditLimit,
                Currency = balance.Currency,
                RecordedAt = startTime,
                Source = source,
                Status = "Success",
                SyncDurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            await _unitOfWork.BalanceHistories.AddAsync(history, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 更新账号最后同步时间
            var account = await _unitOfWork.CloudAccounts.GetByIdAsync(cloudAccountId, cancellationToken);
            if (account != null)
            {
                account.LastSyncAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("账号 {AccountId} 余额刷新成功: {Balance} {Currency} (耗时: {Duration}ms)",
                cloudAccountId, balance.AvailableBalance, balance.Currency, stopwatch.ElapsedMilliseconds);

            // 发布余额刷新事件
            if (_eventBus != null)
            {
                try
                {
                    var balanceEvent = new BalanceRefreshedEvent
                    {
                        CloudAccountId = cloudAccountId,
                        AvailableBalance = balance.AvailableBalance,
                        CreditLimit = balance.CreditLimit,
                        Currency = balance.Currency,
                        Source = source,
                        RefreshedAt = startTime,
                        IsSuccess = true
                    };

                    await _eventBus.PublishAsync(balanceEvent, cancellationToken);
                    _logger.LogDebug("已发布余额刷新事件: AccountId={AccountId}", cloudAccountId);
                }
                catch (Exception eventEx)
                {
                    // 事件发布失败不影响余额刷新
                    _logger.LogError(eventEx, "发布余额刷新事件失败: AccountId={AccountId}", cloudAccountId);
                }
            }

            return history;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 获取账号名称用于通知
            var account = await _unitOfWork.CloudAccounts.GetByIdAsync(cloudAccountId, cancellationToken);
            var accountName = account?.Name ?? $"账号ID:{cloudAccountId}";
            var providerName = account?.ProviderName ?? "未知";

            // 记录失败的同步历史
            var failedHistory = new BalanceHistory
            {
                CloudAccountId = cloudAccountId,
                AvailableBalance = 0,
                Currency = "CNY",
                RecordedAt = startTime,
                Source = source,
                Status = "Failed",
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}",
                SyncDurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            try
            {
                await _unitOfWork.BalanceHistories.AddAsync(failedHistory, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "保存失败记录时发生错误");
            }

            _logger.LogError(ex, "刷新账号 {AccountId} 余额失败 (耗时: {Duration}ms)", cloudAccountId, stopwatch.ElapsedMilliseconds);

            // 发布余额刷新失败事件
            if (_eventBus != null)
            {
                try
                {
                    var balanceEvent = new BalanceRefreshedEvent
                    {
                        CloudAccountId = cloudAccountId,
                        AvailableBalance = 0,
                        CreditLimit = null,
                        Currency = "CNY",
                        Source = source,
                        RefreshedAt = startTime,
                        IsSuccess = false,
                        ErrorMessage = failedHistory.ErrorMessage
                    };

                    await _eventBus.PublishAsync(balanceEvent, cancellationToken);
                    _logger.LogDebug("已发布余额刷新失败事件: AccountId={AccountId}", cloudAccountId);
                }
                catch (Exception eventEx)
                {
                    // 事件发布失败不影响其他流程
                    _logger.LogError(eventEx, "发布余额刷新失败事件失败: AccountId={AccountId}", cloudAccountId);
                }
            }

            return failedHistory;
        }
    }

    /// <summary>
    /// 获取账号的余额历史记录
    /// </summary>
    public async Task<List<BalanceHistory>> GetBalanceHistoryAsync(
        int cloudAccountId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // 如果指定了时间范围，使用Repository方法
        if (startDate.HasValue && endDate.HasValue)
        {
            var results = await _unitOfWork.BalanceHistories
                .GetHistoryByAccountAsync(cloudAccountId, startDate.Value, endDate.Value, cancellationToken);
            return results.Take(limit).ToList();
        }

        // 否则使用FindAsync获取所有记录后筛选
        var allHistories = await _unitOfWork.BalanceHistories.FindAsync(
            h => h.CloudAccountId == cloudAccountId, cancellationToken);

        var query = allHistories.AsEnumerable();

        if (startDate.HasValue)
        {
            query = query.Where(h => h.RecordedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(h => h.RecordedAt <= endDate.Value);
        }

        return query.OrderByDescending(h => h.RecordedAt).Take(limit).ToList();
    }

    /// <summary>
    /// 获取最新的余额记录
    /// </summary>
    public async Task<BalanceHistory?> GetLatestBalanceHistoryAsync(int cloudAccountId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.BalanceHistories
            .GetLatestByAccountAsync(cloudAccountId, cancellationToken);
    }

    /// <summary>
    /// 获取所有账号的最新余额历史记录
    /// </summary>
    public async Task<Dictionary<int, BalanceHistory>> GetAllLatestBalanceHistoriesAsync(CancellationToken cancellationToken = default)
    {
        var enabledAccounts = await _unitOfWork.CloudAccounts
            .GetEnabledAccountsAsync(cancellationToken);
        var accounts = enabledAccounts.Select(a => a.Id).ToList();

        var latestHistories = new Dictionary<int, BalanceHistory>();

        foreach (var accountId in accounts)
        {
            var latest = await GetLatestBalanceHistoryAsync(accountId, cancellationToken);
            if (latest != null)
            {
                latestHistories[accountId] = latest;
            }
        }

        return latestHistories;
    }

    /// <summary>
    /// 删除过期的余额历史记录
    /// </summary>
    public async Task<int> DeleteOldBalanceHistoriesAsync(int retentionDays = 90, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var oldRecords = await _unitOfWork.BalanceHistories
            .FindAsync(h => h.RecordedAt < cutoffDate, cancellationToken);

        _unitOfWork.BalanceHistories.RemoveRange(oldRecords);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("删除了 {Count} 条过期的余额历史记录", oldRecords.Count);
        return oldRecords.Count;
    }
}
