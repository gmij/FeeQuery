using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Providers.WanjieFangzhou;

/// <summary>
/// 万界方舟适配器
/// 万界方舟是AI算力服务提供商，通过用户名密码登录获取余额
/// 登录接口：POST https://fangzhou.wanjiedata.com/maas/login/v2/smsAuthV2
/// 登录响应中直接包含余额，无需额外查询
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class WanjieFangzhouProvider : ICloudProvider
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<WanjieFangzhouProvider>? _logger;
    private const string LoginUrl = "https://fangzhou.wanjiedata.com/maas/login/v2/smsAuthV2";

    public WanjieFangzhouProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<WanjieFangzhouProvider>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderCode => "wanjie";
    public string ProviderName => "万界方舟";
    public string Description => "万界方舟AI算力服务费用查询适配器，基于用户名密码认证";

    /// <summary>
    /// 验证凭证是否有效（尝试登录）
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var username = credentials.GetCredential("Username");
            var password = credentials.GetCredential("Password");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger?.LogWarning("万界方舟凭证验证失败：用户名或密码为空");
                return false;
            }

            _logger?.LogInformation("正在验证万界方舟凭证，Username={Username}", username);

            var result = await GetAccountBalanceAsync(credentials, cancellationToken);

            _logger?.LogInformation("万界方舟凭证验证成功，余额={Balance}", result.AvailableBalance);
            return result.AvailableBalance >= 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "万界方舟凭证验证失败：{Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 获取账户余额（通过登录接口，响应中直接包含余额）
    /// </summary>
    public async Task<AccountBalance> GetAccountBalanceAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var username = credentials.GetCredential("Username")
            ?? throw new InvalidOperationException("缺少用户名（Username）");
        var password = credentials.GetCredential("Password")
            ?? throw new InvalidOperationException("缺少密码（Password）");

        _logger?.LogInformation("正在查询万界方舟账户余额，Username={Username}", username);

        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var requestBody = new
        {
            phone = username,
            username = username,
            password = password,
            tokenExpireInterval = 720
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "万界方舟API请求超时");
            throw new InvalidOperationException("万界方舟API请求超时", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "万界方舟API请求失败：{Message}", ex.Message);
            throw new InvalidOperationException($"万界方舟API请求失败：{ex.Message}", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogInformation("万界方舟API响应：HttpStatusCode={StatusCode}", response.StatusCode);
        _logger?.LogDebug("万界方舟API响应内容：{Content}",
            responseContent.Length > 500 ? responseContent[..500] + "..." : responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("万界方舟API调用失败：{StatusCode}, {Content}", response.StatusCode, responseContent);
            throw new InvalidOperationException($"万界方舟API调用失败：HTTP {(int)response.StatusCode}");
        }

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(responseContent);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "万界方舟API响应JSON解析失败：{Content}", responseContent);
            throw new InvalidOperationException("万界方舟API响应格式错误", ex);
        }

        var root = json.RootElement;

        // 检查业务状态
        if (root.TryGetProperty("success", out var successElem) && !successElem.GetBoolean())
        {
            var message = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : null;
            _logger?.LogError("万界方舟登录失败：{Message}", message);
            throw new InvalidOperationException(
                string.IsNullOrEmpty(message) ? "万界方舟登录失败，请检查用户名和密码" : $"万界方舟登录失败：{message}");
        }

        if (root.TryGetProperty("code", out var codeElem) && codeElem.GetInt32() != 200)
        {
            var message = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : null;
            _logger?.LogError("万界方舟登录返回错误码：{Code}, {Message}", codeElem.GetInt32(), message);
            throw new InvalidOperationException(
                string.IsNullOrEmpty(message) ? "万界方舟登录失败，请检查用户名和密码" : $"万界方舟登录失败：{message}");
        }

        // 从登录响应的 result.userInfo.balance 提取余额
        if (!root.TryGetProperty("result", out var resultElem)
            || !resultElem.TryGetProperty("userInfo", out var userInfoElem)
            || !userInfoElem.TryGetProperty("balance", out var balanceElem))
        {
            _logger?.LogError("万界方舟API响应缺少余额字段，响应：{Content}", responseContent);
            throw new InvalidOperationException("万界方舟API响应格式错误：缺少余额数据");
        }

        decimal balance;
        if (balanceElem.ValueKind == JsonValueKind.Number)
        {
            balance = balanceElem.GetDecimal();
        }
        else if (balanceElem.ValueKind == JsonValueKind.String
            && decimal.TryParse(balanceElem.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            balance = parsed;
        }
        else
        {
            _logger?.LogError("万界方舟余额字段格式无法解析：{Value}", balanceElem.ToString());
            throw new InvalidOperationException("万界方舟API响应格式错误：余额字段无法解析");
        }

        _logger?.LogInformation("万界方舟余额查询成功：余额={Balance}", balance);

        return new AccountBalance
        {
            AvailableBalance = balance,
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
                Key = "Username",
                DisplayName = "用户名（手机号）",
                Description = "万界方舟登录手机号",
                Required = true,
                IsSensitive = false,
                Placeholder = "请输入手机号"
            },
            new CredentialField
            {
                Key = "Password",
                DisplayName = "密码",
                Description = "万界方舟登录密码",
                Required = true,
                IsSensitive = true,
                Placeholder = "请输入登录密码"
            }
        };
    }
}
