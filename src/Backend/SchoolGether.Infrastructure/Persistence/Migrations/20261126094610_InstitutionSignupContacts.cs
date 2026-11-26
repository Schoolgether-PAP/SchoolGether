using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstitutionSignupContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "institution_profile",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "institution_profile",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_role",
                table: "institution_profile",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expected_teachers",
                table: "institution_profile",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "institution_profile",
                type: "varchar(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "institution_profile",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_number",
                table: "institution_profile",
                type: "varchar(9)",
                maxLength: 9,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "contact_role",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "expected_teachers",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "institution_profile");

            migrationBuilder.DropColumn(
                name: "tax_number",
                table: "institution_profile");
        }
    }
}
