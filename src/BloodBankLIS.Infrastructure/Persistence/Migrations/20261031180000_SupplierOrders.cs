using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261031180000_SupplierOrders")]
    public class SupplierOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BloodSupplierId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmitSignatureId = table.Column<long>(type: "bigint", nullable: true),
                    BloodDeliveryId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrders_BloodSuppliers_BloodSupplierId",
                        column: x => x.BloodSupplierId,
                        principalTable: "BloodSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOrderLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Abo = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrderLines_SupplierOrders_SupplierOrderId",
                        column: x => x.SupplierOrderId,
                        principalTable: "SupplierOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierOrderLines_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_SupplierOrders_OrderNumber", table: "SupplierOrders", column: "OrderNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SupplierOrders_BloodSupplierId", table: "SupplierOrders", column: "BloodSupplierId");
            migrationBuilder.CreateIndex(name: "IX_SupplierOrderLines_SupplierOrderId", table: "SupplierOrderLines", column: "SupplierOrderId");
            migrationBuilder.CreateIndex(name: "IX_SupplierOrderLines_ProductTypeId", table: "SupplierOrderLines", column: "ProductTypeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupplierOrderLines");
            migrationBuilder.DropTable(name: "SupplierOrders");
        }
    }
}
