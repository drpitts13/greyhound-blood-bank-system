using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>An order line keeps the test grouper it was ordered as part of, for the EHR procedure code.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261123000000_OrderLineGrouperCode")]
    public class OrderLineGrouperCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "GrouperCode", table: "OrderLines", type: "nvarchar(50)", maxLength: 50, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GrouperCode", table: "OrderLines");
        }
    }
}
