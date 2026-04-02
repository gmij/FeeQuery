namespace FeeQuery.Shared.Interfaces;

/// <summary>
/// 通知提供者接口
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// 提供者类型（email, dingtalk, webhook等）
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// 发送通知
    /// </summary>
    Task<bool> SendAsync(string title, string content, string configJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试连接
    /// </summary>
    /// <returns>成功标志和错误消息（成功时为null）</returns>
    Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(string configJson, CancellationToken cancellationToken = default);
}
