using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 117: the physician's signed statement of urgency for an emergency release.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270202000000_IssueUrgencyStatement")]
    public class IssueUrgencyStatement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "UrgencyStatementSignedBy", table: "Issues", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "UrgencyStatementReceivedUtc", table: "Issues", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "UrgencyStatementRecordedBy", table: "Issues", type: "nvarchar(100)", maxLength: 100, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UrgencyStatementSignedBy", table: "Issues");
            migrationBuilder.DropColumn(name: "UrgencyStatementReceivedUtc", table: "Issues");
            migrationBuilder.DropColumn(name: "UrgencyStatementRecordedBy", table: "Issues");
        }
    }
}
