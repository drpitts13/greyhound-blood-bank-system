using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A reflex order line records the verified result that added it (test, value, result id).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261203000000_OrderLineReflexSource")]
    public class OrderLineReflexSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ReflexFromTestCode", table: "OrderLines", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReflexFromValue", table: "OrderLines", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<long>(name: "ReflexFromResultId", table: "OrderLines", type: "bigint", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReflexFromResultId", table: "OrderLines");
            migrationBuilder.DropColumn(name: "ReflexFromValue", table: "OrderLines");
            migrationBuilder.DropColumn(name: "ReflexFromTestCode", table: "OrderLines");
        }
    }
}
