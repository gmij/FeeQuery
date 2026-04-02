using FeeQuery.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Providers.Common;

/// <summary>
/// 基于签名认证的云厂商提供者基类
/// 适用于火山云、腾讯云、百度云等使用HMAC-SHA256签名的厂商
/// </summary>
public abstract class SignatureBasedProvider : BaseCloudProvider
{
    protected SignatureBasedProvider(
        IHttpClientFactory? httpClientFactory = null,
        ILogger? logger = null)
        : base(httpClientFactory, logger)
    {
    }

    /// <summary>
    /// 获取服务端点（如 billing.volcengineapi.com）
    /// </summary>
    protected abstract string GetEndpoint();

    /// <summary>
    /// 获取服务名称（如 billing）
    /// </summary>
    protected abstract string GetServiceName();

    /// <summary>
    /// 获取区域（如 cn-north-1）
    /// </summary>
    protected abstract string GetRegion();

    /// <summary>
    /// 构建规范化请求字符串
    /// 不同厂商的实现略有差异
    /// </summary>
    protected abstract string BuildCanonicalRequest(
        string method,
        string path,
        string queryString,
        string headers,
        string signedHeaders,
        string hashedPayload);

    /// <summary>
    /// 构建待签名字符串
    /// 不同厂商的实现略有差异
    /// </summary>
    protected abstract string BuildStringToSign(
        string algorithm,
        string timestamp,
        string credentialScope,
        string hashedCanonicalRequest);

    /// <summary>
    /// 计算签名
    /// 不同厂商的密钥派生过程可能不同
    /// </summary>
    protected abstract string CalculateSignature(
        string secretKey,
        string dateStamp,
        string region,
        string service,
        string stringToSign);

    /// <summary>
    /// 构建Authorization头
    /// </summary>
    protected abstract string BuildAuthorizationHeader(
        string accessKey,
        string credentialScope,
        string signedHeaders,
        string signature);

    /// <summary>
    /// 通用的签名API调用方法
    /// </summary>
    protected async Task<JsonElement?> CallSignedApiAsync(
        string action,
        HttpMethod method,
        string path,
        Dictionary<string, object>? parameters,
        CloudCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        // 从凭证中获取AccessKey和SecretKey
        var accessKey = credentials.Credentials["AccessKeyId"];
        var secretKey = credentials.Credentials["SecretAccessKey"];

        // 构建请求
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");

        // 构建查询字符串
        var queryParams = new Dictionary<string, string>
        {
            ["Action"] = action,
            ["Version"] = GetApiVersion()
        };

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                queryParams[param.Key] = param.Value?.ToString() ?? "";
            }
        }

        // 构建规范化查询字符串（按字母顺序排序）
        var sortedParams = queryParams.OrderBy(kv => kv.Key);
        var queryString = string.Join("&", sortedParams.Select(kv =>
            $"{UriEncode(kv.Key)}={UriEncode(kv.Value)}"));

        // 构建请求头
        var headers = new Dictionary<string, string>
        {
            ["Host"] = GetEndpoint(),
            ["X-Date"] = timestamp,
            ["Content-Type"] = "application/json"
        };

        // 构建签名
        var signedHeaders = string.Join(";", headers.Keys.Select(k => k.ToLower()).OrderBy(k => k));
        var canonicalHeaders = string.Join("\n",
            headers.OrderBy(kv => kv.Key.ToLower()).Select(kv => $"{kv.Key.ToLower()}:{kv.Value}")) + "\n";

        var hashedPayload = ToHexString(ComputeSHA256(""));

        var canonicalRequest = BuildCanonicalRequest(
            method.Method,
            path,
            queryString,
            canonicalHeaders,
            signedHeaders,
            hashedPayload);

        var hashedCanonicalRequest = ToHexString(ComputeSHA256(canonicalRequest));

        var credentialScope = $"{dateStamp}/{GetRegion()}/{GetServiceName()}/request";
        var stringToSign = BuildStringToSign(
            "HMAC-SHA256",
            timestamp,
            credentialScope,
            hashedCanonicalRequest);

        var signature = CalculateSignature(
            secretKey,
            dateStamp,
            GetRegion(),
            GetServiceName(),
            stringToSign);

        var authorization = BuildAuthorizationHeader(
            accessKey,
            credentialScope,
            signedHeaders,
            signature);

        headers["Authorization"] = authorization;

        // 发送请求
        var url = $"https://{GetEndpoint()}{path}?{queryString}";
        return await SendHttpRequestAsync(method, url, headers, null, cancellationToken);
    }

    /// <summary>
    /// 获取API版本（子类可以覆盖）
    /// </summary>
    protected virtual string GetApiVersion()
    {
        return "2022-01-01";
    }
}
