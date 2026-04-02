using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using HuaweiCloud.SDK.Bss.V2;
using HuaweiCloud.SDK.Bss.V2.Model;
using HuaweiCloud.SDK.Core;
using HuaweiCloud.SDK.Core.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Providers.Huawei;

/// <summary>
/// 华为云厂商适配器
/// 基于华为云官方.NET SDK实现
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class HuaweiCloudProvider : ICloudProvider
{
    private readonly ILogger<HuaweiCloudProvider>? _logger;

    public HuaweiCloudProvider(ILogger<HuaweiCloudProvider>? logger = null)
    {
        _logger = logger;
    }

    public string ProviderCode => "huawei";

    public string ProviderName => "华为云";

    public string Description => "华为云（Huawei Cloud）费用查询适配器，使用官方.NET SDK";

    /// <summary>
    /// 验证凭证
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateBssClient(credentials);

            // 尝试查询账户余额来验证凭证
            var request = new ShowCustomerAccountBalancesRequest();
            _logger?.LogInformation("正在验证华为云凭证...");
            var response = await Task.Run(() => client.ShowCustomerAccountBalances(request), cancellationToken);

            _logger?.LogInformation("华为云凭证验证成功，HttpStatusCode={StatusCode}", response?.HttpStatusCode);
            return response?.HttpStatusCode == 200;
        }
        catch (HuaweiCloud.SDK.Core.ClientRequestException ex)
        {
            _logger?.LogError(ex, "华为云凭证验证失败 - ClientRequestException: " +
                "HttpStatusCode={StatusCode}, ErrorCode={ErrorCode}, ErrorMsg={ErrorMsg}",
                ex.HttpStatusCode, ex.ErrorCode, ex.ErrorMsg);
            return false;
        }
        catch (HuaweiCloud.SDK.Core.ServerResponseException ex)
        {
            _logger?.LogError(ex, "华为云凭证验证失败 - ServerResponseException: " +
                "HttpStatusCode={StatusCode}, ErrorCode={ErrorCode}, ErrorMsg={ErrorMsg}",
                ex.HttpStatusCode, ex.ErrorCode, ex.ErrorMsg);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "华为云凭证验证失败: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 获取账户余额
    /// </summary>
    public async Task<AccountBalance> GetAccountBalanceAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateBssClient(credentials);
            var request = new ShowCustomerAccountBalancesRequest();

            _logger?.LogInformation("正在查询华为云账户余额...");
            var response = await Task.Run(() => client.ShowCustomerAccountBalances(request), cancellationToken);

            if (response == null)
            {
                _logger?.LogWarning("华为云余额查询响应为空");
                throw new InvalidOperationException("API响应为空");
            }

            _logger?.LogInformation("华为云余额查询响应: HttpStatusCode={StatusCode}", response.HttpStatusCode);

            // 解析余额响应
            if (response.AccountBalances != null && response.AccountBalances.Count > 0)
            {
                _logger?.LogInformation("华为云返回了 {Count} 个账户余额记录", response.AccountBalances.Count);

                // 记录所有账户余额（可能有多个：现金账户、代金券账户等）
                decimal totalAvailableBalance = 0;
                decimal totalCreditLimit = 0;

                for (int i = 0; i < response.AccountBalances.Count; i++)
                {
                    var accountBalance = response.AccountBalances[i];
                    var amount = ConvertToDecimal(accountBalance.Amount?.ToString());
                    var credit = ConvertToDecimal(accountBalance.CreditAmount?.ToString());

                    _logger?.LogInformation("华为云余额账户 [{Index}]: " +
                        "Amount={Amount}, " +
                        "CreditAmount={CreditAmount}, " +
                        "Currency={Currency}, " +
                        "AccountType={AccountType}",
                        i,
                        accountBalance.Amount,
                        accountBalance.CreditAmount,
                        accountBalance.Currency,
                        accountBalance.AccountType);

                    totalAvailableBalance += amount;
                    totalCreditLimit += credit;
                }

                _logger?.LogInformation("华为云余额汇总: 总可用余额={Total}, 总信用额度={Credit}",
                    totalAvailableBalance, totalCreditLimit);

                return new AccountBalance
                {
                    AvailableBalance = totalAvailableBalance,
                    CreditLimit = totalCreditLimit,  // 即使为0也显示
                    Currency = response.AccountBalances[0].Currency ?? "CNY",
                    QueryTime = DateTime.UtcNow
                };
            }
            else
            {
                _logger?.LogWarning("华为云余额查询响应中没有账户余额数据");
                return new AccountBalance
                {
                    AvailableBalance = 0,
                    Currency = "CNY",
                    QueryTime = DateTime.UtcNow
                };
            }
        }
        catch (HuaweiCloud.SDK.Core.ClientRequestException ex)
        {
            // 捕获华为云SDK的客户端请求异常
            _logger?.LogError(ex, "华为云余额查询失败 - ClientRequestException: " +
                "HttpStatusCode={StatusCode}, ErrorCode={ErrorCode}, ErrorMsg={ErrorMsg}, RequestId={RequestId}",
                ex.HttpStatusCode, ex.ErrorCode, ex.ErrorMsg, ex.RequestId);
            throw new InvalidOperationException(
                $"华为云余额查询失败: {ex.ErrorMsg ?? ex.Message} (错误码: {ex.ErrorCode}, HTTP状态: {ex.HttpStatusCode})",
                ex);
        }
        catch (HuaweiCloud.SDK.Core.ServerResponseException ex)
        {
            // 捕获华为云SDK的服务器响应异常
            _logger?.LogError(ex, "华为云余额查询失败 - ServerResponseException: " +
                "HttpStatusCode={StatusCode}, ErrorCode={ErrorCode}, ErrorMsg={ErrorMsg}, RequestId={RequestId}",
                ex.HttpStatusCode, ex.ErrorCode, ex.ErrorMsg, ex.RequestId);
            throw new InvalidOperationException(
                $"华为云服务器错误: {ex.ErrorMsg ?? ex.Message} (错误码: {ex.ErrorCode}, HTTP状态: {ex.HttpStatusCode})",
                ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "华为云余额查询失败 - 未知错误: {Message}", ex.Message);
            throw new InvalidOperationException($"华为云余额查询失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取所需凭证字段
    /// </summary>
    public List<CredentialField> GetRequiredCredentialFields()
    {
        return new List<CredentialField>
        {
            new CredentialField
            {
                Key = "AccessKeyId",
                DisplayName = "访问密钥ID",
                Description = "华为云AK（Access Key ID），用于API认证",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入Access Key ID"
            },
            new CredentialField
            {
                Key = "SecretAccessKey",
                DisplayName = "访问密钥密文",
                Description = "华为云SK（Secret Access Key），用于API认证",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入Secret Access Key"
            },
            new CredentialField
            {
                Key = "DomainId",
                DisplayName = "账号ID (Domain ID)",
                Description = "华为云账号ID，在华为云控制台「我的凭证」页面可以查看",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入账号ID"
            },
            new CredentialField
            {
                Key = "Region",
                DisplayName = "区域",
                Description = "华为云区域，如 cn-north-4",
                Required = false,
                IsSensitive = false,
                Placeholder = "cn-north-4"
            }
        };
    }

    /// <summary>
    /// 创建BSS客户端
    /// </summary>
    private BssClient CreateBssClient(CloudCredentials credentials)
    {
        var ak = credentials.GetCredential("AccessKeyId")
            ?? throw new InvalidOperationException("缺少AccessKeyId");
        var sk = credentials.GetCredential("SecretAccessKey")
            ?? throw new InvalidOperationException("缺少SecretAccessKey");
        var domainId = credentials.GetCredential("DomainId")
            ?? throw new InvalidOperationException("缺少DomainId（账号ID）");
        var region = credentials.GetCredential("Region") ?? "cn-north-4";

        // 华为云BSS服务是全局服务，需要使用GlobalCredentials
        // 必须提供domainId，否则SDK会尝试自动获取（需要iam:users:getUser权限）
        var auth = new GlobalCredentials(ak, sk, domainId);
        var config = HttpConfig.GetDefaultConfig();
        config.IgnoreSslVerification = false;

        return BssClient.NewBuilder()
            .WithCredential(auth)
            .WithRegion(BssRegion.ValueOf(region))
            .WithHttpConfig(config)
            .Build();
    }

    /// <summary>
    /// 映射服务类别
    /// </summary>
    private string MapServiceCategory(string? serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return "其他";
        }

        var name = serviceName.ToLower();

        if (name.Contains("ecs") || name.Contains("compute") || name.Contains("云服务器"))
            return "计算";
        if (name.Contains("obs") || name.Contains("storage") || name.Contains("存储"))
            return "存储";
        if (name.Contains("vpc") || name.Contains("eip") || name.Contains("网络") || name.Contains("bandwidth"))
            return "网络";
        if (name.Contains("rds") || name.Contains("dds") || name.Contains("database") || name.Contains("数据库"))
            return "数据库";
        if (name.Contains("cdn"))
            return "CDN";
        if (name.Contains("modelarts") || name.Contains("ai"))
            return "AI服务";

        return "其他";
    }

    /// <summary>
    /// 转换为Decimal
    /// </summary>
    private decimal ConvertToDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        return decimal.TryParse(value, out var result) ? result : 0;
    }
}
