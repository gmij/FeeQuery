namespace FeeQuery.Shared.Models;

/// <summary>
/// 备份配置（数据库单例记录，Id 固定为 1）
/// 存入数据库以支持运行时修改、无需重启服务
/// </summary>
public class BackupSettings
{
    public int Id { get; set; }

    /// <summary>是否启用自动备份</summary>
    public bool Enabled { get; set; }

    /// <summary>备份类型：Full | Config | Incremental</summary>
    public string BackupType { get; set; } = "Full";

    /// <summary>备份格式：Json | Sqlite</summary>
    public string Format { get; set; } = "Json";

    /// <summary>Cron 表达式（标准 5 段）</summary>
    public string CronExpression { get; set; } = "0 2 * * *";

    /// <summary>自动备份保留份数</summary>
    public int RetentionCount { get; set; } = 7;

    /// <summary>备份目录，空字符串表示使用默认目录</summary>
    public string Directory { get; set; } = "";

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
