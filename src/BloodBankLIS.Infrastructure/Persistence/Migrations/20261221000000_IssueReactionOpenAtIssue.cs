using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>An issue records the patient's reaction investigation open at the time (Cycle 634).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261221000000_IssueReactionOpenAtIssue")]
    public class IssueReactionOpenAtIssue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "ReactionInvestigationOpenAtIssueId", table: "Issues", type: "bigint", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReactionInvestigationOpenAtIssueId", table: "Issues");
        }
    }
}
