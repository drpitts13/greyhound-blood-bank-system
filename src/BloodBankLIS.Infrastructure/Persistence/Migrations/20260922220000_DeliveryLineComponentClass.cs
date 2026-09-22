using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Delivery counts move from one row per catalog product to one row per component class.
    /// Existing lines are copied onto the product's class and quantities for the same class are added together.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260922220000_DeliveryLineComponentClass")]
    public class DeliveryLineComponentClass : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComponentClass",
                table: "BloodDeliveryLines",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE l
                SET l.ComponentClass = p.ComponentClass
                FROM BloodDeliveryLines AS l
                INNER JOIN ProductTypes AS p ON p.Id = l.ProductTypeId
                """);

            migrationBuilder.Sql("""
                UPDATE keep
                SET keep.ExpectedQuantity = summed.ExpectedQuantity
                FROM BloodDeliveryLines AS keep
                INNER JOIN (
                    SELECT BloodDeliveryId, ComponentClass, MIN(Id) AS KeepId, SUM(ExpectedQuantity) AS ExpectedQuantity
                    FROM BloodDeliveryLines
                    WHERE ComponentClass IS NOT NULL
                    GROUP BY BloodDeliveryId, ComponentClass
                ) AS summed ON summed.KeepId = keep.Id
                """);

            migrationBuilder.Sql("""
                DELETE l
                FROM BloodDeliveryLines AS l
                INNER JOIN (
                    SELECT BloodDeliveryId, ComponentClass, MIN(Id) AS KeepId
                    FROM BloodDeliveryLines
                    WHERE ComponentClass IS NOT NULL
                    GROUP BY BloodDeliveryId, ComponentClass
                ) AS summed
                    ON summed.BloodDeliveryId = l.BloodDeliveryId
                    AND summed.ComponentClass = l.ComponentClass
                    AND l.Id <> summed.KeepId
                """);

            migrationBuilder.Sql("DELETE FROM BloodDeliveryLines WHERE ComponentClass IS NULL");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodDeliveryLines_ProductTypes_ProductTypeId",
                table: "BloodDeliveryLines");

            migrationBuilder.DropIndex(
                name: "IX_BloodDeliveryLines_ProductTypeId",
                table: "BloodDeliveryLines");

            migrationBuilder.DropIndex(
                name: "IX_BloodDeliveryLines_BloodDeliveryId_ProductTypeId",
                table: "BloodDeliveryLines");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "BloodDeliveryLines");

            migrationBuilder.AlterColumn<int>(
                name: "ComponentClass",
                table: "BloodDeliveryLines",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveryLines_BloodDeliveryId_ComponentClass",
                table: "BloodDeliveryLines",
                columns: new[] { "BloodDeliveryId", "ComponentClass" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BloodDeliveryLines_BloodDeliveryId_ComponentClass",
                table: "BloodDeliveryLines");

            migrationBuilder.DropColumn(
                name: "ComponentClass",
                table: "BloodDeliveryLines");

            migrationBuilder.AddColumn<long>(
                name: "ProductTypeId",
                table: "BloodDeliveryLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveryLines_ProductTypeId",
                table: "BloodDeliveryLines",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDeliveryLines_BloodDeliveryId_ProductTypeId",
                table: "BloodDeliveryLines",
                columns: new[] { "BloodDeliveryId", "ProductTypeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodDeliveryLines_ProductTypes_ProductTypeId",
                table: "BloodDeliveryLines",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
