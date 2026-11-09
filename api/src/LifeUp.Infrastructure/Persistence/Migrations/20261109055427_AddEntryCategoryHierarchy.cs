using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeUp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryCategoryHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entry_categories_ledger_id_kind_name",
                table: "entry_categories");

            migrationBuilder.AddColumn<bool>(
                name: "is_group",
                table: "entry_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "entry_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_ledger_id_kind_is_group_name",
                table: "entry_categories",
                columns: new[] { "ledger_id", "kind", "is_group", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_parent_id",
                table: "entry_categories",
                column: "parent_id");

            migrationBuilder.Sql(
                """
                INSERT INTO entry_categories
                    (id, ledger_id, kind, name, sort_order, is_active, created_at, updated_at, version, is_group)
                SELECT gen_random_uuid(), ledger.id, defaults.kind, defaults.name, defaults.sort_order,
                    TRUE, now(), now(), 0, TRUE
                FROM ledgers AS ledger
                CROSS JOIN (VALUES
                    ('Expense', '固定', 0), ('Expense', '必要', 1),
                    ('Expense', '弹性', 2), ('Expense', '其他', 3),
                    ('Income', '经常收入', 0), ('Income', '其他收入', 1)
                ) AS defaults(kind, name, sort_order);

                INSERT INTO entry_categories
                    (id, ledger_id, kind, name, sort_order, is_active, created_at, updated_at, version, is_group)
                SELECT gen_random_uuid(), entry.ledger_id, entry.kind, entry.category, 100,
                    FALSE, now(), now(), 0, FALSE
                FROM (SELECT DISTINCT ledger_id, kind, category FROM ledger_entries) AS entry
                WHERE NOT EXISTS (
                    SELECT 1 FROM entry_categories AS category
                    WHERE category.ledger_id = entry.ledger_id AND category.kind = entry.kind
                        AND category.name = entry.category AND NOT category.is_group
                );

                UPDATE entry_categories AS child
                SET parent_id = parent.id
                FROM entry_categories AS parent
                WHERE NOT child.is_group AND parent.is_group
                    AND child.ledger_id = parent.ledger_id AND child.kind = parent.kind
                    AND parent.name = CASE
                        WHEN child.kind = 'Income' AND (child.system_key = 'salary' OR child.name = '工资') THEN '经常收入'
                        WHEN child.kind = 'Income' THEN '其他收入'
                        WHEN child.name IN ('房租', '订阅') THEN '固定'
                        WHEN child.name IN ('餐饮', '日用', '交通', '居家', '服饰', '通讯', '医疗') THEN '必要'
                        WHEN child.name IN ('娱乐', '旅行') THEN '弹性'
                        ELSE '其他'
                    END;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_entry_categories_entry_categories_parent_id",
                table: "entry_categories",
                column: "parent_id",
                principalTable: "entry_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_entry_categories_entry_categories_parent_id",
                table: "entry_categories");

            migrationBuilder.DropIndex(
                name: "IX_entry_categories_ledger_id_kind_is_group_name",
                table: "entry_categories");

            migrationBuilder.DropIndex(
                name: "IX_entry_categories_parent_id",
                table: "entry_categories");

            migrationBuilder.Sql("DELETE FROM entry_categories WHERE is_group;");

            migrationBuilder.DropColumn(
                name: "is_group",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "entry_categories");

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_ledger_id_kind_name",
                table: "entry_categories",
                columns: new[] { "ledger_id", "kind", "name" },
                unique: true);
        }
    }
}
