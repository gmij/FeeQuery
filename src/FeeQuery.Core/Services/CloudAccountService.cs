using FeeQuery.Core.Factories;
using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services;

/// <summary>
/// 云账号管理服务
/// </summary>
public class CloudAccountService
{
    private readonly CloudAccountRepository _repository;
    private readonly CredentialEncryptionService _encryptionService;
    private readonly ICloudProviderFactory _providerFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CloudAccountService> _logger;

    public CloudAccountService(
        CloudAccountRepository repository,
        CredentialEncryptionService encryptionService,
        ICloudProviderFactory providerFactory,
        IUnitOfWork unitOfWork,
        ILogger<CloudAccountService> logger)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _providerFactory = providerFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有账号
    /// </summary>
    public async Task<List<CloudAccount>> GetAllAccountsAsync()
    {
        return await _repository.GetAllAsync();
    }

    /// <summary>
    /// 根据ID获取账号
    /// </summary>
    public async Task<CloudAccount?> GetAccountByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    /// <summary>
    /// 添加账号
    /// </summary>
    public async Task<CloudAccount> AddAccountAsync(CloudAccount account, Dictionary<string, string> credentials)
    {
        // 加密凭证
        account.EncryptedCredentials = _encryptionService.Encrypt(credentials);

        // 保存账号，获取真实 ID
        var savedAccount = await _repository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("成功添加云账号: {AccountId} - {AccountName} ({ProviderName})",
            savedAccount.Id, savedAccount.Name, savedAccount.ProviderName);

        // 在同一事务中为新账号创建同步配置
        var existingConfig = await _unitOfWork.SyncConfigurations
            .FirstOrDefaultAsync(c => c.CloudAccountId == savedAccount.Id);

        if (existingConfig == null)
        {
            var globalConfig = await _unitOfWork.SyncConfigurations
                .FirstOrDefaultAsync(c => c.CloudAccountId == null);

            var config = new SyncConfiguration
            {
                CloudAccountId = savedAccount.Id,
                IsEnabled = true,
                SyncBalance = true,
                SyncBilling = globalConfig?.SyncBilling ?? false,
                IntervalMinutes = globalConfig?.IntervalMinutes ?? 60,
                LastSyncAt = null,
                NextSyncAt = DateTime.UtcNow
            };

            await _unitOfWork.SyncConfigurations.AddAsync(config);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("已为账号 {AccountId} 创建同步配置，间隔: {Interval} 分钟",
                savedAccount.Id, config.IntervalMinutes);
        }

        return savedAccount;
    }

    /// <summary>
    /// 更新账号
    /// </summary>
    public async Task UpdateAccountAsync(CloudAccount account, Dictionary<string, string>? credentials = null)
    {
        // 如果提供了新凭证，则更新
        if (credentials != null)
        {
            account.EncryptedCredentials = _encryptionService.Encrypt(credentials);
        }

        _repository.Update(account);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// 删除账号
    /// </summary>
    public async Task DeleteAccountAsync(int id)
    {
        var account = await _repository.GetByIdAsync(id);
        if (account != null)
        {
            _repository.Remove(account);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 测试账号连接
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(string providerCode, Dictionary<string, string> credentials)
    {
        try
        {
            var provider = _providerFactory.GetProvider(providerCode);
            if (provider == null)
            {
                return (false, $"不支持的云厂商: {providerCode}");
            }

            var cloudCredentials = new CloudCredentials();
            foreach (var kvp in credentials)
            {
                cloudCredentials.SetCredential(kvp.Key, kvp.Value);
            }

            var isValid = await provider.ValidateCredentialsAsync(cloudCredentials);

            if (isValid)
            {
                return (true, $"✓ 连接成功！{provider.ProviderName} 凭证验证通过");
            }
            else
            {
                var errorMessage = $"✗ 连接失败！{provider.ProviderName} 凭证验证未通过，请检查配置";

                // 针对不同厂商提供特定的帮助信息
                if (providerCode == "volcano")
                {
                    errorMessage += "\n提示：请确保密钥对拥有 BillingCenterBillReadOnlyAccess 权限策略";
                }

                return (false, errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接失败: {ProviderCode}", providerCode);
            return (false, $"✗ 连接失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取解密后的凭证
    /// </summary>
    public Dictionary<string, string> GetDecryptedCredentials(CloudAccount account)
    {
        return _encryptionService.Decrypt(account.EncryptedCredentials);
    }

    /// <summary>
    /// 获取所有可用的云厂商
    /// </summary>
    public List<ICloudProvider> GetAvailableProviders()
    {
        return _providerFactory.GetAllProviders().ToList();
    }

    /// <summary>
    /// 获取启用的账号
    /// </summary>
    public async Task<List<CloudAccount>> GetEnabledAccountsAsync()
    {
        return await _repository.GetEnabledAccountsAsync();
    }
}
