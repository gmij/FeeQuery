namespace FeeQuery.Shared.Models;

/// <summary>
/// 备份记录，记录每次备份操作的元数据
/// </summary>
public class BackupRecord
{
    public int Id { get; set; }

    /// <summary>
    /// 备份类型：Full（全量）| Config（配置）| Incremental（增量）
    /// </summary>
    public string BackupType { get; set; } = "Full";

    /// <summary>
    /// 备份格式：Json | Sqlite
    /// </summary>
    public string Format { get; set; } = "Json";

    /// <summary>
    /// 备份文件名（不含路径）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 备份文件大小（字节）
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// 备份状态：Success | Failed
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// 失败时的错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 备份包含的总记录数（JSON 格式）
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// 增量备份的起始时间（Full/Config 为 null）
    /// </summary>
    public DateTime? IncrementalSince { get; set; }

    /// <summary>
    /// 是否为定时自动备份（false = 手动触发）
    /// </summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// 备注：自动备份时记录触发原因（如"更新账号「火山云测试」"），手动备份为空
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 备份创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
