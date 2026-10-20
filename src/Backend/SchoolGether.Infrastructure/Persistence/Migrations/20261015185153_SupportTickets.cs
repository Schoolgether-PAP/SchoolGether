using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_ticket",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    author_id = table.Column<int>(type: "int", nullable: false),
                    author_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    subject = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    priority = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    assignee_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    closed_by = table.Column<int>(type: "int", nullable: true),
                    close_summary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "support_message",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    ticket_id = table.Column<int>(type: "int", nullable: false),
                    author_id = table.Column<int>(type: "int", nullable: false),
                    author_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    text = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    @internal = table.Column<bool>(name: "internal", type: "tinyint(1)", nullable: false),
                    attachments_count = table.Column<int>(type: "int", nullable: false),
                    fingerprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_message", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_message_support_ticket_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "support_ticket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_support_message_institution_id_ticket_id_id",
                table: "support_message",
                columns: new[] { "institution_id", "ticket_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_support_message_ticket_id_client_id",
                table: "support_message",
                columns: new[] { "ticket_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_institution_id_author_id_client_id",
                table: "support_ticket",
                columns: new[] { "institution_id", "author_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_institution_id_status_id",
                table: "support_ticket",
                columns: new[] { "institution_id", "status", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_message");

            migrationBuilder.DropTable(
                name: "support_ticket");
        }
    }
}
