using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeUp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "salary_profile",
                table: "ledgers",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "salary_profile",
                table: "ledgers");
        }
    }
}
