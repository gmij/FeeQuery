namespace FeeQuery.Shared.Models;

/// <summary>
/// 通知配置
/// 支持邮件、钉钉等多种通知渠道
/// </summary>
public class NotificationConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 通知渠道类型（email: 邮件, dingtalk: 钉钉, webhook: Webhook）
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 配置参数（JSON格式）
    /// Email: { "SmtpServer": "smtp.example.com", "Port": 587, "Username": "xxx", "Password": "xxx", "From": "xxx@example.com", "To": "xxx@example.com" }
    /// DingTalk: { "WebhookUrl": "https://oapi.dingtalk.com/robot/send?access_token=xxx", "Secret": "xxx" }
    /// Webhook: { "Url": "https://example.com/webhook", "Method": "POST", "Headers": {...} }
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// 是否为默认配置
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// 最后测试时间
    /// </summary>
    public DateTime? LastTestAt { get; set; }

    /// <summary>
    /// 最后测试是否成功
    /// </summary>
    public bool? LastTestSuccess { get; set; }

    /// <summary>
    /// 最后测试错误信息
    /// </summary>
    public string? LastTestError { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
