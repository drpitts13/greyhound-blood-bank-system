using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts reaction investigations whose patient still has units out or reserved.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261220000000_ShiftHandoffReactionsWithUnitsOut")]
    public class ShiftHandoffReactionsWithUnitsOut : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReactionsWithUnitsOut", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReactionsWithUnitsOut", table: "ShiftHandoffs");
        }
    }
}
