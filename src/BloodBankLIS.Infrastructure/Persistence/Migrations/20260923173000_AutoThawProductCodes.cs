using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Auto-thaw rows are ISBT product description codes (E0707), not facility product types.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260923173000_AutoThawProductCodes")]
    public class AutoThawProductCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM AutoThawProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_AutoThawProducts_ProductTypes_ProductTypeId",
                table: "AutoThawProducts");

            migrationBuilder.DropIndex(
                name: "IX_AutoThawProducts_ProductTypeId",
                table: "AutoThawProducts");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "AutoThawProducts");

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "AutoThawProducts",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawProducts_ProductCode",
                table: "AutoThawProducts",
                column: "ProductCode",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AutoThawProducts_ProductCode",
                table: "AutoThawProducts");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "AutoThawProducts");

            migrationBuilder.AddColumn<long>(
                name: "ProductTypeId",
                table: "AutoThawProducts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AutoThawProducts_ProductTypeId",
                table: "AutoThawProducts",
                column: "ProductTypeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AutoThawProducts_ProductTypes_ProductTypeId",
                table: "AutoThawProducts",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
