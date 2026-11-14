using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A submitted supplier order records the follow-up with the supplier and its promised arrival (end-user testing pass 45).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270116000000_SupplierOrderFollowUp")]
    public class SupplierOrderFollowUp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "PromisedArrivalUtc", table: "SupplierOrders", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "FollowUpNote", table: "SupplierOrders", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "FollowUpBy", table: "SupplierOrders", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "FollowUpUtc", table: "SupplierOrders", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PromisedArrivalUtc", table: "SupplierOrders");
            migrationBuilder.DropColumn(name: "FollowUpNote", table: "SupplierOrders");
            migrationBuilder.DropColumn(name: "FollowUpBy", table: "SupplierOrders");
            migrationBuilder.DropColumn(name: "FollowUpUtc", table: "SupplierOrders");
        }
    }
}
