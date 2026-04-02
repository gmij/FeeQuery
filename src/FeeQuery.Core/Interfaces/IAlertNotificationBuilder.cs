using FeeQuery.Shared.Models;

namespace FeeQuery.Core.Interfaces;

/// <summary>
/// 预警通知构建器接口
/// </summary>
public interface IAlertNotificationBuilder
{
    /// <summary>
    /// 支持的预警类型
    /// </summary>
    string AlertType { get; }

    /// <summary>
    /// 构建通知标题
    /// </summary>
    string BuildTitle(AlertNotificationContext context);

    /// <summary>
    /// 构建通知内容（纯文本）
    /// </summary>
    string BuildContent(AlertNotificationContext context);

    /// <summary>
    /// 构建富文本内容（Markdown格式，可选）
    /// </summary>
    string? BuildRichContent(AlertNotificationContext context);

    /// <summary>
    /// 构建操作按钮列表（用于交互式通知，可选）
    /// </summary>
    List<AlertActionButton>? BuildActions(AlertNotificationContext context);
}

/// <summary>
/// 预警通知上下文
/// </summary>
public class AlertNotificationContext
{
    /// <summary>
    /// 预警历史记录
    /// </summary>
    public required AlertHistory Alert { get; set; }

    /// <summary>
    /// 预警规则
    /// </summary>
    public required AlertRule Rule { get; set; }

    /// <summary>
    /// 云账号信息
    /// </summary>
    public required CloudAccount Account { get; set; }

    /// <summary>
    /// 账户余额（用于余额预警）
    /// </summary>
    public AccountBalance? Balance { get; set; }

    /// <summary>
    /// 实际值
    /// </summary>
    public decimal ActualValue { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public decimal ThresholdValue { get; set; }

    /// <summary>
    /// 超出百分比
    /// </summary>
    public decimal? ExceedPercentage { get; set; }

    /// <summary>
    /// 基础URL（用于构建链接）
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 是否是恢复通知
    /// </summary>
    public bool IsRecoveryNotification { get; set; }
}

/// <summary>
/// 预警操作按钮
/// </summary>
public class AlertActionButton
{
    /// <summary>
    /// 按钮文本
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// 按钮URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// 按钮样式（如 "primary", "danger"）
    /// </summary>
    public string? Style { get; set; }
}
