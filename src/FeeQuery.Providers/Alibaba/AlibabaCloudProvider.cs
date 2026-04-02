using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AlibabaCloud.SDK.BssOpenApi20171214;
using Tea;

namespace FeeQuery.Providers.Alibaba;

/// <summary>
/// 阿里云厂商适配器
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class AlibabaCloudProvider : ICloudProvider
{
    private readonly ILogger<AlibabaCloudProvider>? _logger;

    public AlibabaCloudProvider(ILogger<AlibabaCloudProvider>? logger = null)
    {
        _logger = logger;
    }

    public string ProviderCode => "alibaba";

    public string ProviderName => "阿里云";

    public string Description => "阿里云（Alibaba Cloud）费用查询适配器";

    /// <summary>
    /// 验证凭证
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("开始验证阿里云凭证");

            var accessKeyId = credentials.GetCredential("AccessKeyId");
            var accessKeySecret = credentials.GetCredential("AccessKeySecret");

            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(accessKeySecret))
            {
                _logger?.LogWarning("凭证不完整：AccessKeyId或AccessKeySecret为空");
                return false;
            }

            // 通过调用查询余额API来验证凭证是否有效
            var config = new AlibabaCloud.OpenApiClient.Models.Config
            {
                AccessKeyId = accessKeyId,
                AccessKeySecret = accessKeySecret,
                RegionId = "cn-hangzhou",
                Endpoint = "business.aliyuncs.com"
            };

            var client = new Client(config);

            // 调用API验证凭证
            var response = await client.QueryAccountBalanceAsync();

            var isValid = response.Body?.Success == true;
            _logger?.LogInformation("阿里云凭证验证{Result}", isValid ? "成功" : "失败");

            return isValid;
        }
        catch (TeaException ex)
        {
            _logger?.LogError(ex, "阿里云凭证验证失败：API调用异常 - {ErrorCode}: {ErrorMessage}", ex.Code, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "阿里云凭证验证失败：{ErrorMessage}", ex.Message);
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
            _logger?.LogInformation("开始查询阿里云账户余额");

            var accessKeyId = credentials.GetCredential("AccessKeyId");
            var accessKeySecret = credentials.GetCredential("AccessKeySecret");

            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(accessKeySecret))
            {
                throw new InvalidOperationException("AccessKeyId 或 AccessKeySecret 未配置");
            }

            // 创建阿里云客户端配置
            var config = new AlibabaCloud.OpenApiClient.Models.Config
            {
                AccessKeyId = accessKeyId,
                AccessKeySecret = accessKeySecret,
                RegionId = "cn-hangzhou",
                Endpoint = "business.aliyuncs.com"
            };

            var client = new Client(config);

            // 调用API查询余额
            var response = await client.QueryAccountBalanceAsync();

            // 检查响应
            if (response.Body == null || response.Body.Data == null)
            {
                throw new InvalidOperationException("查询余额失败：响应数据为空");
            }

            if (response.Body.Success != true)
            {
                var errorMessage = response.Body.Message ?? "未知错误";
                throw new InvalidOperationException($"查询余额失败：{errorMessage}");
            }

            var data = response.Body.Data;

            // 解析余额数据
            var availableBalance = decimal.TryParse(data.AvailableAmount, out var available)
                ? available
                : 0m;

            var creditLimit = decimal.TryParse(data.CreditAmount, out var credit)
                ? credit
                : 0m;

            var mybankCredit = decimal.TryParse(data.MybankCreditAmount, out var mybank)
                ? mybank
                : 0m;

            // 总信用额度 = 信控额度 + 网商银行信用额度
            var totalCreditLimit = creditLimit + mybankCredit;

            _logger?.LogInformation("阿里云账户余额查询成功：可用余额={AvailableBalance} {Currency}",
                availableBalance, data.Currency ?? "CNY");

            return new AccountBalance
            {
                AvailableBalance = availableBalance,
                CreditLimit = totalCreditLimit,
                Currency = data.Currency ?? "CNY",
                QueryTime = DateTime.UtcNow
            };
        }
        catch (TeaException ex)
        {
            _logger?.LogError(ex, "阿里云API调用失败：{ErrorCode} - {ErrorMessage}", ex.Code, ex.Message);
            throw new InvalidOperationException($"阿里云API调用失败：{ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger?.LogError(ex, "查询阿里云余额失败：{ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"查询余额失败：{ex.Message}", ex);
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
                Description = "阿里云AccessKey ID，用于API认证",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入AccessKey ID"
            },
            new CredentialField
            {
                Key = "AccessKeySecret",
                DisplayName = "访问密钥密文",
                Description = "阿里云AccessKey Secret，用于API认证",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入AccessKey Secret"
            }
        };
    }
}
