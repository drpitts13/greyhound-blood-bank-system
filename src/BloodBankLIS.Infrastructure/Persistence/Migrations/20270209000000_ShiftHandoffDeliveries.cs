using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 144: deliveries with every unit entered, still to complete, on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270209000000_ShiftHandoffDeliveries")]
    public class ShiftHandoffDeliveries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "DeliveriesToComplete", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeliveriesToComplete", table: "ShiftHandoffs");
        }
    }
}
