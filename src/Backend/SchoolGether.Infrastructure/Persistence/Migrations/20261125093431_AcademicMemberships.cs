using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_institution_member_institution_id_user_id",
                table: "institution_member",
                columns: new[] { "institution_id", "user_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_academic_class_institution_id_course_id_study_year_academic_~",
                table: "academic_class",
                columns: new[] { "institution_id", "course_id", "study_year", "academic_year", "id" });

            migrationBuilder.CreateTable(
                name: "academic_assignment",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    class_id = table.Column<int>(type: "int", nullable: false),
                    unit_id = table.Column<int>(type: "int", nullable: false),
                    teacher_id = table.Column<int>(type: "int", nullable: false),
                    active_teacher_id = table.Column<int>(type: "int", nullable: true),
                    study_year = table.Column<int>(type: "int", nullable: false),
                    academic_year = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
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
                    table.PrimaryKey("PK_academic_assignment", x => x.id);
                    table.CheckConstraint("ck_assignment_active_slot", "(is_active = 1 AND active_teacher_id IS NOT NULL AND active_teacher_id = teacher_id) OR (is_active = 0 AND active_teacher_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_academic_assignment_academic_class_institution_id_course_id_~",
                        columns: x => new { x.institution_id, x.course_id, x.study_year, x.academic_year, x.class_id },
                        principalTable: "academic_class",
                        principalColumns: new[] { "institution_id", "course_id", "study_year", "academic_year", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_assignment_academic_course_unit_institution_id_cour~",
                        columns: x => new { x.institution_id, x.course_id, x.unit_id },
                        principalTable: "academic_course_unit",
                        principalColumns: new[] { "institution_id", "course_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_assignment_institution_member_institution_id_teache~",
                        columns: x => new { x.institution_id, x.teacher_id },
                        principalTable: "institution_member",
                        principalColumns: new[] { "institution_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "academic_enrolment",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    class_id = table.Column<int>(type: "int", nullable: false),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    active_student_id = table.Column<int>(type: "int", nullable: true),
                    study_year = table.Column<int>(type: "int", nullable: false),
                    academic_year = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
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
                    table.PrimaryKey("PK_academic_enrolment", x => x.id);
                    table.CheckConstraint("ck_enrolment_active_slot", "(is_active = 1 AND active_student_id IS NOT NULL AND active_student_id = student_id) OR (is_active = 0 AND active_student_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_academic_enrolment_academic_class_institution_id_course_id_s~",
                        columns: x => new { x.institution_id, x.course_id, x.study_year, x.academic_year, x.class_id },
                        principalTable: "academic_class",
                        principalColumns: new[] { "institution_id", "course_id", "study_year", "academic_year", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_enrolment_institution_member_institution_id_student~",
                        columns: x => new { x.institution_id, x.student_id },
                        principalTable: "institution_member",
                        principalColumns: new[] { "institution_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_academic_assignment_institution_id_class_id_unit_id_teacher_~",
                table: "academic_assignment",
                columns: new[] { "institution_id", "class_id", "unit_id", "teacher_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_assignment_institution_id_client_id",
                table: "academic_assignment",
                columns: new[] { "institution_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_assignment_institution_id_course_id_study_year_acad~",
                table: "academic_assignment",
                columns: new[] { "institution_id", "course_id", "study_year", "academic_year", "class_id" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_assignment_institution_id_course_id_unit_id",
                table: "academic_assignment",
                columns: new[] { "institution_id", "course_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_assignment_institution_id_teacher_id",
                table: "academic_assignment",
                columns: new[] { "institution_id", "teacher_id" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_enrolment_institution_id_class_id_student_id",
                table: "academic_enrolment",
                columns: new[] { "institution_id", "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_enrolment_institution_id_client_id",
                table: "academic_enrolment",
                columns: new[] { "institution_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_enrolment_institution_id_course_id_academic_year_ac~",
                table: "academic_enrolment",
                columns: new[] { "institution_id", "course_id", "academic_year", "active_student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_enrolment_institution_id_course_id_study_year_acade~",
                table: "academic_enrolment",
                columns: new[] { "institution_id", "course_id", "study_year", "academic_year", "class_id" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_enrolment_institution_id_student_id",
                table: "academic_enrolment",
                columns: new[] { "institution_id", "student_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_assignment");

            migrationBuilder.DropTable(
                name: "academic_enrolment");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_institution_member_institution_id_user_id",
                table: "institution_member");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_academic_class_institution_id_course_id_study_year_academic_~",
                table: "academic_class");
        }
    }
}
