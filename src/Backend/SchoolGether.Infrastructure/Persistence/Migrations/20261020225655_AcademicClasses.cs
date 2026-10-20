using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "study_year",
                table: "academic_course_unit",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_academic_course_unit_institution_id_course_id_id",
                table: "academic_course_unit",
                columns: new[] { "institution_id", "course_id", "id" });

            migrationBuilder.CreateTable(
                name: "academic_class",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    study_year = table.Column<int>(type: "int", nullable: false),
                    academic_year = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false),
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
                    table.PrimaryKey("PK_academic_class", x => x.id);
                    table.UniqueConstraint("AK_academic_class_institution_id_course_id_id", x => new { x.institution_id, x.course_id, x.id });
                    table.ForeignKey(
                        name: "FK_academic_class_academic_course_institution_id_course_id",
                        columns: x => new { x.institution_id, x.course_id },
                        principalTable: "academic_course",
                        principalColumns: new[] { "institution_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "academic_class_unit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    class_id = table.Column<int>(type: "int", nullable: false),
                    unit_id = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_class_unit", x => x.id);
                    table.ForeignKey(
                        name: "FK_academic_class_unit_academic_class_institution_id_course_id_~",
                        columns: x => new { x.institution_id, x.course_id, x.class_id },
                        principalTable: "academic_class",
                        principalColumns: new[] { "institution_id", "course_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_class_unit_academic_course_unit_institution_id_cour~",
                        columns: x => new { x.institution_id, x.course_id, x.unit_id },
                        principalTable: "academic_course_unit",
                        principalColumns: new[] { "institution_id", "course_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_academic_class_institution_id_client_id",
                table: "academic_class",
                columns: new[] { "institution_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_class_institution_id_course_id_study_year_academic_~",
                table: "academic_class",
                columns: new[] { "institution_id", "course_id", "study_year", "academic_year", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_class_unit_class_id_unit_id",
                table: "academic_class_unit",
                columns: new[] { "class_id", "unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_class_unit_institution_id_course_id_class_id",
                table: "academic_class_unit",
                columns: new[] { "institution_id", "course_id", "class_id" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_class_unit_institution_id_course_id_unit_id",
                table: "academic_class_unit",
                columns: new[] { "institution_id", "course_id", "unit_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_class_unit");

            migrationBuilder.DropTable(
                name: "academic_class");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_academic_course_unit_institution_id_course_id_id",
                table: "academic_course_unit");

            migrationBuilder.DropColumn(
                name: "study_year",
                table: "academic_course_unit");
        }
    }
}
