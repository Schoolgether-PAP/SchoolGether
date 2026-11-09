using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolGether.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnnualTeacherPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "monthly_price",
                table: "demonstration_subscription",
                newName: "price");

            migrationBuilder.AddColumn<string>(
                name: "billing_period",
                table: "demonstration_subscription",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<string>(
                name: "capacity_unit",
                table: "demonstration_subscription",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "billing_period",
                table: "demonstration_subscription");

            migrationBuilder.DropColumn(
                name: "capacity_unit",
                table: "demonstration_subscription");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "demonstration_subscription",
                newName: "monthly_price");
        }
    }
}
