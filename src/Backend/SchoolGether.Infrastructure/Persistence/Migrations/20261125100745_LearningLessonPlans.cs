using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningLessonPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_lesson_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    institution_id = table.Column<int>(type: "int", nullable: false),
                    teacher_id = table.Column<int>(type: "int", nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    class_id = table.Column<int>(type: "int", nullable: false),
                    unit_id = table.Column<int>(type: "int", nullable: false),
                    academic_year = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    objectives = table.Column<string>(type: "text", maxLength: 4000, nullable: true),
                    content = table.Column<string>(type: "text", maxLength: 8000, nullable: true),
                    resources = table.Column<string>(type: "text", maxLength: 4000, nullable: true),
                    create_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    is_archived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_lesson_plan", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_lesson_plan_institution_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_learning_lesson_plan_institution_id_teacher_id_academic_year",
                table: "learning_lesson_plan",
                columns: new[] { "institution_id", "teacher_id", "academic_year" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_lesson_plan_institution_id_teacher_id_client_id",
                table: "learning_lesson_plan",
                columns: new[] { "institution_id", "teacher_id", "client_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_lesson_plan");
        }
    }
}
