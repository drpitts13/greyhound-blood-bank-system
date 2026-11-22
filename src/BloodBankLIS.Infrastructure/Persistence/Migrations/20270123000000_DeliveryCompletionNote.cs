using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 83: why a delivery was completed short of or over its packing list.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270123000000_DeliveryCompletionNote")]
    public class DeliveryCompletionNote : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "CompletionNote", table: "BloodDeliveries", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CompletionNote", table: "BloodDeliveries");
        }
    }
}
