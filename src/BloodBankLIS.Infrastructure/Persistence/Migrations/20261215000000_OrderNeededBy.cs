using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>When an order is needed by (surgery start, scheduled transfusion).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261215000000_OrderNeededBy")]
    public class OrderNeededBy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "NeededByUtc", table: "Orders", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NeededByUtc", table: "Orders");
        }
    }
}
