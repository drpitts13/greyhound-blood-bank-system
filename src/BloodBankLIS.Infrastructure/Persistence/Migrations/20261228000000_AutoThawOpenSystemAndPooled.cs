using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-068: open-system and pooled thawed shelf lives per code, and what each thaw applied (Cycle 667).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261228000000_AutoThawOpenSystemAndPooled")]
    public class AutoThawOpenSystemAndPooled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "OpenSystemShelfLifeHours", table: "AutoThawProducts", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PooledShelfLifeHours", table: "AutoThawProducts", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "OpenSystem", table: "AutoThawRequests", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Pooled", table: "AutoThawRequests", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "ShelfLifeHoursApplied", table: "AutoThawRequests", type: "int", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OpenSystemShelfLifeHours", table: "AutoThawProducts");
            migrationBuilder.DropColumn(name: "PooledShelfLifeHours", table: "AutoThawProducts");
            migrationBuilder.DropColumn(name: "OpenSystem", table: "AutoThawRequests");
            migrationBuilder.DropColumn(name: "Pooled", table: "AutoThawRequests");
            migrationBuilder.DropColumn(name: "ShelfLifeHoursApplied", table: "AutoThawRequests");
        }
    }
}
