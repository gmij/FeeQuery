using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Providers.Common;

/// <summary>
/// 云厂商提供者基类，提供通用功能
/// </summary>
public abstract class BaseCloudProvider : ICloudProvider
{
    protected readonly IHttpClientFactory? _httpClientFactory;
    protected readonly ILogger? _logger;

    public abstract string ProviderName { get; }
    public abstract string ProviderCode { get; }
    public abstract string Description { get; }

    protected BaseCloudProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract List<CredentialField> GetRequiredCredentialFields();

    public abstract Task<bool> ValidateCredentialsAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default);

    public abstract Task<List<BillingRecord>> GetBillingDataAsync(
        CloudCredentials credentials,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    public abstract Task<AccountBalance> GetAccountBalanceAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default);

    #region 通用加密工具方法

    /// <summary>
    /// 计算SHA256哈希
    /// </summary>
    protected byte[] ComputeSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }

    /// <summary>
    /// 计算SHA256哈希（字符串）
    /// </summary>
    protected byte[] ComputeSHA256(string data)
    {
        return ComputeSHA256(Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// 计算HMAC-SHA256
    /// </summary>
    protected byte[] ComputeHMAC(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// 计算HMAC-SHA256（字符串）
    /// </summary>
    protected byte[] ComputeHMAC(byte[] key, string data)
    {
        return ComputeHMAC(key, Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// 字节数组转十六进制字符串（小写）
    /// </summary>
    protected string ToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// URL编码（RFC 3986标准）
    /// </summary>
    protected string UriEncode(string value, bool encodeSlash = true)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var encoded = new StringBuilder();
        foreach (char c in value)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '~' || c == '.')
            {
                encoded.Append(c);
            }
            else if (c == '/' && !encodeSlash)
            {
                encoded.Append(c);
            }
            else
            {
                encoded.Append('%');
                encoded.Append(((int)c).ToString("X2"));
            }
        }
        return encoded.ToString();
    }

    #endregion

    #region 通用JSON解析方法

    /// <summary>
    /// 从JSON元素获取decimal值
    /// </summary>
    protected decimal GetDecimalValue(JsonElement element, string propertyName)
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
                if (!string.IsNullOrEmpty(str) && decimal.TryParse(str, out var result))
                {
                    return result;
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// 从JSON元素获取string值
    /// </summary>
    protected string GetStringValue(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// 从JSON元素获取int值
    /// </summary>
    protected int GetIntValue(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString();
                if (!string.IsNullOrEmpty(str) && int.TryParse(str, out var result))
                {
                    return result;
                }
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// 从JSON元素获取DateTime值
    /// </summary>
    protected DateTime GetDateTimeValue(JsonElement element, string propertyName, DateTime? defaultValue = null)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            var str = prop.GetString();
            if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, out var result))
            {
                return result;
            }
        }
        return defaultValue ?? DateTime.MinValue;
    }

    #endregion

    #region 通用凭证验证

    /// <summary>
    /// 验证必需的凭证字段是否存在
    /// </summary>
    protected bool ValidateRequiredCredentials(CloudCredentials credentials, params string[] requiredKeys)
    {
        if (credentials?.Credentials == null)
        {
            _logger?.LogWarning("凭证为空");
            return false;
        }

        foreach (var key in requiredKeys)
        {
            if (!credentials.Credentials.ContainsKey(key) ||
                string.IsNullOrWhiteSpace(credentials.Credentials[key]))
            {
                _logger?.LogWarning("缺少必需的凭证字段: {Key}", key);
                return false;
            }
        }

        return true;
    }

    #endregion

    #region 通用HTTP请求方法

    /// <summary>
    /// 发送HTTP请求并返回JSON响应
    /// </summary>
    protected async Task<JsonElement?> SendHttpRequestAsync(
        HttpMethod method,
        string url,
        Dictionary<string, string>? headers = null,
        string? jsonBody = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
            var request = new HttpRequestMessage(method, url);

            // 添加请求头
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // 添加请求体
            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("API调用失败: {StatusCode}, {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"API调用失败: {response.StatusCode}");
            }

            var json = JsonDocument.Parse(responseContent);
            return json.RootElement;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP请求失败: {Url}", url);
            throw;
        }
    }

    #endregion
}
