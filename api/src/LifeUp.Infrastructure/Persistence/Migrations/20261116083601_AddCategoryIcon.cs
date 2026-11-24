using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeUp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "icon_key",
                table: "entry_categories",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                comment: "Noto Emoji 图标标识");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "icon_key",
                table: "entry_categories");
        }
    }
}
