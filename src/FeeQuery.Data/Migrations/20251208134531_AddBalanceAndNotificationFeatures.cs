using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceAndNotificationFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertType",
                table: "AlertRules",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonOperator",
                table: "AlertRules",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BalanceHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CloudAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BalanceHistories_CloudAccounts_CloudAccountId",
                        column: x => x.CloudAccountId,
                        principalTable: "CloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ChannelType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastTestAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastTestSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CloudAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncBalance = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncBilling = table.Column<bool>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncConfigurations_CloudAccounts_CloudAccountId",
                        column: x => x.CloudAccountId,
                        principalTable: "CloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_AlertType",
                table: "AlertRules",
                column: "AlertType");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceHistories_CloudAccountId",
                table: "BalanceHistories",
                column: "CloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceHistories_CloudAccountId_RecordedAt",
                table: "BalanceHistories",
                columns: new[] { "CloudAccountId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceHistories_RecordedAt",
                table: "BalanceHistories",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationConfigs_ChannelType",
                table: "NotificationConfigs",
                column: "ChannelType");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationConfigs_IsDefault",
                table: "NotificationConfigs",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationConfigs_IsEnabled",
                table: "NotificationConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConfigurations_CloudAccountId",
                table: "SyncConfigurations",
                column: "CloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConfigurations_IsEnabled",
                table: "SyncConfigurations",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceHistories");

            migrationBuilder.DropTable(
                name: "NotificationConfigs");

            migrationBuilder.DropTable(
                name: "SyncConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AlertRules_AlertType",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "AlertType",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "ComparisonOperator",
                table: "AlertRules");
        }
    }
}
