using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteNotificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationChannels",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "NotificationTargets",
                table: "AlertRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationChannels",
                table: "AlertRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotificationTargets",
                table: "AlertRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
