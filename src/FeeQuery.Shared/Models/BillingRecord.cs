namespace FeeQuery.Shared.Models;

/// <summary>
/// 账单记录
/// </summary>
public class BillingRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 云账号ID
    /// </summary>
    public int CloudAccountId { get; set; }

    /// <summary>
    /// 云厂商标识符
    /// </summary>
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>
    /// 账单日期
    /// </summary>
    public DateTime BillingDate { get; set; }

    /// <summary>
    /// 服务名称（如 "云服务器ECS"、"对象存储OSS"）
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 服务类别（如 "计算"、"存储"、"网络"）
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 费用金额（人民币，保留2位小数）
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 货币单位
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 使用量
    /// </summary>
    public decimal? Usage { get; set; }

    /// <summary>
    /// 使用量单位
    /// </summary>
    public string? UsageUnit { get; set; }

    /// <summary>
    /// 区域（如 "cn-hangzhou"）
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 原始数据（JSON格式，保存厂商原始账单数据）
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的云账号
    /// </summary>
    public CloudAccount? CloudAccount { get; set; }
}
