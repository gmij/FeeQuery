using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Notifications.DingTalk;

/// <summary>
/// 钉钉通知提供者
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class DingTalkNotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DingTalkNotificationProvider> _logger;

    public string ProviderType => "dingtalk";

    public DingTalkNotificationProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<DingTalkNotificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string title, string content, string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DingTalkConfig>(configJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookUrl))
            {
                _logger.LogError("钉钉配置解析失败");
                return false;
            }

            // 根据配置的消息类型选择发送方式
            if (config.MessageType == "actionCard" && config.Buttons != null && config.Buttons.Count > 0)
            {
                return await SendActionCardAsync(title, content, config.Buttons, configJson, cancellationToken);
            }
            else
            {
                return await SendTextMessageAsync(title, content, configJson, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉消息发送失败");
            return false;
        }
    }

    /// <summary>
    /// 发送交互式卡片消息（带按钮）
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">内容（Markdown格式）</param>
    /// <param name="buttons">按钮列表</param>
    /// <param name="configJson">钉钉配置JSON</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> SendActionCardAsync(
        string title,
        string content,
        List<ActionCardButton> buttons,
        string configJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DingTalkConfig>(configJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookUrl))
            {
                _logger.LogError("钉钉配置解析失败");
                return false;
            }

            var url = config.WebhookUrl;

            // 如果配置了Secret，生成签名
            if (!string.IsNullOrEmpty(config.Secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sign = GenerateSign(timestamp, config.Secret);
                url = $"{url}&timestamp={timestamp}&sign={sign}";
            }

            // 构建 ActionCard 消息体
            var message = new
            {
                msgtype = "actionCard",
                actionCard = new
                {
                    title,
                    text = content,  // Markdown 格式
                    btnOrientation = "0",  // 按钮竖直排列
                    btns = buttons.Select(b => new
                    {
                        title = b.Title,
                        actionURL = b.ActionUrl
                    }).ToList()
                }
            };

            var json = JsonSerializer.Serialize(message);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(url, httpContent, cancellationToken);

            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(result);
                    if (jsonDoc.RootElement.TryGetProperty("errcode", out var errCodeElement))
                    {
                        var errCode = errCodeElement.GetInt32();
                        if (errCode == 0)
                        {
                            _logger.LogInformation("钉钉 ActionCard 消息发送成功");
                            return true;
                        }
                        else
                        {
                            var errMsg = jsonDoc.RootElement.TryGetProperty("errmsg", out var errMsgElement)
                                ? errMsgElement.GetString()
                                : "未知错误";
                            _logger.LogError("钉钉 ActionCard 消息发送失败 (errcode={ErrCode}): {ErrMsg}", errCode, errMsg);
                            return false;
                        }
                    }

                    _logger.LogInformation("钉钉 ActionCard 消息发送成功: {Result}", result);
                    return true;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "解析钉钉响应失败: {Result}", result);
                    return false;
                }
            }
            else
            {
                _logger.LogError("钉钉 ActionCard 消息发送失败 (HTTP {StatusCode}): {Error}", response.StatusCode, result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉 ActionCard 消息发送失败");
            return false;
        }
    }

    /// <summary>
    /// 发送普通文本消息
    /// </summary>
    private async Task<bool> SendTextMessageAsync(string title, string content, string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DingTalkConfig>(configJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookUrl))
            {
                _logger.LogError("钉钉配置解析失败");
                return false;
            }

            var url = config.WebhookUrl;

            // 如果配置了Secret，生成签名
            if (!string.IsNullOrEmpty(config.Secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sign = GenerateSign(timestamp, config.Secret);
                url = $"{url}&timestamp={timestamp}&sign={sign}";
            }

            // 构建消息体
            var message = new
            {
                msgtype = "text",
                text = new
                {
                    content = $"{title}\n\n{content}"
                },
                at = new
                {
                    isAtAll = config.AtAll
                }
            };

            var json = JsonSerializer.Serialize(message);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(url, httpContent, cancellationToken);

            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            // 钉钉即使 HTTP 状态码为 200，也可能返回业务错误码
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(result);
                    if (jsonDoc.RootElement.TryGetProperty("errcode", out var errCodeElement))
                    {
                        var errCode = errCodeElement.GetInt32();
                        if (errCode == 0)
                        {
                            _logger.LogInformation("钉钉消息发送成功");
                            return true;
                        }
                        else
                        {
                            var errMsg = jsonDoc.RootElement.TryGetProperty("errmsg", out var errMsgElement)
                                ? errMsgElement.GetString()
                                : "未知错误";
                            _logger.LogError("钉钉消息发送失败 (errcode={ErrCode}): {ErrMsg}", errCode, errMsg);
                            return false;
                        }
                    }

                    // 如果没有 errcode 字段，认为成功
                    _logger.LogInformation("钉钉消息发送成功: {Result}", result);
                    return true;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "解析钉钉响应失败: {Result}", result);
                    return false;
                }
            }
            else
            {
                _logger.LogError("钉钉消息发送失败 (HTTP {StatusCode}): {Error}", response.StatusCode, result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉消息发送失败");
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DingTalkConfig>(configJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookUrl))
            {
                return (false, "钉钉配置解析失败或Webhook地址为空");
            }

            var url = config.WebhookUrl;

            // 如果配置了Secret，生成签名
            if (!string.IsNullOrEmpty(config.Secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sign = GenerateSign(timestamp, config.Secret);
                url = $"{url}&timestamp={timestamp}&sign={sign}";
            }

            // 构建测试消息
            var message = new
            {
                msgtype = "text",
                text = new
                {
                    content = "【测试】FeeQuery 钉钉通知测试\n\n这是一条测试消息，如果您收到此消息，说明钉钉通知配置正确。"
                },
                at = new
                {
                    isAtAll = config.AtAll
                }
            };

            var json = JsonSerializer.Serialize(message);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(url, httpContent, cancellationToken);

            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            // 钉钉即使 HTTP 状态码为 200，也可能返回业务错误码
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(result);
                    if (jsonDoc.RootElement.TryGetProperty("errcode", out var errCodeElement))
                    {
                        var errCode = errCodeElement.GetInt32();
                        if (errCode == 0)
                        {
                            _logger.LogInformation("钉钉测试消息发送成功");
                            return (true, null);
                        }
                        else
                        {
                            var errMsg = jsonDoc.RootElement.TryGetProperty("errmsg", out var errMsgElement)
                                ? errMsgElement.GetString()
                                : "未知错误";

                            _logger.LogError("钉钉测试失败 (errcode={ErrCode}): {ErrMsg}", errCode, errMsg);

                            // 返回友好的错误消息
                            return (false, $"钉钉返回错误 (代码: {errCode}): {errMsg}");
                        }
                    }

                    // 如果没有 errcode 字段，认为成功
                    _logger.LogInformation("钉钉测试消息发送成功: {Result}", result);
                    return (true, null);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "解析钉钉响应失败: {Result}", result);
                    return (false, $"解析钉钉响应失败: {ex.Message}");
                }
            }
            else
            {
                _logger.LogError("钉钉测试失败 (HTTP {StatusCode}): {Error}", response.StatusCode, result);
                return (false, $"HTTP请求失败 ({response.StatusCode}): {result}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "钉钉测试连接失败");
            return (false, $"网络请求失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉测试失败");
            return (false, $"测试失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成钉钉签名
    /// </summary>
    private string GenerateSign(long timestamp, string secret)
    {
        var stringToSign = $"{timestamp}\n{secret}";
        var encoding = Encoding.UTF8;
        var keyBytes = encoding.GetBytes(secret);
        var messageBytes = encoding.GetBytes(stringToSign);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        var sign = Convert.ToBase64String(hashBytes);

        return Uri.EscapeDataString(sign);
    }

    private class DingTalkConfig
    {
        public string WebhookUrl { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public bool AtAll { get; set; } = false;

        /// <summary>
        /// 消息类型：text=文本消息, actionCard=互动消息
        /// </summary>
        public string MessageType { get; set; } = "text";

        /// <summary>
        /// 互动消息按钮列表
        /// </summary>
        public List<ActionCardButton>? Buttons { get; set; }
    }

    /// <summary>
    /// ActionCard 按钮
    /// </summary>
    public class ActionCardButton
    {
        public string Title { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
    }
}
