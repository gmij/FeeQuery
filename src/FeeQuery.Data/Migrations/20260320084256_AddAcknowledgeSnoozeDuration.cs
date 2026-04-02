using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcknowledgeSnoozeDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcknowledgeSnoozeDuration",
                table: "AlertRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 480);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgeSnoozeDuration",
                table: "AlertRules");
        }
    }
}
