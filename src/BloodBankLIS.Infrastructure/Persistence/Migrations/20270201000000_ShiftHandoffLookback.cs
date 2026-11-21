using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 116: lookback notifications due on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270201000000_ShiftHandoffLookback")]
    public class ShiftHandoffLookback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "LookbackDue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "LookbackPastNotifyBy", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LookbackDue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "LookbackPastNotifyBy", table: "ShiftHandoffs");
        }
    }
}
