using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The ward's request to issue a reserved unit: when, by whom, and where to.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261117000000_AllocationIssueRequest")]
    public class AllocationIssueRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "IssueRequestedUtc", table: "Allocations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "IssueRequestedBy", table: "Allocations", type: "nvarchar(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<string>(name: "IssueRequestedTo", table: "Allocations", type: "nvarchar(128)", maxLength: 128, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IssueRequestedUtc", table: "Allocations");
            migrationBuilder.DropColumn(name: "IssueRequestedBy", table: "Allocations");
            migrationBuilder.DropColumn(name: "IssueRequestedTo", table: "Allocations");
        }
    }
}
