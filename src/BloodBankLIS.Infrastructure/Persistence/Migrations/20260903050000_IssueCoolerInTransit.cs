using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903050000_IssueCoolerInTransit")]
    public partial class IssueCoolerInTransit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoolerId",
                table: "Issues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InTransitDueUtc",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Status_WardReceivedUtc",
                table: "Issues",
                columns: new[] { "Status", "WardReceivedUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Issues_Status_WardReceivedUtc",
                table: "Issues");
            migrationBuilder.DropColumn(name: "CoolerId", table: "Issues");
            migrationBuilder.DropColumn(name: "InTransitDueUtc", table: "Issues");
        }
    }
}
