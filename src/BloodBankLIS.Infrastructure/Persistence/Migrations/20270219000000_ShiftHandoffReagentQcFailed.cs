using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts lots whose unreviewed QC includes a failed reading (end-user testing pass 319).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270219000000_ShiftHandoffReagentQcFailed")]
    public class ShiftHandoffReagentQcFailed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReagentQcUnreviewedFailed", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReagentQcUnreviewedFailed", table: "ShiftHandoffs");
        }
    }
}
