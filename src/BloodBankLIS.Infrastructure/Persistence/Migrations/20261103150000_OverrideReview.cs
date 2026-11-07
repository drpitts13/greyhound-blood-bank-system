using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Second-person review of overrides after the fact.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103150000_OverrideReview")]
    public class OverrideReview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReviewOutcome", table: "Overrides", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReviewedBy", table: "Overrides", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ReviewedUtc", table: "Overrides", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReviewNote", table: "Overrides", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Overrides_ReviewedUtc", table: "Overrides", column: "ReviewedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Overrides_ReviewedUtc", table: "Overrides");
            migrationBuilder.DropColumn(name: "ReviewOutcome", table: "Overrides");
            migrationBuilder.DropColumn(name: "ReviewedBy", table: "Overrides");
            migrationBuilder.DropColumn(name: "ReviewedUtc", table: "Overrides");
            migrationBuilder.DropColumn(name: "ReviewNote", table: "Overrides");
        }
    }
}
