using FeeQuery.Data;
using FeeQuery.Shared.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeeQuery.Core.Services;

/// <summary>
/// 备份数据的 JSON 根结构
/// </summary>
public class BackupPayload
{
    public string Version { get; set; } = "1.0";
    public string BackupType { get; set; } = "Full";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IncrementalSince { get; set; }
    public BackupData Data { get; set; } = new();
}

public class BackupData
{
    public List<CloudAccount> CloudAccounts { get; set; } = [];
    public List<AlertRule> AlertRules { get; set; } = [];
    public List<NotificationConfig> NotificationConfigs { get; set; } = [];
    public List<SyncConfiguration> SyncConfigurations { get; set; } = [];
    public List<DataProtectionKey> DataProtectionKeys { get; set; } = [];
    // 仅 Full / Incremental 包含
    public List<BalanceHistory>? BalanceHistories { get; set; }
    public List<AlertHistory>? AlertHistories { get; set; }
    public List<BillingRecord>? BillingRecords { get; set; }
}

/// <summary>
/// 备份服务，支持 JSON 和 SQLite 两种格式，以及全量/配置/增量三种备份类型
/// </summary>
public class BackupService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public BackupService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IConfiguration configuration,
        ILogger<BackupService> logger)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 获取备份目录（优先读 IConfiguration，最后用默认值）
    /// </summary>
    public string GetBackupDirectory(string? settingsDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
            return settingsDirectory;
        var configured = _configuration.GetValue<string>("Backup:Directory");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        return Path.Combine(AppContext.BaseDirectory, "data", "backups");
    }

    /// <summary>
    /// 配置变更后自动创建配置备份，仅保留最近 10 份自动备份
    /// </summary>
    public async Task CreateAutoConfigBackupAsync(string? remark = null)
    {
        try
        {
            var record = await CreateBackupAsync("Config", "Json", isAutomatic: true, remark: remark);
            if (record.Status == "Success")
                await CleanupAutoBackupsAsync(10);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "自动配置备份失败，跳过");
        }
    }

    /// <summary>
    /// 创建备份
    /// </summary>
    public async Task<BackupRecord> CreateBackupAsync(string backupType, string format, bool isAutomatic = false, string? remark = null)
    {
        var dir = GetBackupDirectory();
        Directory.CreateDirectory(dir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var ext = format.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ? "db" : "json";
        var fileName = $"feequery_{backupType.ToLower()}_{timestamp}.{ext}";
        var filePath = Path.Combine(dir, fileName);

        var record = new BackupRecord
        {
            BackupType = backupType,
            Format = format,
            FileName = fileName,
            IsAutomatic = isAutomatic,
            Remark = remark,
            Status = "Failed"
        };

        try
        {
            if (format.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                await CreateSqliteBackupAsync(filePath);
            else
                record.TotalRecords = await CreateJsonBackupAsync(filePath, backupType);

            var fileInfo = new FileInfo(filePath);
            record.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
            record.Status = "Success";
            _logger.LogInformation("备份成功：{FileName}，大小：{Size} 字节", fileName, record.FileSizeBytes);
        }
        catch (Exception ex)
        {
            record.ErrorMessage = ex.Message;
            _logger.LogError(ex, "备份失败：{BackupType}/{Format}", backupType, format);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        // 确定增量起始时间
        if (backupType.Equals("Incremental", StringComparison.OrdinalIgnoreCase))
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var lastSuccess = await db.BackupRecords
                .Where(r => r.Status == "Success")
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            record.IncrementalSince = lastSuccess?.CreatedAt;
        }

        // 保存备份记录
        await using var saveDb = await _dbFactory.CreateDbContextAsync();
        saveDb.BackupRecords.Add(record);
        await saveDb.SaveChangesAsync();

        return record;
    }

    /// <summary>
    /// 获取备份记录列表（按时间倒序）
    /// </summary>
    public async Task<List<BackupRecord>> GetBackupListAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BackupRecords
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 删除备份（同时删除文件和数据库记录）
    /// </summary>
    public async Task DeleteBackupAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var record = await db.BackupRecords.FindAsync(id);
        if (record == null) return;

        var filePath = Path.Combine(GetBackupDirectory(), record.FileName);
        if (File.Exists(filePath)) File.Delete(filePath);

        db.BackupRecords.Remove(record);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 获取备份文件的完整路径
    /// </summary>
    public async Task<string?> GetBackupFilePathAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var record = await db.BackupRecords.FindAsync(id);
        if (record == null) return null;

        var path = Path.Combine(GetBackupDirectory(), record.FileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 获取备份记录（含文件路径验证）
    /// </summary>
    public async Task<BackupRecord?> GetBackupRecordAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BackupRecords.FindAsync(id);
    }

    /// <summary>
    /// 从 JSON 文件恢复（覆盖现有数据，在事务中执行）
    /// </summary>
    public async Task RestoreFromJsonAsync(Stream jsonStream)
    {
        var payload = await JsonSerializer.DeserializeAsync<BackupPayload>(jsonStream, _jsonOptions)
            ?? throw new InvalidOperationException("备份文件格式无效");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            // 始终清除并恢复配置数据
            db.DataProtectionKeys.RemoveRange(db.DataProtectionKeys);
            db.AlertRules.RemoveRange(db.AlertRules);
            db.NotificationConfigs.RemoveRange(db.NotificationConfigs);
            db.SyncConfigurations.RemoveRange(db.SyncConfigurations);
            db.CloudAccounts.RemoveRange(db.CloudAccounts);

            // 仅 Full 备份才清除历史表，Config 备份只还原配置，历史数据保持不变
            if (payload.BackupType.Equals("Full", StringComparison.OrdinalIgnoreCase))
            {
                db.BillingRecords.RemoveRange(db.BillingRecords);
                db.AlertHistories.RemoveRange(db.AlertHistories);
                db.BalanceHistories.RemoveRange(db.BalanceHistories);
            }

            await db.SaveChangesAsync();

            // 还原配置数据
            if (payload.Data.DataProtectionKeys.Count > 0)
                await db.DataProtectionKeys.AddRangeAsync(payload.Data.DataProtectionKeys);
            if (payload.Data.CloudAccounts.Count > 0)
                await db.CloudAccounts.AddRangeAsync(payload.Data.CloudAccounts);
            if (payload.Data.AlertRules.Count > 0)
                await db.AlertRules.AddRangeAsync(payload.Data.AlertRules);
            if (payload.Data.NotificationConfigs.Count > 0)
                await db.NotificationConfigs.AddRangeAsync(payload.Data.NotificationConfigs);
            if (payload.Data.SyncConfigurations.Count > 0)
                await db.SyncConfigurations.AddRangeAsync(payload.Data.SyncConfigurations);

            // 还原历史数据（若包含）
            if (payload.Data.BalanceHistories?.Count > 0)
                await db.BalanceHistories.AddRangeAsync(payload.Data.BalanceHistories);
            if (payload.Data.AlertHistories?.Count > 0)
                await db.AlertHistories.AddRangeAsync(payload.Data.AlertHistories);
            if (payload.Data.BillingRecords?.Count > 0)
                await db.BillingRecords.AddRangeAsync(payload.Data.BillingRecords);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            _logger.LogInformation("从备份恢复成功，备份类型：{Type}，创建时间：{At}", payload.BackupType, payload.CreatedAt);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 清理旧备份，仅保留最近 retentionCount 份成功备份
    /// </summary>
    public async Task CleanupOldBackupsAsync(int retentionCount)
    {
        if (retentionCount <= 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var toDelete = await db.BackupRecords
            .Where(r => r.Status == "Success")
            .OrderByDescending(r => r.CreatedAt)
            .Skip(retentionCount)
            .ToListAsync();

        foreach (var record in toDelete)
        {
            var filePath = Path.Combine(GetBackupDirectory(), record.FileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            db.BackupRecords.Remove(record);
        }

        if (toDelete.Count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("已清理 {Count} 份旧备份（保留 {Retain} 份）", toDelete.Count, retentionCount);
        }
    }

    /// <summary>
    /// 仅清理自动备份，保留最近 keepCount 份，手动备份不受影响
    /// </summary>
    private async Task CleanupAutoBackupsAsync(int keepCount)
    {
        if (keepCount <= 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var toDelete = await db.BackupRecords
            .Where(r => r.Status == "Success" && r.IsAutomatic)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(keepCount)
            .ToListAsync();

        foreach (var record in toDelete)
        {
            var filePath = Path.Combine(GetBackupDirectory(), record.FileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            db.BackupRecords.Remove(record);
        }

        if (toDelete.Count > 0)
            await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    // 私有方法
    // ──────────────────────────────────────────────

    private async Task<int> CreateJsonBackupAsync(string filePath, string backupType)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // 确定增量起始时间
        DateTime? since = null;
        if (backupType.Equals("Incremental", StringComparison.OrdinalIgnoreCase))
        {
            var lastSuccess = await db.BackupRecords
                .Where(r => r.Status == "Success")
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            since = lastSuccess?.CreatedAt;
        }

        var payload = new BackupPayload
        {
            BackupType = backupType,
            CreatedAt = DateTime.UtcNow,
            IncrementalSince = since,
            Data = new BackupData
            {
                CloudAccounts = await db.CloudAccounts.AsNoTracking().ToListAsync(),
                AlertRules = await db.AlertRules.AsNoTracking().ToListAsync(),
                NotificationConfigs = await db.NotificationConfigs.AsNoTracking().ToListAsync(),
                SyncConfigurations = await db.SyncConfigurations.AsNoTracking().ToListAsync(),
                DataProtectionKeys = await db.DataProtectionKeys.AsNoTracking().ToListAsync(),
            }
        };

        // Full 和 Incremental 包含历史数据
        if (!backupType.Equals("Config", StringComparison.OrdinalIgnoreCase))
        {
            payload.Data.BalanceHistories = since.HasValue
                ? await db.BalanceHistories.AsNoTracking().Where(h => h.RecordedAt > since.Value).ToListAsync()
                : await db.BalanceHistories.AsNoTracking().ToListAsync();

            payload.Data.AlertHistories = since.HasValue
                ? await db.AlertHistories.AsNoTracking().Where(h => h.TriggeredAt > since.Value).ToListAsync()
                : await db.AlertHistories.AsNoTracking().ToListAsync();

            payload.Data.BillingRecords = since.HasValue
                ? await db.BillingRecords.AsNoTracking().Where(r => r.CreatedAt > since.Value).ToListAsync()
                : await db.BillingRecords.AsNoTracking().ToListAsync();
        }

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        var total = payload.Data.CloudAccounts.Count
            + payload.Data.AlertRules.Count
            + payload.Data.NotificationConfigs.Count
            + payload.Data.SyncConfigurations.Count
            + payload.Data.DataProtectionKeys.Count
            + (payload.Data.BalanceHistories?.Count ?? 0)
            + (payload.Data.AlertHistories?.Count ?? 0)
            + (payload.Data.BillingRecords?.Count ?? 0);
        return total;
    }

    private async Task CreateSqliteBackupAsync(string destPath)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var srcConnectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");

        // 从连接字符串解析 SQLite 文件路径
        var builder = new SqliteConnectionStringBuilder(srcConnectionString);
        var srcPath = builder.DataSource;

        // 使用 SQLite 在线备份 API（支持并发读写）
        using var source = new SqliteConnection($"Data Source={srcPath}");
        using var destination = new SqliteConnection($"Data Source={destPath}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }
}
