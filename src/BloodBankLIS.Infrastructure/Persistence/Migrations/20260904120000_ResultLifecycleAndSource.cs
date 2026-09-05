using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BloodBankLIS.Infrastructure.Persistence;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260904120000_ResultLifecycleAndSource")]
    public partial class ResultLifecycleAndSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "TestResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "TestResults",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidatedBy",
                table: "TestResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvalidatedUtc",
                table: "TestResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidationReason",
                table: "TestResults",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Source", table: "TestResults");
            migrationBuilder.DropColumn(name: "SourceReference", table: "TestResults");
            migrationBuilder.DropColumn(name: "InvalidatedBy", table: "TestResults");
            migrationBuilder.DropColumn(name: "InvalidatedUtc", table: "TestResults");
            migrationBuilder.DropColumn(name: "InvalidationReason", table: "TestResults");
        }
    }
}
