using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts blood orders still to prepare and those past their needed-by time.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261216000000_ShiftHandoffBloodOrders")]
    public class ShiftHandoffBloodOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "BloodOrdersToPrepare", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "BloodOrdersPastNeededBy", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BloodOrdersPastNeededBy", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "BloodOrdersToPrepare", table: "ShiftHandoffs");
        }
    }
}
