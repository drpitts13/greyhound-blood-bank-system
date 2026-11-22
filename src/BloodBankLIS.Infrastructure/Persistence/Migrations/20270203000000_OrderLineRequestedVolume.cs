using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 118: the volume asked for on a product order line.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270203000000_OrderLineRequestedVolume")]
    public class OrderLineRequestedVolume : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RequestedVolumeMl", table: "OrderLines", type: "int", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RequestedVolumeMl", table: "OrderLines");
        }
    }
}
