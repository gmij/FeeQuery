using FeeQuery.Shared.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeeQuery.Data;

/// <summary>
/// 应用程序数据库上下文。
/// 实现 IDataProtectionKeyContext 以支持将 DataProtection 密钥存储到同一数据库，
/// 避免密钥文件丢失导致的凭证无法解密问题。
/// </summary>
public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 云账号
    /// </summary>
    public DbSet<CloudAccount> CloudAccounts { get; set; }

    /// <summary>
    /// 账单记录
    /// </summary>
    public DbSet<BillingRecord> BillingRecords { get; set; }

    /// <summary>
    /// 预警规则
    /// </summary>
    public DbSet<AlertRule> AlertRules { get; set; }

    /// <summary>
    /// 预警历史
    /// </summary>
    public DbSet<AlertHistory> AlertHistories { get; set; }

    /// <summary>
    /// 余额历史
    /// </summary>
    public DbSet<BalanceHistory> BalanceHistories { get; set; }

    /// <summary>
    /// 同步配置
    /// </summary>
    public DbSet<SyncConfiguration> SyncConfigurations { get; set; }

    /// <summary>
    /// 通知配置
    /// </summary>
    public DbSet<NotificationConfig> NotificationConfigs { get; set; }

    /// <summary>
    /// DataProtection 密钥（当 Security:EncryptionProvider=DataProtection 时使用）
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    /// <summary>
    /// 备份记录（记录每次备份操作的元数据）
    /// </summary>
    public DbSet<BackupRecord> BackupRecords { get; set; }

    /// <summary>
    /// 备份配置（单例，Id=1，运行时可修改无需重启）
    /// </summary>
    public DbSet<BackupSettings> BackupSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 CloudAccount
        modelBuilder.Entity<CloudAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProviderCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EncryptedCredentials).IsRequired();
            entity.HasIndex(e => e.ProviderCode);
            entity.HasIndex(e => e.IsEnabled);
        });

        // 配置 BillingRecord
        modelBuilder.Entity<BillingRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProviderCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);

            // 创建索引以优化查询
            entity.HasIndex(e => e.CloudAccountId);
            entity.HasIndex(e => e.BillingDate);
            entity.HasIndex(e => new { e.CloudAccountId, e.BillingDate });
            entity.HasIndex(e => e.ProviderCode);

            // 配置外键
            entity.HasOne(e => e.CloudAccount)
                  .WithMany()
                  .HasForeignKey(e => e.CloudAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置 AlertRule
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Threshold).HasPrecision(18, 2);
            entity.Property(e => e.AlertType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PeriodType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ComparisonOperator).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => e.CloudAccountId);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.AlertType);

            // 配置外键（可为空）
            entity.HasOne(e => e.CloudAccount)
                  .WithMany()
                  .HasForeignKey(e => e.CloudAccountId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 配置 AlertHistory
        modelBuilder.Entity<AlertHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActualAmount).HasPrecision(18, 2);
            entity.Property(e => e.ThresholdAmount).HasPrecision(18, 2);
            entity.Property(e => e.ExceedPercentage).HasPrecision(5, 2);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => e.AlertRuleId);
            entity.HasIndex(e => e.CloudAccountId);
            entity.HasIndex(e => e.TriggeredAt);
            entity.HasIndex(e => e.Status);

            // 配置外键
            entity.HasOne(e => e.AlertRule)
                  .WithMany()
                  .HasForeignKey(e => e.AlertRuleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CloudAccount)
                  .WithMany()
                  .HasForeignKey(e => e.CloudAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置 BalanceHistory
        modelBuilder.Entity<BalanceHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AvailableBalance).HasPrecision(18, 2);
            entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            entity.HasIndex(e => e.CloudAccountId);
            entity.HasIndex(e => e.RecordedAt);
            entity.HasIndex(e => new { e.CloudAccountId, e.RecordedAt });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.CloudAccountId, e.Status });

            entity.HasOne(e => e.CloudAccount)
                  .WithMany()
                  .HasForeignKey(e => e.CloudAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置 SyncConfiguration
        modelBuilder.Entity<SyncConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CronExpression).HasMaxLength(100);

            entity.HasIndex(e => e.CloudAccountId);
            entity.HasIndex(e => e.IsEnabled);

            entity.HasOne(e => e.CloudAccount)
                  .WithMany()
                  .HasForeignKey(e => e.CloudAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置 NotificationConfig
        modelBuilder.Entity<NotificationConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ChannelType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ConfigJson).IsRequired();
            entity.Property(e => e.LastTestError).HasMaxLength(2000);

            entity.HasIndex(e => e.ChannelType);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.IsDefault);
        });

        // 配置 BackupRecord
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BackupType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(20);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });

        // 配置 BackupSettings（单例）
        modelBuilder.Entity<BackupSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BackupType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Directory).HasMaxLength(500);
        });
    }
}
