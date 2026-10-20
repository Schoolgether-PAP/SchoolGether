using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicCourseUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_academic_course_institution_id_id",
                table: "academic_course",
                columns: new[] { "institution_id", "id" });

            migrationBuilder.CreateTable(
                name: "academic_course_unit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    syllabus = table.Column<string>(type: "varchar(5000)", maxLength: 5000, nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creation_fingerprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_course_unit", x => x.id);
                    table.ForeignKey(
                        name: "FK_academic_course_unit_academic_course_institution_id_course_id",
                        columns: x => new { x.institution_id, x.course_id },
                        principalTable: "academic_course",
                        principalColumns: new[] { "institution_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_academic_course_unit_institution_id_client_id",
                table: "academic_course_unit",
                columns: new[] { "institution_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_course_unit_institution_id_course_id_code",
                table: "academic_course_unit",
                columns: new[] { "institution_id", "course_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_course_unit");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_academic_course_institution_id_id",
                table: "academic_course");
        }
    }
}
