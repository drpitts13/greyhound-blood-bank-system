using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A supplier order line can ask for an Rh type, special requirements (for example irradiated, CMV-negative,
    /// K-negative), and name the patient it is for (end-user testing pass 15: a patient-specific unit could not be
    /// ordered).
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270113000000_SupplierOrderSpecialLines")]
    public class SupplierOrderSpecialLines : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RhD", table: "SupplierOrderLines", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Requirements", table: "SupplierOrderLines", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<long>(name: "PatientId", table: "SupplierOrderLines", type: "bigint", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RhD", table: "SupplierOrderLines");
            migrationBuilder.DropColumn(name: "Requirements", table: "SupplierOrderLines");
            migrationBuilder.DropColumn(name: "PatientId", table: "SupplierOrderLines");
        }
    }
}
