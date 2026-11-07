using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Each test definition can carry the LOINC code the EHR receives with its results.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261121000000_TestDefinitionLoinc")]
    public class TestDefinitionLoinc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "LoincCode", table: "TestDefinitions", type: "nvarchar(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LoincCode", table: "TestDefinitions");
        }
    }
}
