using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Special requirement tables (unit typing, unit modification, informational, issue procedure), the unit category
    /// each applies to, and the typing details (allowed ABO groups, fixed antigen, unit test code).
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261112000000_SpecialRequirementCategories")]
    public class SpecialRequirementCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Category", table: "SpecialRequirementDefinitions", type: "int", nullable: false, defaultValue: -1);
            migrationBuilder.AddColumn<int>(name: "ComponentClass", table: "SpecialRequirementDefinitions", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AllowedAboGroups", table: "SpecialRequirementDefinitions", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "AntigenCode", table: "SpecialRequirementDefinitions", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "UnitTestCode", table: "SpecialRequirementDefinitions", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.Sql(SpecialRequirementCategoryBackfill.Sql("Category = -1"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE SpecialRequirementDefinitions SET EnforcementKind = 0 WHERE EnforcementKind = 7");
            migrationBuilder.DropColumn(name: "Category", table: "SpecialRequirementDefinitions");
            migrationBuilder.DropColumn(name: "ComponentClass", table: "SpecialRequirementDefinitions");
            migrationBuilder.DropColumn(name: "AllowedAboGroups", table: "SpecialRequirementDefinitions");
            migrationBuilder.DropColumn(name: "AntigenCode", table: "SpecialRequirementDefinitions");
            migrationBuilder.DropColumn(name: "UnitTestCode", table: "SpecialRequirementDefinitions");
        }
    }
}
