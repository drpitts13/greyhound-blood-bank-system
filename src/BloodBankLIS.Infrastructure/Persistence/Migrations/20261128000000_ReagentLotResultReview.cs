using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A result tested with a lot that failed QC can be marked reviewed, with who, when, and what was found or done.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261128000000_ReagentLotResultReview")]
    public class ReagentLotResultReview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "ReviewedUtc", table: "TestResultReagentLots", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReviewedBy", table: "TestResultReagentLots", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReviewNote", table: "TestResultReagentLots", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReviewNote", table: "TestResultReagentLots");
            migrationBuilder.DropColumn(name: "ReviewedBy", table: "TestResultReagentLots");
            migrationBuilder.DropColumn(name: "ReviewedUtc", table: "TestResultReagentLots");
        }
    }
}
