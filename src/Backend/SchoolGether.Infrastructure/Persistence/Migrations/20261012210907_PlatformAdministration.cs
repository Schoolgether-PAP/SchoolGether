using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "user_account",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "master_slot",
                table: "user_account",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "permissions_json",
                table: "user_account",
                type: "varchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "audit_entry",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actor_id = table.Column<int>(type: "int", nullable: false),
                    actor_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    institution_id = table.Column<int>(type: "int", nullable: true),
                    action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    target = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    result = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    before = table.Column<string>(type: "text", nullable: true),
                    after = table.Column<string>(type: "text", nullable: true),
                    request_reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entry", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_master_slot",
                table: "user_account",
                column: "master_slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entry_created_at",
                table: "audit_entry",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry");

            migrationBuilder.DropIndex(
                name: "IX_user_account_master_slot",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "master_slot",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "permissions_json",
                table: "user_account");
        }
    }
}
