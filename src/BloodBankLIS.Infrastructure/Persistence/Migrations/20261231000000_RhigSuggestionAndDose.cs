using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-069: the suggested decision and calculated vials kept on a completed RhIG evaluation (Cycle 671).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261231000000_RhigSuggestionAndDose")]
    public class RhigSuggestionAndDose : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "SuggestedDecision", table: "RhigEvaluations", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "VialsCalculated", table: "RhigEvaluations", type: "int", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SuggestedDecision", table: "RhigEvaluations");
            migrationBuilder.DropColumn(name: "VialsCalculated", table: "RhigEvaluations");
        }
    }
}
