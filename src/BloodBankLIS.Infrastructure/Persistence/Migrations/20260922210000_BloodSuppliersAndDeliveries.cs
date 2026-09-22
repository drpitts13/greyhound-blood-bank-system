using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260922210000_BloodSuppliersAndDeliveries")]
    public class BloodSuppliersAndDeliveries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloodSuppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Registration = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodSuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodSupplierId = table.Column<long>(type: "bigint", nullable: false),
                    PackingListNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodDeliveries_BloodSuppliers_BloodSupplierId",
                        column: x => x.BloodSupplierId,
                        principalTable: "BloodSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodDeliveryLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodDeliveryId = table.Column<long>(type: "bigint", nullable: false),
                    ProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDeliveryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodDeliveryLines_BloodDeliveries_BloodDeliveryId",
                        column: x => x.BloodDeliveryId,
                        principalTable: "BloodDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BloodDeliveryLines_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<long>(
                name: "DeliveryId",
                table: "BloodProducts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodSuppliers_Code",
                table: "BloodSuppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveries_BloodSupplierId",
                table: "BloodDeliveries",
                column: "BloodSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveries_Status",
                table: "BloodDeliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveryLines_BloodDeliveryId_ProductTypeId",
                table: "BloodDeliveryLines",
                columns: new[] { "BloodDeliveryId", "ProductTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveryLines_ProductTypeId",
                table: "BloodDeliveryLines",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodProducts_DeliveryId",
                table: "BloodProducts",
                column: "DeliveryId");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodProducts_BloodDeliveries_DeliveryId",
                table: "BloodProducts",
                column: "DeliveryId",
                principalTable: "BloodDeliveries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BloodProducts_BloodDeliveries_DeliveryId",
                table: "BloodProducts");
            migrationBuilder.DropIndex(name: "IX_BloodProducts_DeliveryId", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "DeliveryId", table: "BloodProducts");
            migrationBuilder.DropTable(name: "BloodDeliveryLines");
            migrationBuilder.DropTable(name: "BloodDeliveries");
            migrationBuilder.DropTable(name: "BloodSuppliers");
        }
    }
}
