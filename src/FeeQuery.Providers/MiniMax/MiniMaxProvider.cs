using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Providers.MiniMax;

/// <summary>
/// MiniMax厂商适配器
/// MiniMax是AI模型服务提供商
/// 文档：https://api.minimax.chat/document
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class MiniMaxProvider : ICloudProvider
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<MiniMaxProvider>? _logger;
    private const string ApiBaseUrl = "https://www.minimaxi.com";

    public MiniMaxProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<MiniMaxProvider>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderCode => "minimax";
    public string ProviderName => "MiniMax";
    public string Description => "MiniMax AI模型服务费用查询适配器，基于API Key认证";

    /// <summary>
    /// 验证凭证
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = credentials.GetCredential("ApiKey");
            var groupId = credentials.GetCredential("GroupId");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(groupId))
            {
                _logger?.LogWarning("MiniMax凭证验证失败: ApiKey或GroupId为空");
                return false;
            }

            _logger?.LogInformation("正在验证MiniMax凭证，GroupId={GroupId}", groupId);

            // 尝试调用余额查询API验证凭证
            var result = await GetAccountBalanceAsync(credentials, cancellationToken);

            _logger?.LogInformation("MiniMax凭证验证成功，余额={Balance}", result.AvailableBalance);
            return result.AvailableBalance >= 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MiniMax凭证验证失败: {Message}", ex.Message);
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
            var groupId = credentials.GetCredential("GroupId")
                ?? throw new InvalidOperationException("缺少GroupId");

            _logger?.LogInformation("正在查询MiniMax账户余额，GroupId={GroupId}", groupId);

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", groupId }
            };

            var response = await CallApiAsync("GET", "/account/query_balance", parameters, credentials, cancellationToken);

            if (response == null)
            {
                _logger?.LogWarning("MiniMax余额查询响应为空");
                throw new InvalidOperationException("API响应为空");
            }

            _logger?.LogInformation("MiniMax余额查询响应结构: {ResponseKeys}",
                string.Join(", ", response.Value.EnumerateObject().Select(p => p.Name)));

            // MiniMax API有两种可能的响应格式：
            // 1. 带data字段: { "data": { "balance": "xxx" }, "base_resp": {...} }
            // 2. 直接返回: { "available_amount": "xxx", "cash_balance": "xxx", ..., "base_resp": {...} }

            decimal availableBalance;
            decimal? creditBalance = null;

            if (response.Value.TryGetProperty("data", out var data))
            {
                // 格式1: 有data字段包裹
                availableBalance = GetDecimalValue(data, "balance");
            }
            else if (response.Value.TryGetProperty("available_amount", out _))
            {
                // 格式2: 直接在根级别返回余额字段
                // 注意：available_amount = cash_balance + voucher_balance + 剩余可用信用额度
                var totalAvailable = GetDecimalValue(response.Value, "available_amount");
                var cashBalance = GetDecimalValue(response.Value, "cash_balance");
                var voucherBalance = GetDecimalValue(response.Value, "voucher_balance");
                var creditLimit = GetDecimalValue(response.Value, "credit_balance");

                // 计算实际现金余额（可能为负数，表示已透支）
                // totalAvailable = cashBalance + voucherBalance + min(creditLimit, creditLimit - 已用额度)
                // 推导：cashBalance + voucherBalance = totalAvailable - 剩余可用信用
                var cashAndVoucher = cashBalance + voucherBalance;

                if (cashAndVoucher <= 0 && creditLimit > 0)
                {
                    // 现金用完，正在使用信用额度
                    // 剩余可用信用 = totalAvailable - 0 = totalAvailable
                    var usedCredit = creditLimit - totalAvailable;

                    // AvailableBalance显示负数表示已透支金额
                    availableBalance = -usedCredit;
                    creditBalance = creditLimit;

                    _logger?.LogWarning("MiniMax账户已透支信用额度: 现金={Cash}, 代金券={Voucher}, 信用额度总量={CreditTotal}, 已用信用={UsedCredit}, 剩余可用信用={RemainingCredit}, 透支金额={Overdraft}",
                        cashBalance, voucherBalance, creditLimit, usedCredit, totalAvailable, usedCredit);
                }
                else
                {
                    // 正常情况：有现金或代金券余额
                    availableBalance = cashAndVoucher;
                    creditBalance = creditLimit;

                    _logger?.LogInformation("MiniMax余额详情: 总可用={Total}, 现金={Cash}, 代金券={Voucher}, 信用额度={Credit}, 可用余额={Available}",
                        totalAvailable, cashBalance, voucherBalance, creditLimit, availableBalance);
                }
            }
            else
            {
                _logger?.LogWarning("MiniMax余额查询响应格式不识别，响应内容: {Response}",
                    response.Value.ToString());
                throw new InvalidOperationException("API响应格式错误：无法识别的余额数据结构");
            }

            _logger?.LogInformation("MiniMax余额查询成功: 可用余额={Balance} 元", availableBalance);

            return new AccountBalance
            {
                AvailableBalance = availableBalance,
                CreditLimit = creditBalance,  // 总是设置，包括0
                Currency = "CNY",
                QueryTime = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "MiniMax余额查询失败 - HTTP请求错误: {Message}", ex.Message);
            throw new InvalidOperationException($"MiniMax余额查询失败: {ex.Message}", ex);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MiniMax余额查询失败 - 未知错误: {Message}", ex.Message);
            throw new InvalidOperationException($"MiniMax余额查询失败: {ex.Message}", ex);
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
                Key = "ApiKey",
                DisplayName = "API Key",
                Description = "MiniMax API密钥，用于API认证。在MiniMax控制台「API密钥管理」页面创建",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入API Key"
            },
            new CredentialField
            {
                Key = "GroupId",
                DisplayName = "Group ID",
                Description = "MiniMax组织ID（Group ID）。在MiniMax控制台「账户设置」页面查看",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入Group ID"
            }
        };
    }

    /// <summary>
    /// 调用MiniMax API（使用Bearer Token认证）
    /// </summary>
    private async Task<JsonElement?> CallApiAsync(
        string method,
        string path,
        Dictionary<string, object> parameters,
        CloudCredentials credentials,
        CancellationToken cancellationToken)
    {
        var apiKey = credentials.GetCredential("ApiKey")
            ?? throw new InvalidOperationException("缺少ApiKey");

        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        HttpRequestMessage request;
        if (method.ToUpper() == "GET" && parameters.Count > 0)
        {
            // GET请求：参数放在查询字符串中
            var queryString = string.Join("&", parameters.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value.ToString() ?? "")}"));
            var url = $"{ApiBaseUrl}{path}?{queryString}";

            _logger?.LogInformation("MiniMax API请求: {Method} {Url}", method, url);
            request = new HttpRequestMessage(HttpMethod.Get, url);
        }
        else if (method.ToUpper() == "POST")
        {
            // POST请求：参数放在请求体中
            var url = $"{ApiBaseUrl}{path}";
            _logger?.LogInformation("MiniMax API请求: {Method} {Url}", method, url);

            request = new HttpRequestMessage(HttpMethod.Post, url);
            var jsonBody = JsonSerializer.Serialize(parameters);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            _logger?.LogDebug("MiniMax API请求体: {Body}", jsonBody);
        }
        else
        {
            var url = $"{ApiBaseUrl}{path}";
            _logger?.LogInformation("MiniMax API请求: {Method} {Url}", method, url);
            request = new HttpRequestMessage(new HttpMethod(method), url);
        }

        // 添加认证头
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger?.LogInformation("MiniMax API响应: HttpStatusCode={StatusCode}, ContentLength={Length}",
                response.StatusCode, responseContent.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("MiniMax API调用失败: {StatusCode}, {Content}",
                    response.StatusCode, responseContent);

                // 尝试解析错误响应
                try
                {
                    var errorJson = JsonDocument.Parse(responseContent);
                    if (errorJson.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        var errorMessage = GetStringValue(errorElement, "message") ?? "未知错误";
                        var errorCode = GetStringValue(errorElement, "code") ?? "UNKNOWN";
                        throw new HttpRequestException(
                            $"MiniMax API错误 [{errorCode}]: {errorMessage} (HTTP {response.StatusCode})");
                    }
                }
                catch (JsonException)
                {
                    // 无法解析错误响应，使用默认错误消息
                }

                throw new HttpRequestException($"MiniMax API调用失败: HTTP {response.StatusCode}");
            }

            _logger?.LogDebug("MiniMax API响应内容: {Content}",
                responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);

            var json = JsonDocument.Parse(responseContent);
            return json.RootElement;
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "MiniMax API请求超时");
            throw new HttpRequestException("API请求超时", ex);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MiniMax API请求发生异常: {Message}", ex.Message);
            throw new HttpRequestException($"API请求失败: {ex.Message}", ex);
        }
    }

    private string? GetStringValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }

    private decimal GetDecimalValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDecimal();
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString();
                if (decimal.TryParse(str, out var result))
                {
                    return result;
                }
            }
        }
        return 0;
    }
}
