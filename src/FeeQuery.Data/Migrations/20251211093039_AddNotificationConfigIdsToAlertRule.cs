using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationConfigIdsToAlertRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationConfigIds",
                table: "AlertRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationConfigIds",
                table: "AlertRules");
        }
    }
}
