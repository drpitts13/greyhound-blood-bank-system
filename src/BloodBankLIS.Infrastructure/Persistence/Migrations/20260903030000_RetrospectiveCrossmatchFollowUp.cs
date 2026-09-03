using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903030000_RetrospectiveCrossmatchFollowUp")]
    public partial class RetrospectiveCrossmatchFollowUp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetrospectiveCrossmatchDueUtc",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetrospectiveCrossmatchCompletedUtc",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RetrospectiveCrossmatchId",
                table: "Issues",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TestsIncompleteAtIssue_RetrospectiveCrossmatchCompletedUtc",
                table: "Issues",
                columns: new[] { "TestsIncompleteAtIssue", "RetrospectiveCrossmatchCompletedUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Issues_TestsIncompleteAtIssue_RetrospectiveCrossmatchCompletedUtc",
                table: "Issues");
            migrationBuilder.DropColumn(name: "RetrospectiveCrossmatchDueUtc", table: "Issues");
            migrationBuilder.DropColumn(name: "RetrospectiveCrossmatchCompletedUtc", table: "Issues");
            migrationBuilder.DropColumn(name: "RetrospectiveCrossmatchId", table: "Issues");
        }
    }
}
