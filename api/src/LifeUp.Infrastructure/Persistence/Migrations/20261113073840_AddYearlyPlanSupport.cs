using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeUp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddYearlyPlanSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "month_of_year",
                table: "auto_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE entry_categories SET system_key = 'bonus' WHERE kind = 'Income' AND name = '奖金' AND system_key IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "month_of_year",
                table: "auto_plans");

            migrationBuilder.Sql(
                "UPDATE entry_categories SET system_key = NULL WHERE kind = 'Income' AND name = '奖金' AND system_key = 'bonus';");
        }
    }
}
