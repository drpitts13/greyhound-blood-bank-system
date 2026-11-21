using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Who reopened a signed reaction investigation, when, and why (the latest reopen; earlier ones are audited).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103120000_ReactionReopen")]
    public class ReactionReopen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "ReopenedUtc", table: "ReactionInvestigations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReopenedBy", table: "ReactionInvestigations", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReopenReason", table: "ReactionInvestigations", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReopenedUtc", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "ReopenedBy", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "ReopenReason", table: "ReactionInvestigations");
        }
    }
}
