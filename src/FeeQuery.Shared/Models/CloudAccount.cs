namespace FeeQuery.Shared.Models;

/// <summary>
/// 云账号信息
/// </summary>
public class CloudAccount
{
    /// <summary>
    /// 账号ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 账号名称（用户自定义）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 云厂商标识符（如 "alibaba", "tencent", "huawei"）
    /// </summary>
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>
    /// 云厂商显示名称（如 "阿里云", "腾讯云"）
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 加密的凭证数据（JSON格式，包含AccessKey等）
    /// </summary>
    public string EncryptedCredentials { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

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

    /// <summary>
    /// 最后同步时间
    /// </summary>
    public DateTime? LastSyncAt { get; set; }
}
