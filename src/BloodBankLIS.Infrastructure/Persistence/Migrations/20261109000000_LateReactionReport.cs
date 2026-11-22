using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A reaction reported after the transfusion was documented without one, and when its signs began.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261109000000_LateReactionReport")]
    public class LateReactionReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "ReportedAfterTransfusion", table: "ReactionInvestigations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "OnsetUtc", table: "ReactionInvestigations", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReportedAfterTransfusion", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "OnsetUtc", table: "ReactionInvestigations");
        }
    }
}
