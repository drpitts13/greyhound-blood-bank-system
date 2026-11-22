using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts equipment checks to do and equipment out of service (Cycle 710).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270109000000_ShiftHandoffEquipment")]
    public class ShiftHandoffEquipment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "EquipmentChecksToDo", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "EquipmentChecksFailedOrOverdue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "EquipmentOutOfService", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EquipmentChecksToDo", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "EquipmentChecksFailedOrOverdue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "EquipmentOutOfService", table: "ShiftHandoffs");
        }
    }
}
