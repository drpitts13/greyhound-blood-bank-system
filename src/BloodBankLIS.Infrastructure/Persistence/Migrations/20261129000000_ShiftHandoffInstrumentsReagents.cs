using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts open instrument failures (and held results) and failed reagent lots (and results to review).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261129000000_ShiftHandoffInstrumentsReagents")]
    public class ShiftHandoffInstrumentsReagents : Migration
    {
        private static readonly string[] Columns = ["InstrumentFailuresOpen", "InstrumentResultsHeld", "ReagentLotsFailedQc", "ReagentResultsToReview"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.AddColumn<int>(name: column, table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.DropColumn(name: column, table: "ShiftHandoffs");
            }
        }
    }
}
