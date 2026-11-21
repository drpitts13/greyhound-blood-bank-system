using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A modification records the device it was performed on and, for irradiation, the dose indicator check.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261201000000_ModificationDevice")]
    public class ModificationDevice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "DeviceId", table: "UnitModifications", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IndicatorConfirmed", table: "UnitModifications", type: "bit", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IndicatorConfirmed", table: "UnitModifications");
            migrationBuilder.DropColumn(name: "DeviceId", table: "UnitModifications");
        }
    }
}
