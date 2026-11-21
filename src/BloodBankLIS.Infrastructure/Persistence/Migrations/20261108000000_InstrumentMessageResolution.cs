using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>When, by whom, and why a failed instrument message was closed (by a replay that applied, or by hand).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261108000000_InstrumentMessageResolution")]
    public class InstrumentMessageResolution : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "ResolvedUtc", table: "InstrumentMessages", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ResolvedBy", table: "InstrumentMessages", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ResolutionNote", table: "InstrumentMessages", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ResolvedUtc", table: "InstrumentMessages");
            migrationBuilder.DropColumn(name: "ResolvedBy", table: "InstrumentMessages");
            migrationBuilder.DropColumn(name: "ResolutionNote", table: "InstrumentMessages");
        }
    }
}
