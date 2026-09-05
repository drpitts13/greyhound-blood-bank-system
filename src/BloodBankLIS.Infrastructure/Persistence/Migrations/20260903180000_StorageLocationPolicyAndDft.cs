using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903180000_StorageLocationPolicyAndDft")]
    public partial class StorageLocationPolicyAndDft : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Department", table: "InventoryLocations", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsIssue", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsRemoteIssue", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "AllowsElectronicIssue", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "RequiresSecondVerifier", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsSatellite", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "AllowsRbc", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsPlasma", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsPlatelets", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsCryo", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "AllowsWholeBlood", table: "InventoryLocations", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<decimal>(name: "StorageTempMinC", table: "InventoryLocations", type: "decimal(18,1)", precision: 18, scale: 1, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "StorageTempMaxC", table: "InventoryLocations", type: "decimal(18,1)", precision: 18, scale: 1, nullable: true);
            migrationBuilder.AddColumn<int>(name: "DefaultInTransitHours", table: "InventoryLocations", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Notes", table: "InventoryLocations", type: "nvarchar(500)", maxLength: 500, nullable: true);

            migrationBuilder.AddColumn<string>(name: "RevenueCode", table: "ChargeCodes", type: "nvarchar(4)", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Modifier", table: "ChargeCodes", type: "nvarchar(2)", maxLength: 2, nullable: true);

            migrationBuilder.AddColumn<string>(name: "ProcedureCode", table: "BillingEvents", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "RevenueCode", table: "BillingEvents", type: "nvarchar(4)", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Modifier", table: "BillingEvents", type: "nvarchar(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Description", table: "BillingEvents", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PerformingLocationCode", table: "BillingEvents", type: "nvarchar(50)", maxLength: 50, nullable: true);

            migrationBuilder.Sql("""
                UPDATE InventoryLocations
                SET AllowsPlasma = 0, AllowsPlatelets = 0, AllowsCryo = 0, StorageTempMinC = 1, StorageTempMaxC = 6
                WHERE Code = 'FRIDGE-1';
                UPDATE InventoryLocations
                SET AllowsRbc = 0, AllowsWholeBlood = 0, AllowsPlatelets = 0, StorageTempMinC = -30, StorageTempMaxC = -18
                WHERE Code = 'FREEZER-1';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Department", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsIssue", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsRemoteIssue", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsElectronicIssue", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "RequiresSecondVerifier", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "IsSatellite", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsRbc", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsPlasma", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsPlatelets", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsCryo", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "AllowsWholeBlood", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "StorageTempMinC", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "StorageTempMaxC", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "DefaultInTransitHours", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "Notes", table: "InventoryLocations");
            migrationBuilder.DropColumn(name: "RevenueCode", table: "ChargeCodes");
            migrationBuilder.DropColumn(name: "Modifier", table: "ChargeCodes");
            migrationBuilder.DropColumn(name: "ProcedureCode", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "RevenueCode", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "Modifier", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "Description", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "PerformingLocationCode", table: "BillingEvents");
        }
    }
}
