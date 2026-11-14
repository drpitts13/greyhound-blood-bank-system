using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>When and why a stored tube was discarded; the discard is also a row in the tube's storage history.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261209000000_SpecimenDiscard")]
    public class SpecimenDiscard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "DiscardedUtc", table: "Specimens", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DiscardedBy", table: "Specimens", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "DiscardReason", table: "Specimens", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "Discarded", table: "SpecimenStorageEvents", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Discarded", table: "SpecimenStorageEvents");
            migrationBuilder.DropColumn(name: "DiscardReason", table: "Specimens");
            migrationBuilder.DropColumn(name: "DiscardedBy", table: "Specimens");
            migrationBuilder.DropColumn(name: "DiscardedUtc", table: "Specimens");
        }
    }
}
