using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Pending results that must be verified before their specimen expires, counted on each shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261105000000_ShiftHandoffVerifyBeforeExpiry")]
    public class ShiftHandoffVerifyBeforeExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "PendingTestsVerifyBeforeExpiry", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PendingTestsVerifyBeforeExpiry", table: "ShiftHandoffs");
        }
    }
}
