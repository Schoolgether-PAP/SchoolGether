using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformInstitutionsAndDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_suspended",
                table: "institution",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                table: "institution",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dashboard_preference",
                columns: table => new
                {
                    account_id = table.Column<int>(type: "int", nullable: false),
                    schema_version = table.Column<int>(type: "int", nullable: false),
                    widgets_json = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_preference", x => x.account_id);
                    table.ForeignKey(
                        name: "FK_dashboard_preference_user_account_account_id",
                        column: x => x.account_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_preference");

            migrationBuilder.DropColumn(
                name: "is_suspended",
                table: "institution");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                table: "institution");
        }
    }
}
