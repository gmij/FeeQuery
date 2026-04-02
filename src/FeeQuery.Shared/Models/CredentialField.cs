namespace FeeQuery.Shared.Models;

/// <summary>
/// 凭证字段定义
/// 用于描述每个云厂商需要哪些凭证字段
/// </summary>
public class CredentialField
{
    /// <summary>
    /// 字段键名（如 "AccessKeyId"）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 字段显示名称（如 "访问密钥ID"）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 字段描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 是否敏感信息（如密码、密钥）
    /// </summary>
    public bool IsSensitive { get; set; } = true;

    /// <summary>
    /// 占位符文本
    /// </summary>
    public string? Placeholder { get; set; }
}
