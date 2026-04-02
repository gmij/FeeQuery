using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepeatNotificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRepeatCount",
                table: "AlertRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatNotificationInterval",
                table: "AlertRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastNotificationAt",
                table: "AlertHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatNotificationCount",
                table: "AlertHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxRepeatCount",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "RepeatNotificationInterval",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "LastNotificationAt",
                table: "AlertHistories");

            migrationBuilder.DropColumn(
                name: "RepeatNotificationCount",
                table: "AlertHistories");
        }
    }
}
