using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260914200000_ModificationRuleCodeReuse")]
    public partial class ModificationRuleCodeReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModificationRules_ModificationCode",
                table: "ModificationRules");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_ModificationCode_SourceProductTypeId_TargetProductTypeId",
                table: "ModificationRules",
                columns: new[] { "ModificationCode", "SourceProductTypeId", "TargetProductTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModificationRules_ModificationCode_SourceProductTypeId_TargetProductTypeId",
                table: "ModificationRules");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_ModificationCode",
                table: "ModificationRules",
                column: "ModificationCode",
                unique: true);
        }
    }
}
