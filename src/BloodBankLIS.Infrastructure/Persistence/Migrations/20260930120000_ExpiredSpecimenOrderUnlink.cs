using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260930120000_ExpiredSpecimenOrderUnlink")]
    public class ExpiredSpecimenOrderUnlink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UnlinkedUtc",
                table: "OrderSpecimens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlinkReason",
                table: "OrderSpecimens",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UnlinkReason", table: "OrderSpecimens");
            migrationBuilder.DropColumn(name: "UnlinkedUtc", table: "OrderSpecimens");
        }
    }
}
