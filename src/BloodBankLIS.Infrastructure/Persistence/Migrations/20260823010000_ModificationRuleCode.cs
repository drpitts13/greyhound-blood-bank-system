using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260823010000_ModificationRuleCode")]
    public partial class ModificationRuleCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModificationCode",
                table: "ModificationRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.ModificationCode = LEFT(CONCAT(
                    CASE r.ModificationType
                        WHEN 0 THEN 'DIV'
                        WHEN 1 THEN 'POOL'
                        WHEN 2 THEN 'IRR'
                        WHEN 3 THEN 'THAW'
                        WHEN 4 THEN 'VR'
                        WHEN 5 THEN 'LR'
                        WHEN 6 THEN 'WASH'
                        ELSE 'MOD'
                    END, '-', p.ProductCode), 20)
                FROM ModificationRules r
                INNER JOIN ProductTypes p ON p.Id = r.SourceProductTypeId
                WHERE r.ModificationCode = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.ModificationCode = LEFT(CONCAT(r.ModificationCode, '-', r.Id), 20)
                FROM ModificationRules r
                INNER JOIN (
                    SELECT ModificationCode
                    FROM ModificationRules
                    GROUP BY ModificationCode
                    HAVING COUNT(*) > 1
                ) d ON d.ModificationCode = r.ModificationCode;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_ModificationCode",
                table: "ModificationRules",
                column: "ModificationCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModificationRules_ModificationCode",
                table: "ModificationRules");

            migrationBuilder.DropColumn(
                name: "ModificationCode",
                table: "ModificationRules");
        }
    }
}
