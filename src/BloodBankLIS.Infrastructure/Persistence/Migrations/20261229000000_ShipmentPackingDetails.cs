using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-072: a shipment to another facility records container, packing temperature, packer and receiving contact (Cycle 668).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261229000000_ShipmentPackingDetails")]
    public class ShipmentPackingDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ShipContainer", table: "BloodProducts", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ShipPackingTemperatureC", table: "BloodProducts", type: "decimal(5,1)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ShipPackedBy", table: "BloodProducts", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ShipReceivingContact", table: "BloodProducts", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShipContainer", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ShipPackingTemperatureC", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ShipPackedBy", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ShipReceivingContact", table: "BloodProducts");
        }
    }
}
