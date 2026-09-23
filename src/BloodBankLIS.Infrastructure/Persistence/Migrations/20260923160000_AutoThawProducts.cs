using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Catalog of product types eligible for in-place auto-thaw, and the per-allocation offer.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260923160000_AutoThawProducts")]
    public class AutoThawProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoThawProducts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoThawProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoThawProducts_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutoThawRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllocationId = table.Column<long>(type: "bigint", nullable: false),
                    BloodUnitId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ThawUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderActionId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoThawRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoThawRequests_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutoThawRequests_BloodProducts_BloodUnitId",
                        column: x => x.BloodUnitId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutoThawRequests_OrderActions_OrderActionId",
                        column: x => x.OrderActionId,
                        principalTable: "OrderActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawProducts_ProductTypeId",
                table: "AutoThawProducts",
                column: "ProductTypeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawProducts_IsActive",
                table: "AutoThawProducts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawRequests_AllocationId",
                table: "AutoThawRequests",
                column: "AllocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawRequests_BloodUnitId",
                table: "AutoThawRequests",
                column: "BloodUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawRequests_Status",
                table: "AutoThawRequests",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AutoThawRequests");
            migrationBuilder.DropTable(name: "AutoThawProducts");
        }
    }
}
