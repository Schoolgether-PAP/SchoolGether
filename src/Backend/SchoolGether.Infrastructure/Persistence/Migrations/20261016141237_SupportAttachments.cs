using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_attachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    message_id = table.Column<int>(type: "int", nullable: false),
                    storage_key = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    file_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    content_type = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_attachment", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_attachment_support_message_message_id",
                        column: x => x.message_id,
                        principalTable: "support_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "support_storage_usage",
                columns: table => new
                {
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    used_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_storage_usage", x => x.institution_id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_support_attachment_institution_id_message_id",
                table: "support_attachment",
                columns: new[] { "institution_id", "message_id" });

            migrationBuilder.CreateIndex(
                name: "IX_support_attachment_message_id",
                table: "support_attachment",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_attachment_storage_key",
                table: "support_attachment",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_storage_usage_institution_id",
                table: "support_storage_usage",
                column: "institution_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_attachment");

            migrationBuilder.DropTable(
                name: "support_storage_usage");
        }
    }
}
