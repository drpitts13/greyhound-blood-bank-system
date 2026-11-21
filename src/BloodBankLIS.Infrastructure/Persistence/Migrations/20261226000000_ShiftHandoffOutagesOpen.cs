using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts recorded outages not yet ended (Cycle 663).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261226000000_ShiftHandoffOutagesOpen")]
    public class ShiftHandoffOutagesOpen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "OutagesOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OutagesOpen", table: "ShiftHandoffs");
        }
    }
}
