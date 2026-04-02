using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProviderCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EncryptedCredentials = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CloudAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Threshold = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PeriodType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationChannels = table.Column<string>(type: "TEXT", nullable: false),
                    NotificationTargets = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRules_CloudAccounts_CloudAccountId",
                        column: x => x.CloudAccountId,
                        principalTable: "CloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BillingRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CloudAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BillingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Usage = table.Column<decimal>(type: "TEXT", nullable: true),
                    UsageUnit = table.Column<string>(type: "TEXT", nullable: true),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingRecords_CloudAccounts_CloudAccountId",
                        column: x => x.CloudAccountId,
                        principalTable: "CloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlertRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CloudAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExceedPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertHistories_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertHistories_CloudAccounts_CloudAccountId",
                        column: x => x.CloudAccountId,
                        principalTable: "CloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_AlertRuleId",
                table: "AlertHistories",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_CloudAccountId",
                table: "AlertHistories",
                column: "CloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_Status",
                table: "AlertHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_TriggeredAt",
                table: "AlertHistories",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_CloudAccountId",
                table: "AlertRules",
                column: "CloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_IsEnabled",
                table: "AlertRules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRecords_BillingDate",
                table: "BillingRecords",
                column: "BillingDate");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRecords_CloudAccountId",
                table: "BillingRecords",
                column: "CloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRecords_CloudAccountId_BillingDate",
                table: "BillingRecords",
                columns: new[] { "CloudAccountId", "BillingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingRecords_ProviderCode",
                table: "BillingRecords",
                column: "ProviderCode");

            migrationBuilder.CreateIndex(
                name: "IX_CloudAccounts_IsEnabled",
                table: "CloudAccounts",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CloudAccounts_ProviderCode",
                table: "CloudAccounts",
                column: "ProviderCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertHistories");

            migrationBuilder.DropTable(
                name: "BillingRecords");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "CloudAccounts");
        }
    }
}
