using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Each shift handoff also counts quality deviations not yet closed, and the critical ones.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261118000000_ShiftHandoffDeviations")]
    public class ShiftHandoffDeviations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "OpenDeviations", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "CriticalDeviations", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OpenDeviations", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "CriticalDeviations", table: "ShiftHandoffs");
        }
    }
}
