using FeeQuery.Providers.Common;
using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Providers.Tencent;

/// <summary>
/// 腾讯云厂商适配器
/// 基于腾讯云API 3.0（TC3-HMAC-SHA256签名算法）
/// 文档：https://cloud.tencent.com/document/api/555/19182
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class TencentCloudProvider : BaseCloudProvider
{
    private const string ServiceName = "billing";
    private const string ApiVersion = "2018-07-09";
    private const string Endpoint = "billing.tencentcloudapi.com";
    private const string Region = "ap-guangzhou";

    public TencentCloudProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<TencentCloudProvider>? logger = null)
        : base(httpClientFactory, logger)
    {
    }

    public override string ProviderCode => "tencent";
    public override string ProviderName => "腾讯云";
    public override string Description => "腾讯云（Tencent Cloud）费用查询适配器，基于API 3.0 TC3-HMAC-SHA256签名实现";

    /// <summary>
    /// 验证凭证
    /// </summary>
    public override async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var secretId = credentials.GetCredential("SecretId");
            var secretKey = credentials.GetCredential("SecretKey");

            if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            {
                return false;
            }

            // 尝试调用DescribeAccountBalance API验证凭证
            var parameters = new Dictionary<string, object>();
            var result = await CallApiAsync("DescribeAccountBalance", parameters, credentials, cancellationToken);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "腾讯云凭证验证失败");
            return false;
        }
    }

    /// <summary>
    /// 获取账户余额
    /// </summary>
    public override async Task<AccountBalance> GetAccountBalanceAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("正在查询腾讯云账户余额...");

            var parameters = new Dictionary<string, object>();
            var response = await CallApiAsync("DescribeAccountBalance", parameters, credentials, cancellationToken);

            if (response == null)
            {
                _logger?.LogWarning("腾讯云余额查询响应为空");
                throw new InvalidOperationException("API响应为空");
            }

            if (!response.Value.TryGetProperty("Response", out var responseData))
            {
                _logger?.LogWarning("腾讯云余额查询响应格式错误，缺少Response字段");
                throw new InvalidOperationException("API响应格式错误");
            }

            // 检查是否有错误
            if (responseData.TryGetProperty("Error", out var error))
            {
                var errorCode = GetStringValue(error, "Code");
                var errorMessage = GetStringValue(error, "Message");
                _logger?.LogError("腾讯云API返回错误: Code={ErrorCode}, Message={ErrorMessage}",
                    errorCode, errorMessage);
                throw new InvalidOperationException($"腾讯云API错误: {errorCode} - {errorMessage}");
            }

            _logger?.LogInformation("腾讯云余额响应字段: {Fields}",
                string.Join(", ", responseData.EnumerateObject().Select(p => p.Name)));

            // 腾讯云返回的金额单位是分，需要除以100转换为元
            // Balance: 可用余额 = 现金 + 赠金(代金券) + 收益金 - 冻结金额
            // RealBalance: 现金账户余额
            // PresentBalance: 赠金账户余额（即代金券）
            // IncomeIntoAccountBalance: 收益转入账户余额
            var balance = GetDecimalValue(responseData, "Balance") / 100m;
            var credit = GetDecimalValue(responseData, "Credit") / 100m;
            var cashBalance = GetDecimalValue(responseData, "RealBalance") / 100m;  // 现金余额
            var voucherBalance = GetDecimalValue(responseData, "PresentBalance") / 100m;  // 代金券余额（赠金）

            _logger?.LogInformation("腾讯云余额详情: 可用余额={Balance}元, 信用额度={Credit}元, 现金={Cash}元, 代金券={Voucher}元",
                balance, credit, cashBalance, voucherBalance);

            // 如果有现金和代金券的详细数据，使用它们
            // 否则使用Balance字段
            var availableBalance = (cashBalance > 0 || voucherBalance > 0)
                ? cashBalance + voucherBalance
                : balance;

            _logger?.LogInformation("腾讯云余额查询成功: 可用余额={Available}元, 信用额度={Credit}元",
                availableBalance, credit);

            return new AccountBalance
            {
                AvailableBalance = availableBalance,
                CreditLimit = credit,
                Currency = "CNY",
                QueryTime = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "腾讯云余额查询失败 - HTTP请求错误: {Message}", ex.Message);
            throw new InvalidOperationException($"腾讯云余额查询失败: {ex.Message}", ex);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "腾讯云余额查询失败 - 未知错误: {Message}", ex.Message);
            throw new InvalidOperationException($"腾讯云余额查询失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取账单数据
    /// </summary>
    public override async Task<List<BillingRecord>> GetBillingDataAsync(
        CloudCredentials credentials,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // TODO: 实现腾讯云账单数据获取
        _logger?.LogWarning("腾讯云账单数据获取功能尚未实现");
        return await Task.FromResult(new List<BillingRecord>());
    }

    /// <summary>
    /// 获取所需凭证字段
    /// </summary>
    public override List<CredentialField> GetRequiredCredentialFields()
    {
        return new List<CredentialField>
        {
            new CredentialField
            {
                Key = "SecretId",
                DisplayName = "Secret ID",
                Description = "腾讯云Secret ID，用于API认证",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入Secret ID"
            },
            new CredentialField
            {
                Key = "SecretKey",
                DisplayName = "Secret Key",
                Description = "腾讯云Secret Key，用于API认证",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入Secret Key"
            }
        };
    }

    /// <summary>
    /// 调用腾讯云API（使用TC3-HMAC-SHA256签名算法）
    /// </summary>
    private async Task<JsonElement?> CallApiAsync(
        string action,
        Dictionary<string, object> parameters,
        CloudCredentials credentials,
        CancellationToken cancellationToken)
    {
        var secretId = credentials.GetCredential("SecretId")
            ?? throw new InvalidOperationException("缺少SecretId");
        var secretKey = credentials.GetCredential("SecretKey")
            ?? throw new InvalidOperationException("缺少SecretKey");

        var now = DateTime.UtcNow;
        var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
        var date = now.ToString("yyyy-MM-dd");

        // 构建请求体
        var jsonBody = parameters.Count > 0 ? JsonSerializer.Serialize(parameters) : "{}";
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var bodyHash = ToHexString(ComputeSHA256(bodyBytes));

        // 构建规范请求
        var httpMethod = "POST";
        var canonicalUri = "/";
        var canonicalQueryString = "";
        var canonicalHeaders = $"content-type:application/json\nhost:{Endpoint}\n";
        var signedHeaders = "content-type;host";
        var canonicalRequest = $"{httpMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{bodyHash}";
        var canonicalRequestHash = ToHexString(ComputeSHA256(Encoding.UTF8.GetBytes(canonicalRequest)));

        // 构建待签名字符串
        var credentialScope = $"{date}/{ServiceName}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{canonicalRequestHash}";

        // 计算签名
        var secretDate = ComputeHMAC(Encoding.UTF8.GetBytes($"TC3{secretKey}"), Encoding.UTF8.GetBytes(date));
        var secretService = ComputeHMAC(secretDate, Encoding.UTF8.GetBytes(ServiceName));
        var secretSigning = ComputeHMAC(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        var signature = ToHexString(ComputeHMAC(secretSigning, Encoding.UTF8.GetBytes(stringToSign)));

        // 构建Authorization头
        var authorization = $"TC3-HMAC-SHA256 Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        // 发送HTTP请求
        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Endpoint}");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.Add("X-TC-Action", action);
        request.Headers.Add("X-TC-Version", ApiVersion);
        request.Headers.Add("X-TC-Timestamp", timestamp.ToString());
        request.Headers.Add("X-TC-Region", Region);

        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _logger?.LogInformation("腾讯云API请求: {Action}, Timestamp={Timestamp}",
            action, timestamp);

        var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger?.LogInformation("腾讯云API响应: HttpStatusCode={StatusCode}, ContentLength={Length}",
            response.StatusCode, responseContent.Length);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("腾讯云API调用失败: {StatusCode}, {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"API调用失败: {response.StatusCode}, 响应: {responseContent}");
        }

        _logger?.LogDebug("腾讯云API响应内容: {Content}",
            responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);

        var json = JsonDocument.Parse(responseContent);
        return json.RootElement;
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

        if (name.Contains("cvm") || name.Contains("云服务器") || name.Contains("实例") || name.Contains("compute"))
            return "计算";
        if (name.Contains("cos") || name.Contains("cfs") || name.Contains("存储") || name.Contains("对象存储") || name.Contains("storage"))
            return "存储";
        if (name.Contains("vpc") || name.Contains("网络") || name.Contains("带宽") || name.Contains("clb") || name.Contains("负载均衡"))
            return "网络";
        if (name.Contains("cdb") || name.Contains("mongodb") || name.Contains("redis") || name.Contains("数据库") || name.Contains("database"))
            return "数据库";
        if (name.Contains("cdn") || name.Contains("内容分发"))
            return "CDN";
        if (name.Contains("ai") || name.Contains("智能") || name.Contains("机器学习") || name.Contains("ocr"))
            return "AI服务";
        if (name.Contains("安全") || name.Contains("防护") || name.Contains("waf"))
            return "安全";

        return "其他";
    }

    private DateTime? ParseDate(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            var value = prop.GetString();
            if (DateTime.TryParse(value, out var date))
            {
                return date;
            }
        }
        return null;
    }
}
