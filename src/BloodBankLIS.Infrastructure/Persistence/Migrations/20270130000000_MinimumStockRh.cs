using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 105: a minimum stock level for one Rh(D) type.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270130000000_MinimumStockRh")]
    public class MinimumStockRh : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RhD", table: "StockParLevels", type: "int", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RhD", table: "StockParLevels");
        }
    }
}
