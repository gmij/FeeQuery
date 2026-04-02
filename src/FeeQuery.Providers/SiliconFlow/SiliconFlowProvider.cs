using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FeeQuery.Providers.SiliconFlow;

/// <summary>
/// 硅基流动适配器
/// 硅基流动是AI模型服务提供商，通过Web内部API查询余额
/// 注意：该API需要用户手动提供Web会话令牌（无官方余额查询API）
/// API端点：https://cloud.siliconflow.cn/walletd-server/api/v1/subject/profile/peek
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class SiliconFlowProvider : ICloudProvider
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<SiliconFlowProvider>? _logger;
    private const string ApiUrl = "https://cloud.siliconflow.cn/walletd-server/api/v1/subject/profile/peek";

    public SiliconFlowProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<SiliconFlowProvider>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderCode => "siliconflow";
    public string ProviderName => "硅基流动";
    public string Description => "硅基流动AI模型服务费用查询适配器，基于Web会话令牌认证";

    /// <summary>
    /// 验证凭证是否有效
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var subjectId = credentials.GetCredential("SubjectId");
            var sessionToken = credentials.GetCredential("SessionToken");

            if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(sessionToken))
            {
                _logger?.LogWarning("硅基流动凭证验证失败：账号ID或会话令牌为空");
                return false;
            }

            _logger?.LogInformation("正在验证硅基流动凭证，SubjectId={SubjectId}", subjectId);

            var result = await GetAccountBalanceAsync(credentials, cancellationToken);

            _logger?.LogInformation("硅基流动凭证验证成功，可用余额={Balance}", result.AvailableBalance);
            return result.AvailableBalance >= 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "硅基流动凭证验证失败：{Message}", ex.Message);
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
        var subjectId = credentials.GetCredential("SubjectId")
            ?? throw new InvalidOperationException("缺少账号ID（SubjectId）");
        var sessionToken = credentials.GetCredential("SessionToken")
            ?? throw new InvalidOperationException("缺少会话令牌（SessionToken）");

        _logger?.LogInformation("正在查询硅基流动账户余额，SubjectId={SubjectId}", subjectId);

        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
        request.Headers.Add("x-subject-id", subjectId);
        request.Headers.Add("Cookie", $"__SF_auth.session-token={sessionToken}");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "硅基流动API请求超时");
            throw new InvalidOperationException("硅基流动API请求超时", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "硅基流动API请求失败：{Message}", ex.Message);
            throw new InvalidOperationException($"硅基流动API请求失败：{ex.Message}", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogInformation("硅基流动API响应：HttpStatusCode={StatusCode}", response.StatusCode);
        _logger?.LogDebug("硅基流动API响应内容：{Content}",
            responseContent.Length > 500 ? responseContent[..500] + "..." : responseContent);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("会话令牌已过期或无效，请重新登录硅基流动并更新令牌");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("硅基流动API调用失败：{StatusCode}, {Content}", response.StatusCode, responseContent);
            throw new InvalidOperationException($"硅基流动API调用失败：HTTP {(int)response.StatusCode}");
        }

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(responseContent);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "硅基流动API响应JSON解析失败：{Content}", responseContent);
            throw new InvalidOperationException("硅基流动API响应格式错误", ex);
        }

        var root = json.RootElement;

        // 检查业务状态码
        if (root.TryGetProperty("code", out var codeElement))
        {
            var code = codeElement.GetInt32();
            if (code != 20000)
            {
                _logger?.LogError("硅基流动API返回业务错误码：{Code}, 响应：{Content}", code, responseContent);
                throw new InvalidOperationException(code == 40001 || code == 40003
                    ? "会话令牌已过期或无效，请重新登录硅基流动并更新令牌"
                    : $"硅基流动API返回错误码：{code}");
            }
        }

        // 解析余额：data.financialInfo
        if (!root.TryGetProperty("data", out var dataElement)
            || !dataElement.TryGetProperty("financialInfo", out var financialInfo))
        {
            _logger?.LogError("硅基流动API响应缺少财务信息字段，响应：{Content}", responseContent);
            throw new InvalidOperationException("硅基流动API响应格式错误：缺少financialInfo字段");
        }

        var available = ParseDecimalString(financialInfo, "available");
        var lineOfCredit = ParseDecimalString(financialInfo, "lineOfCredit");

        _logger?.LogInformation("硅基流动余额查询成功：可用余额={Available}，信用额度={Credit}", available, lineOfCredit);

        return new AccountBalance
        {
            AvailableBalance = available,
            CreditLimit = lineOfCredit > 0 ? lineOfCredit : null,
            Currency = "CNY",
            QueryTime = DateTime.UtcNow
        };
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
                Key = "SubjectId",
                DisplayName = "账号ID",
                Description = "硅基流动账号ID（数字），登录后在「用户中心」→「账号设置」中查看",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入硅基流动账号ID"
            },
            new CredentialField
            {
                Key = "SessionToken",
                DisplayName = "会话令牌",
                Description = "登录后的 __SF_auth.session-token Cookie值。打开浏览器开发者工具 → Application → Cookies → 找到并复制该值。注意：令牌会定期过期，过期后需重新获取",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入 __SF_auth.session-token 的值"
            }
        };
    }

    private static decimal ParseDecimalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return 0;

        if (prop.ValueKind == JsonValueKind.Number)
            return prop.GetDecimal();

        if (prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (decimal.TryParse(str, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }

        return 0;
    }
}
