using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261031120000_StorageTemperatureReadings")]
    public class StorageTemperatureReadings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageTemperatureReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryLocationId = table.Column<long>(type: "bigint", nullable: false),
                    ReadingC = table.Column<decimal>(type: "decimal(18,1)", precision: 18, scale: 1, nullable: false),
                    ReadingUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RangeMinC = table.Column<decimal>(type: "decimal(18,1)", precision: 18, scale: 1, nullable: true),
                    RangeMaxC = table.Column<decimal>(type: "decimal(18,1)", precision: 18, scale: 1, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeviationId = table.Column<long>(type: "bigint", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageTemperatureReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageTemperatureReadings_InventoryLocations_InventoryLocationId",
                        column: x => x.InventoryLocationId,
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageTemperatureReadings_InventoryLocationId_ReadingUtc",
                table: "StorageTemperatureReadings",
                columns: new[] { "InventoryLocationId", "ReadingUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StorageTemperatureReadings");
        }
    }
}
