using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts recalled units not yet pulled (Cycle 689).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270105000000_ShiftHandoffRecalledToPull")]
    public class ShiftHandoffRecalledToPull : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RecalledToPull", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RecalledToPull", table: "ShiftHandoffs");
        }
    }
}
