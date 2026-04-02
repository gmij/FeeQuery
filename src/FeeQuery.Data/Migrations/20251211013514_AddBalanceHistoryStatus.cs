using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceHistoryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "BalanceHistories",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BalanceHistories",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SyncDurationMs",
                table: "BalanceHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BalanceHistories_CloudAccountId_Status",
                table: "BalanceHistories",
                columns: new[] { "CloudAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceHistories_Status",
                table: "BalanceHistories",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BalanceHistories_CloudAccountId_Status",
                table: "BalanceHistories");

            migrationBuilder.DropIndex(
                name: "IX_BalanceHistories_Status",
                table: "BalanceHistories");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "BalanceHistories");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BalanceHistories");

            migrationBuilder.DropColumn(
                name: "SyncDurationMs",
                table: "BalanceHistories");
        }
    }
}
