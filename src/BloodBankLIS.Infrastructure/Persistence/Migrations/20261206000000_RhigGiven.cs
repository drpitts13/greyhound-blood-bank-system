using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>An RhIG evaluation records the lot and vials given.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261206000000_RhigGiven")]
    public class RhigGiven : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "GivenLot", table: "RhigEvaluations", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>(name: "GivenVials", table: "RhigEvaluations", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "GivenUtc", table: "RhigEvaluations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GivenBy", table: "RhigEvaluations", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "GivenNote", table: "RhigEvaluations", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GivenNote", table: "RhigEvaluations");
            migrationBuilder.DropColumn(name: "GivenBy", table: "RhigEvaluations");
            migrationBuilder.DropColumn(name: "GivenUtc", table: "RhigEvaluations");
            migrationBuilder.DropColumn(name: "GivenVials", table: "RhigEvaluations");
            migrationBuilder.DropColumn(name: "GivenLot", table: "RhigEvaluations");
        }
    }
}
