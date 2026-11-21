using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Units shipped to another facility: where, when, and the outbound shipment reference.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261211000000_ShipOut")]
    public class ShipOut : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ShippedToFacility", table: "BloodProducts", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ShippedUtc", table: "BloodProducts", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "OutboundShipmentReference", table: "BloodProducts", type: "nvarchar(100)", maxLength: 100, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OutboundShipmentReference", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ShippedUtc", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ShippedToFacility", table: "BloodProducts");
        }
    }
}
