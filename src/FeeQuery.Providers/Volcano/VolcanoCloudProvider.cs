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

namespace FeeQuery.Providers.Volcano;

/// <summary>
/// 火山引擎厂商适配器
/// 基于火山引擎OpenAPI实现（使用签名算法V4）
/// 文档：https://www.volcengine.com/docs/6269/1165275
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class VolcanoCloudProvider : BaseCloudProvider
{
    private const string ServiceName = "billing";
    private const string ApiVersion = "2022-01-01";
    private const string Endpoint = "billing.volcengineapi.com";
    private const string Region = "cn-north-1";

    public VolcanoCloudProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<VolcanoCloudProvider>? logger = null)
        : base(httpClientFactory, logger)
    {
    }

    public override string ProviderCode => "volcano";
    public override string ProviderName => "火山引擎";
    public override string Description => "火山引擎（Volcano Engine）费用查询适配器，基于OpenAPI V4签名实现";

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

            // 尝试调用ListBillDetail API验证凭证
            var now = DateTime.UtcNow;
            var parameters = new Dictionary<string, object>
            {
                { "BillPeriod", now.AddMonths(-1).ToString("yyyy-MM") },
                { "Limit", 1 }
            };

            var result = await CallApiAsync("ListBillDetail", parameters, credentials, cancellationToken);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "火山引擎凭证验证失败");
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
            var parameters = new Dictionary<string, object>();
            // QueryBalanceAcct 使用 GET 方法，不需要请求体参数
            var response = await CallApiAsync("QueryBalanceAcct", parameters, credentials, cancellationToken, useGetMethod: true);

            _logger?.LogDebug("火山引擎余额查询响应: {Response}", response);

            if (response.HasValue && response.Value.TryGetProperty("Result", out var result))
            {
                // 火山引擎返回字段说明：
                // - CashBalance: 现金余额
                // - CreditLimit: 信用额度
                // - AvailableBalance: 可用余额（现金 + 信用 - 欠费 - 冻结）
                // - ArrearsBalance: 欠费金额
                // - FreezeAmount: 冻结金额

                // 我们模型中的 AvailableBalance 定义为"现金余额"（不含信用额度）
                // 当有欠费时，现金余额应为负数，表示已透支
                var cashBalance = GetDecimalValue(result, "CashBalance");
                var arrearsBalance = GetDecimalValue(result, "ArrearsBalance");

                // 计算实际可用现金余额：现金余额 - 欠费金额
                // 如果已欠费，结果为负数，表示已透支信用额度
                var availableBalance = cashBalance - arrearsBalance;

                return new AccountBalance
                {
                    AvailableBalance = availableBalance,
                    CreditLimit = GetDecimalValue(result, "CreditLimit"),
                    Currency = "CNY",
                    QueryTime = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "火山引擎余额查询失败");
        }

        return new AccountBalance
        {
            AvailableBalance = 0,
            Currency = "CNY",
            QueryTime = DateTime.UtcNow
        };
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
        var records = new List<BillingRecord>();

        try
        {
            // 火山引擎账单数据按月查询
            var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
            var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

            while (currentMonth <= endMonth)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "BillPeriod", currentMonth.ToString("yyyy-MM") },
                    { "Limit", 100 },
                    { "Offset", 0 }
                };

                var hasMore = true;
                while (hasMore)
                {
                    var response = await CallApiAsync("ListBillDetail", parameters, credentials, cancellationToken);

                    if (response.HasValue && response.Value.TryGetProperty("Result", out var result))
                    {
                        if (result.TryGetProperty("List", out var list) && list.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in list.EnumerateArray())
                            {
                                var billDate = GetStringValue(item, "BillDate");
                                if (DateTime.TryParse(billDate, out var date) && date >= startDate && date <= endDate)
                                {
                                    records.Add(new BillingRecord
                                    {
                                        BillingDate = date,
                                        ServiceName = GetStringValue(item, "ProductName", "未知服务"),
                                        Amount = GetDecimalValue(item, "BillAmount"),
                                        Currency = "CNY",
                                        Region = GetStringValue(item, "Region", "全球"),
                                        Category = GetStringValue(item, "BillType", ""),
                                        Metadata = item.GetRawText()
                                    });
                                }
                            }
                        }

                        // 检查是否还有更多数据
                        var total = GetIntValue(result, "Total", 0);
                        var offset = (int)parameters["Offset"];
                        var limit = (int)parameters["Limit"];

                        if (offset + limit < total)
                        {
                            parameters["Offset"] = offset + limit;
                        }
                        else
                        {
                            hasMore = false;
                        }
                    }
                    else
                    {
                        hasMore = false;
                    }
                }

                currentMonth = currentMonth.AddMonths(1);
            }

            _logger?.LogInformation("火山引擎账单数据获取成功，共 {Count} 条记录", records.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "火山引擎账单数据获取失败");
        }

        return records;
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
                DisplayName = "访问密钥ID",
                Description = "火山引擎Access Key ID（AK），用于API认证。需要授予 BillingCenterBillReadOnlyAccess 权限策略",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入Access Key ID"
            },
            new CredentialField
            {
                Key = "SecretAccessKey",
                DisplayName = "访问密钥密文",
                Description = "火山引擎Secret Access Key（SK），用于API认证。确保该密钥对拥有账单查询权限",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入Secret Access Key"
            }
        };
    }

    /// <summary>
    /// 调用火山引擎API（使用签名算法V4）
    /// </summary>
    private async Task<JsonElement?> CallApiAsync(
        string action,
        Dictionary<string, object> parameters,
        CloudCredentials credentials,
        CancellationToken cancellationToken,
        bool useGetMethod = false)
    {
        var accessKeyId = credentials.GetCredential("AccessKeyId")
            ?? throw new InvalidOperationException("缺少AccessKeyId");
        var secretAccessKey = credentials.GetCredential("SecretAccessKey")
            ?? throw new InvalidOperationException("缺少SecretAccessKey");

        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");

        // 构建查询字符串（Action 和 Version 作为查询参数）
        var queryParams = new Dictionary<string, string>
        {
            { "Action", action },
            { "Version", ApiVersion }
        };
        var canonicalQueryString = string.Join("&", queryParams.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        // 根据请求方法构建请求体
        byte[] bodyBytes;
        string bodyHash;
        string httpMethod;

        if (useGetMethod)
        {
            // GET 请求没有请求体
            httpMethod = "GET";
            bodyBytes = Array.Empty<byte>();
            bodyHash = ToHexString(ComputeSHA256(bodyBytes));
        }
        else
        {
            // POST 请求包含JSON请求体
            httpMethod = "POST";
            var jsonBody = parameters.Count > 0 ? JsonSerializer.Serialize(parameters) : "{}";
            bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            bodyHash = ToHexString(ComputeSHA256(bodyBytes));
        }

        // 构建规范请求
        var canonicalUri = "/";
        var canonicalHeaders = useGetMethod
            ? $"host:{Endpoint}\nx-date:{amzDate}\n"
            : $"content-type:application/json\nhost:{Endpoint}\nx-date:{amzDate}\n";
        var signedHeaders = useGetMethod
            ? "host;x-date"
            : "content-type;host;x-date";

        var canonicalRequest = $"{httpMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{bodyHash}";
        var canonicalRequestHash = ToHexString(ComputeSHA256(Encoding.UTF8.GetBytes(canonicalRequest)));

        // 构建待签名字符串
        var credentialScope = $"{dateStamp}/{Region}/{ServiceName}/request";
        var stringToSign = $"HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        // 计算签名
        var kDate = ComputeHMAC(Encoding.UTF8.GetBytes(secretAccessKey), Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = ComputeHMAC(kDate, Encoding.UTF8.GetBytes(Region));
        var kService = ComputeHMAC(kRegion, Encoding.UTF8.GetBytes(ServiceName));
        var kSigning = ComputeHMAC(kService, Encoding.UTF8.GetBytes("request"));
        var signature = ToHexString(ComputeHMAC(kSigning, Encoding.UTF8.GetBytes(stringToSign)));

        // 构建Authorization头
        var authorization = $"HMAC-SHA256 Credential={accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        // 发送HTTP请求
        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        var requestUrl = $"https://{Endpoint}{canonicalUri}?{canonicalQueryString}";
        var request = new HttpRequestMessage(
            useGetMethod ? HttpMethod.Get : HttpMethod.Post,
            requestUrl);
        request.Headers.Add("X-Date", amzDate);
        // 使用 TryAddWithoutValidation 避免格式验证问题
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!useGetMethod)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("火山引擎API调用失败: {StatusCode}, {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"API调用失败: {response.StatusCode}");
        }

        var json = JsonDocument.Parse(responseContent);
        return json.RootElement;
    }

}
