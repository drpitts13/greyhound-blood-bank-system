using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts RhIG evaluations still needing work and those to give.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261207000000_ShiftHandoffRhig")]
    public class ShiftHandoffRhig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RhigOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "RhigToGive", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RhigToGive", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "RhigOpen", table: "ShiftHandoffs");
        }
    }
}
