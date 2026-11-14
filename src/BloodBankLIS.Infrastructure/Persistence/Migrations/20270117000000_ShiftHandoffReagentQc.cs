using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts reagent QC to do today and QC to review (end-user testing pass 70).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270117000000_ShiftHandoffReagentQc")]
    public class ShiftHandoffReagentQc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReagentLotsQcDue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ReagentQcUnreviewed", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReagentLotsQcDue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "ReagentQcUnreviewed", table: "ShiftHandoffs");
        }
    }
}
