using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903120000_DonationRestriction")]
    public partial class DonationRestriction : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonationRestriction",
                table: "BloodProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ReservedPatientId",
                table: "BloodProducts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodProducts_ReservedPatientId",
                table: "BloodProducts",
                column: "ReservedPatientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_BloodProducts_ReservedPatientId", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ReservedPatientId", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "DonationRestriction", table: "BloodProducts");
        }
    }
}
