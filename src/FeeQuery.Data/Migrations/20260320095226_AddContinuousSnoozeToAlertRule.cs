using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContinuousSnoozeToAlertRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContinuousSnooze",
                table: "AlertRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContinuousSnooze",
                table: "AlertRules");
        }
    }
}
