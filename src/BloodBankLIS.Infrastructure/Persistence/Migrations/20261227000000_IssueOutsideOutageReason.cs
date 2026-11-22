using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-081: why a downtime back-entry's paper time falls outside every recorded outage (Cycle 665).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261227000000_IssueOutsideOutageReason")]
    public class IssueOutsideOutageReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "OutsideOutageReason", table: "Issues", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OutsideOutageReason", table: "Issues");
        }
    }
}
