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

namespace FeeQuery.Providers.Baidu;

/// <summary>
/// 百度云厂商适配器
/// 基于百度云BCE签名算法
/// 文档：https://cloud.baidu.com/doc/BILLING/s/Gjz5wfh8l
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class BaiduCloudProvider : BaseCloudProvider
{
    private const string ServiceName = "billing";
    private const string Endpoint = "billing.baidubce.com";
    private const string Region = "global";

    public BaiduCloudProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<BaiduCloudProvider>? logger = null)
        : base(httpClientFactory, logger)
    {
    }

    public override string ProviderCode => "baidu";
    public override string ProviderName => "百度云";
    public override string Description => "百度云（Baidu Cloud）费用查询适配器，基于BCE签名算法实现";

    /// <summary>
    /// 验证凭证
    /// </summary>
    public override async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessKeyId = credentials.GetCredential("AccessKeyId");
            var secretAccessKey = credentials.GetCredential("SecretAccessKey");

            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
            {
                return false;
            }

            // 尝试调用GetAccountBalance API验证凭证
            var parameters = new Dictionary<string, object>();
            var result = await CallApiAsync("GetAccountBalance", parameters, credentials, cancellationToken);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "百度云凭证验证失败");
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
            _logger?.LogInformation("正在查询百度云账户余额...");

            if (credentials == null)
            {
                throw new InvalidOperationException("凭证对象为空");
            }

            var parameters = new Dictionary<string, object>();
            var response = await CallApiAsync("GetAccountBalance", parameters, credentials, cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("API响应为空");
            }

            _logger?.LogInformation("百度云余额查询响应: {Response}", response.Value.ToString());

            decimal cashBalance = 0;
            decimal creditLimit = 0;

            // 尝试多种可能的字段名
            if (response.Value.TryGetProperty("cashBalance", out _))
            {
                cashBalance = GetDecimalValue(response.Value, "cashBalance");
            }
            else if (response.Value.TryGetProperty("balance", out _))
            {
                cashBalance = GetDecimalValue(response.Value, "balance");
            }

            if (response.Value.TryGetProperty("creditLimit", out _))
            {
                creditLimit = GetDecimalValue(response.Value, "creditLimit");
            }
            else if (response.Value.TryGetProperty("credit", out _))
            {
                creditLimit = GetDecimalValue(response.Value, "credit");
            }

            _logger?.LogInformation("百度云余额查询成功: 现金余额={Cash}, 信用额度={Credit}",
                cashBalance, creditLimit);

            return new AccountBalance
            {
                AvailableBalance = cashBalance,
                CreditLimit = creditLimit,
                Currency = "CNY",
                QueryTime = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "百度云余额查询失败 - HTTP请求错误: {Message}", ex.Message);
            throw new InvalidOperationException($"百度云余额查询失败: {ex.Message}", ex);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "百度云余额查询失败 - 未知错误: {Message}", ex.Message);
            throw new InvalidOperationException($"百度云余额查询失败: {ex.Message}", ex);
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
        // TODO: 实现百度云账单数据获取
        _logger?.LogWarning("百度云账单数据获取功能尚未实现");
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
                Key = "AccessKeyId",
                DisplayName = "Access Key ID",
                Description = "百度云Access Key ID（AK），用于API认证",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入Access Key ID"
            },
            new CredentialField
            {
                Key = "SecretAccessKey",
                DisplayName = "Secret Access Key",
                Description = "百度云Secret Access Key（SK），用于API认证",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入Secret Access Key"
            }
        };
    }

    /// <summary>
    /// 获取API端点路径和HTTP方法
    /// </summary>
    private (string Path, string Method) GetApiEndpoint(string action)
    {
        return action switch
        {
            // 余额查询使用POST方法
            "GetAccountBalance" => ("/v1/finance/cash/balance", "POST"),
            "GetBillDetail" => ("/v1/finance/bill/detail", "POST"),
            _ => ($"/v1/{action}", "POST")
        };
    }

    /// <summary>
    /// 调用百度云API（使用BCE v1签名算法）
    /// 签名算法参考: https://cloud.baidu.com/doc/Reference/s/Njwvz1yfu
    /// </summary>
    private async Task<JsonElement?> CallApiAsync(
        string action,
        Dictionary<string, object> parameters,
        CloudCredentials credentials,
        CancellationToken cancellationToken)
    {
        var accessKeyId = credentials.GetCredential("AccessKeyId")
            ?? throw new InvalidOperationException("缺少AccessKeyId");
        var secretAccessKey = credentials.GetCredential("SecretAccessKey")
            ?? throw new InvalidOperationException("缺少SecretAccessKey");

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var expirationPeriodInSeconds = 1800;

        var (apiPath, httpMethod) = GetApiEndpoint(action);

        _logger?.LogDebug("=== 开始百度云API调用 (BCE v1): {Action} ===", action);
        _logger?.LogDebug("  HTTP Method: {Method}, Path: {Path}", httpMethod, apiPath);

        // 构建请求体
        string? jsonBody = null;
        if (parameters.Count > 0)
        {
            jsonBody = JsonSerializer.Serialize(parameters);
        }

        // ========== BCE v1 签名算法 ==========
        // Step 1: AuthStringPrefix
        var authStringPrefix = $"bce-auth-v1/{accessKeyId}/{timestamp}/{expirationPeriodInSeconds}";

        // Step 2: SigningKey = HMAC-SHA256(SK, AuthStringPrefix) -> 转为十六进制字符串
        var signingKeyBytes = ComputeHMAC(Encoding.UTF8.GetBytes(secretAccessKey), Encoding.UTF8.GetBytes(authStringPrefix));
        var signingKeyHex = ToHexString(signingKeyBytes);

        // Step 3: CanonicalRequest
        // 格式: HTTPMethod + \n + URI + \n + QueryString + \n + CanonicalHeaders
        // 注意: x-bce-date 的值需要 URI 编码（冒号编码为 %3A）
        var encodedTimestamp = BceUriEncode(timestamp);
        var canonicalHeaders = $"host:{Endpoint}\nx-bce-date:{encodedTimestamp}";
        var canonicalRequest = $"{httpMethod}\n{apiPath}\n\n{canonicalHeaders}";

        // Step 4: Signature = HMAC-SHA256(SigningKeyHex, CanonicalRequest)
        // 关键：使用 SigningKey 的十六进制字符串作为 HMAC 密钥
        var signature = ToHexString(ComputeHMAC(Encoding.UTF8.GetBytes(signingKeyHex), Encoding.UTF8.GetBytes(canonicalRequest)));

        // Step 5: Authorization
        var signedHeaders = "host;x-bce-date";
        var authorization = $"{authStringPrefix}/{signedHeaders}/{signature}";

        _logger?.LogDebug("  CanonicalRequest: {CR}", canonicalRequest.Replace("\n", "\\n"));
        _logger?.LogDebug("  Authorization: {Auth}", authorization);

        // ========== 发送HTTP请求 ==========
        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var method = httpMethod == "GET" ? HttpMethod.Get : HttpMethod.Post;
        var requestUrl = $"https://{Endpoint}{apiPath}";
        var request = new HttpRequestMessage(method, requestUrl);

        // 设置请求头 - 注意：发送原始时间戳，不编码
        request.Headers.Host = Endpoint;
        request.Headers.TryAddWithoutValidation("x-bce-date", timestamp);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!string.IsNullOrEmpty(jsonBody))
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger?.LogDebug("响应: {StatusCode}, {Content}", response.StatusCode,
            responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API调用失败: {response.StatusCode}, 响应: {responseContent}");
        }

        var json = JsonDocument.Parse(responseContent);
        return json.RootElement;
    }

    /// <summary>
    /// BCE URI 编码
    /// 不编码的字符: A-Z a-z 0-9 - . _ ~
    /// 其他字符编码为 %XX (大写十六进制)
    /// </summary>
    private string BceUriEncode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(input))
        {
            char c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '_' || c == '~')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
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

        if (name.Contains("bcc") || name.Contains("云服务器") || name.Contains("实例") || name.Contains("compute"))
            return "计算";
        if (name.Contains("bos") || name.Contains("cfs") || name.Contains("存储") || name.Contains("对象存储") || name.Contains("storage"))
            return "存储";
        if (name.Contains("vpc") || name.Contains("eip") || name.Contains("网络") || name.Contains("带宽") || name.Contains("blb"))
            return "网络";
        if (name.Contains("rds") || name.Contains("mongodb") || name.Contains("redis") || name.Contains("数据库") || name.Contains("database"))
            return "数据库";
        if (name.Contains("cdn") || name.Contains("内容分发"))
            return "CDN";
        if (name.Contains("ai") || name.Contains("智能") || name.Contains("机器学习") || name.Contains("ocr") || name.Contains("语音"))
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
