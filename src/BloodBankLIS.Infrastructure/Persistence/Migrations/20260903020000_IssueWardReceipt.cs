using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903020000_IssueWardReceipt")]
    public partial class IssueWardReceipt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WardReceivedUtc",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WardReceivedBy",
                table: "Issues",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WardVisualAcceptable",
                table: "Issues",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WardReceivedUtc", table: "Issues");
            migrationBuilder.DropColumn(name: "WardReceivedBy", table: "Issues");
            migrationBuilder.DropColumn(name: "WardVisualAcceptable", table: "Issues");
        }
    }
}
