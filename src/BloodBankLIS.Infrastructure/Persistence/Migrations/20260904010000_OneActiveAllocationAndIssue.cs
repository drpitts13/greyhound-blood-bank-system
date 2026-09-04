using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260904010000_OneActiveAllocationAndIssue")]
    public partial class OneActiveAllocationAndIssue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Allocations_OneReservedPerUnit",
                table: "Allocations",
                column: "BloodProductId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OneOpenIssuePerUnit",
                table: "Issues",
                column: "BloodProductId",
                unique: true,
                filter: "[Status] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Allocations_OneReservedPerUnit",
                table: "Allocations");

            migrationBuilder.DropIndex(
                name: "IX_Issues_OneOpenIssuePerUnit",
                table: "Issues");
        }
    }
}
