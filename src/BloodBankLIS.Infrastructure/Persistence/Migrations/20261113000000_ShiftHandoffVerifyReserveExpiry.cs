using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Each shift handoff also counts results waiting for verification, reservations past their end time, and units
    /// near expiry (and how many of those are reserved).
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261113000000_ShiftHandoffVerifyReserveExpiry")]
    public class ShiftHandoffVerifyReserveExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ResultsAwaitingVerification", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ReservationsLapsed", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "NearExpiryUnits", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "NearExpiryReserved", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ResultsAwaitingVerification", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "ReservationsLapsed", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "NearExpiryUnits", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "NearExpiryReserved", table: "ShiftHandoffs");
        }
    }
}
