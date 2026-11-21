using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Equipment names the tests it is used for (end-user testing pass 74).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270120000000_EquipmentTestCodes")]
    public class EquipmentTestCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "TestCodes", table: "Equipment", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TestCodes", table: "Equipment");
        }
    }
}
