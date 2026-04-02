using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeeQuery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupRemark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "BackupRecords",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remark",
                table: "BackupRecords");
        }
    }
}
