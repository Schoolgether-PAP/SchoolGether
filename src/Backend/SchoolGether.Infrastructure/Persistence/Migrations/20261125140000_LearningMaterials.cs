using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_material",
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
                    content = table.Column<string>(type: "text", maxLength: 8000, nullable: true),
                    url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    create_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    is_archived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_material", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_material_institution_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_learning_material_context",
                table: "learning_material",
                columns: new[] { "institution_id", "academic_year", "course_id", "class_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_material_institution_id_teacher_id_client_id",
                table: "learning_material",
                columns: new[] { "institution_id", "teacher_id", "client_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_material");
        }
    }
}
