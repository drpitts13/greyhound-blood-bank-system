using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 86: a reaction's imputability and its hemovigilance report.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270125000000_ReactionImputability")]
    public class ReactionImputability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Imputability", table: "ReactionInvestigations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateOnly>(name: "HemovigilanceReportedOn", table: "ReactionInvestigations", type: "date", nullable: true);
            migrationBuilder.AddColumn<string>(name: "HemovigilanceReference", table: "ReactionInvestigations", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Imputability", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "HemovigilanceReportedOn", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "HemovigilanceReference", table: "ReactionInvestigations");
        }
    }
}
