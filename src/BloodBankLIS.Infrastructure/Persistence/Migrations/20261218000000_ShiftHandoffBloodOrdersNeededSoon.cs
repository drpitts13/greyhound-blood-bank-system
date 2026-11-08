using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts blood orders needed soon (within the facility's warning window).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261218000000_ShiftHandoffBloodOrdersNeededSoon")]
    public class ShiftHandoffBloodOrdersNeededSoon : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "BloodOrdersNeededSoon", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BloodOrdersNeededSoon", table: "ShiftHandoffs");
        }
    }
}
