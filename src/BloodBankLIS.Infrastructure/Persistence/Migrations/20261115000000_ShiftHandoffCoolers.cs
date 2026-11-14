using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Each shift handoff also counts coolers still in transit, and of those how many were never checked before they
    /// left and how many were checked with differences.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261115000000_ShiftHandoffCoolers")]
    public class ShiftHandoffCoolers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "CoolersInTransit", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "CoolersNotChecked", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "CoolersWithDifferences", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CoolersInTransit", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "CoolersNotChecked", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "CoolersWithDifferences", table: "ShiftHandoffs");
        }
    }
}
